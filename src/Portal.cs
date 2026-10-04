using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// An animal you told to follow goes through a portal with you; one left staying does not.
    ///
    /// **Why this is two steps and not one.** The obvious move is to put the animals beside
    /// you when the portal fires. It cannot work: Player.TeleportTo starts a countdown, the
    /// player's transform jumps to the destination two seconds in, and the game then waits for
    /// the destination area to load before it lets the player go. For those seconds the old
    /// zone is being let go of, so an animal that was standing beside you is on its way to
    /// being destroyed, and an animal placed at the destination before that ground exists
    /// falls through the world. So the trip is remembered when the portal fires (by ZDOID,
    /// which outlives the loaded object) and the animals are put down when
    /// Player.UpdateTeleport reports the player has landed, which is the game's own "the area
    /// is ready and there is floor" moment.
    ///
    /// **Why the ZDO and not the animal.** By landing time the animal's GameObject is usually
    /// gone. Its ZDO is not, and moving the ZDO to the destination is all it takes: the scene
    /// instantiates it there from its saved state, and Tameable.UpdateSavedFollowTarget then
    /// finds the "follow" name in that ZDO and has it follow you again by itself.
    ///
    /// **Ownership, and why landing is checked.** A write to a ZDO you do not own is
    /// discarded, so the ZDO is claimed first, the same call ZNetView.ClaimOwnership makes. A
    /// claim is not a lock, and the claim alone is not enough: every two seconds the server's
    /// ZDOMan.ReleaseNearbyZDOS hands an animal to whichever player is near it, and the
    /// leader's reference position is now far away. If another player is standing at the old
    /// portal, their machine owns the animal and keeps writing it from the old spot, while the
    /// leader's SetOwner and SetPosition carry stale revisions. RPC_ZDOData accepts a packet
    /// only when its data revision beats the server's, so the move is dropped without a word
    /// and the animal stays. So a short while after the move each animal is checked: standing
    /// near its spot means it took, and only those are counted. By then the server's newer
    /// data has overwritten a lost write locally, so the position read is the truth. An animal
    /// owned by someone else is not written to at all; the placement is routed to its owner,
    /// who is the only machine whose write counts (the Taum_Place message below).
    ///
    /// **The ore rule is not touched.** This only watches portals the game has already let the
    /// player into: if Player.IsTeleportable refuses, no teleport starts, and the trip never
    /// begins. The animals carry nothing, so they cannot be a way round it.
    /// </summary>
    [HarmonyPatch]
    internal static class Portal
    {
        /// <summary>
        /// How far from you an animal may be and still go. Following animals trail a few metres
        /// behind, so this is generous; it exists so that one following you from the far side
        /// of the farm is not dragged out of its pen. A constant because nobody has asked for it
        /// to be a setting.
        /// </summary>
        private const float Reach = 25f;

        /// <summary>
        /// How long a remembered trip is believed. A teleport takes at most 15 seconds, so a
        /// trip older than this belongs to a teleport that never landed (a death, a logout) and
        /// must not be cashed in by the next unrelated one.
        /// </summary>
        private const float Patience = 60f;

        /// <summary>
        /// How long after a move before an animal is checked, and how many checks. The owner
        /// change and the position travel through the server, so the first look has to wait a
        /// round trip; three looks cover a slow link without keeping the trip alive for long.
        /// </summary>
        private const float CheckEvery = 1.5f;

        private const int Checks = 3;

        /// <summary>How close to its spot an animal must be to count as having arrived.</summary>
        private const float Landed = 6f;

        private const string PlaceRpc = "Taum_Place";

        /// <summary>
        /// How far from the sender a requested spot may be. Put sets animals 2.5 m in front of
        /// the leader, so this is generous; it exists so the message cannot be used to throw a
        /// follower of the sender's across the world.
        /// </summary>
        private const float PlaceRange = 40f;

        private static List<ZDOID> _trip;
        private static float _started;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        private static void Entering(Player player, out bool __state)
        {
            __state = player != null && player.IsTeleporting();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        private static void Entered(Player player, bool __state)
        {
            // Teleport returns void and refuses in several ways, each ending in a message and
            // no teleport. A teleport that actually began is the one fact they share.
            try
            {
                if (__state || player == null || player != Player.m_localPlayer) return;
                if (!player.IsTeleporting()) return;
                if (!TaumConfig.Enabled.Value || !TaumConfig.FollowThroughPortals.Value) return;

                _trip = Followers(player);
                _started = Time.time;

                if (TaumConfig.Verbose.Value)
                    TaumPlugin.Log.LogInfo("Portal: " + _trip.Count + " following animal(s) will come through.");
            }
            catch (Exception e)
            {
                // A failure here costs the animals, never the teleport the player asked for.
                _trip = null;
                TaumPlugin.LogOnce("Portal: could not note the following animals, so none will come through: " + e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "UpdateTeleport")]
        private static void Counting(Player __instance, out bool __state)
        {
            __state = __instance.IsTeleporting();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdateTeleport")]
        private static void Landing(Player __instance, bool __state)
        {
            try
            {
                if (!__state || __instance.IsTeleporting()) return;
                if (__instance != Player.m_localPlayer || _trip == null) return;

                var trip = _trip;
                _trip = null;

                if (Time.time - _started > Patience) return;

                Put(__instance, trip);
            }
            catch (Exception e)
            {
                _trip = null;
                TaumPlugin.LogOnce("Portal: could not put the following animals down, so they stayed behind: " + e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNet), "Awake")]
        private static void Wire()
        {
            // A fresh ZRoutedRpc is built with every ZNet, so the registration is too. A peer
            // without Taum has no handler for the name and drops the message.
            try
            {
                if (ZRoutedRpc.instance != null)
                    ZRoutedRpc.instance.Register<ZDOID, Vector3>(PlaceRpc, RPC_Place);
            }
            catch (Exception e)
            {
                TaumPlugin.LogOnce("Portal: could not register the placement message, so animals owned by other players will stay behind: " + e);
            }
        }

        /// <summary>
        /// Runs on the machine that owns the animal, which is the only one whose write to it
        /// counts. Refuses anything that is not an animal someone told to follow, so the
        /// message is not a way to move arbitrary objects.
        /// </summary>
        private static void RPC_Place(long sender, ZDOID id, Vector3 spot)
        {
            try
            {
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || !zdo.IsOwner()) return;
                var follows = zdo.GetString(ZDOVars.s_follow, "");
                if (follows == "") return;

                if (!SenderMayPlace(sender, follows, spot))
                {
                    TaumPlugin.LogOnce("Portal: refused a placement request from a peer that is not the animal's leader or asked for a spot far from them.");
                    return;
                }

                Place(zdo, spot);
            }
            catch (Exception e)
            {
                TaumPlugin.LogOnce("Portal: could not place an animal on request: " + e);
            }
        }

        /// <summary>
        /// The sender must be the player the animal follows, found through the player list, which
        /// every machine has: its character's ZDOID carries the session id of the machine that
        /// made it, which is the id a routed message arrives with. Players are looked up there
        /// and not through the loaded ones because the leader has just landed far from the old
        /// zone, so their Player is usually not loaded on the animal's owner. For the same reason
        /// their position is known only when it is public or their ZDO has been sent here; when
        /// neither is true the name check stands alone and the spot cannot be compared.
        /// </summary>
        private static bool SenderMayPlace(long sender, string follows, Vector3 spot)
        {
            if (ZNet.instance == null) return false;

            foreach (var info in ZNet.instance.GetPlayerList())
            {
                if (info.m_characterID == ZDOID.None || info.m_characterID.UserID != sender) continue;
                if (info.m_name != follows) return false;

                if (info.m_publicPosition) return Vector3.Distance(info.m_position, spot) <= PlaceRange;

                var leader = ZDOMan.instance.GetZDO(info.m_characterID);
                return leader == null || Vector3.Distance(leader.GetPosition(), spot) <= PlaceRange;
            }

            return false;
        }

        private static List<ZDOID> Followers(Player player)
        {
            var found = new List<KeyValuePair<float, ZDOID>>();
            var name = player.GetPlayerName();
            var here = player.transform.position;

            foreach (var character in Character.GetAllCharacters())
            {
                if (character == null || character.IsPlayer()) continue;
                if (!character.TryGetComponent(out Tameable tameable) || !Follow.Wants(tameable)) continue;
                if (!character.TryGetComponent(out ZNetView nview) || !nview.IsValid()) continue;

                // The game's own record of who it follows, not our own: the same field the
                // hover line reads, and the one that survives the animal being unloaded.
                if (nview.GetZDO().GetString(ZDOVars.s_follow, "") != name) continue;

                var away = Vector3.Distance(character.transform.position, here);
                if (away > Reach) continue;

                found.Add(new KeyValuePair<float, ZDOID>(away, nview.GetZDO().m_uid));
            }

            found.Sort((a, b) => a.Key.CompareTo(b.Key));

            var trip = new List<ZDOID>();
            foreach (var entry in found)
            {
                if (trip.Count >= TaumConfig.PortalMax.Value) break;
                trip.Add(entry.Value);
            }

            return trip;
        }

        /// <summary>
        /// Puts the animals down in a fan in front of the player. In front, because the portal
        /// is behind you when you step out and an animal set on top of the frame is stuck in it.
        /// </summary>
        private static void Put(Player player, List<ZDOID> trip)
        {
            var origin = player.transform.position;
            var pending = new Dictionary<ZDOID, Vector3>();

            for (var i = 0; i < trip.Count; i++)
            {
                var side = (i + 1) / 2 * 40f * (i % 2 == 1 ? 1f : -1f);
                var spot = origin + Quaternion.Euler(0f, side, 0f) * player.transform.forward * 2.5f
                           + Vector3.up * 0.5f;

                var zdo = ZDOMan.instance.GetZDO(trip[i]);
                if (zdo == null)
                {
                    TaumPlugin.LogOnce("Portal: a following animal's ZDO was no longer known here, so it stayed behind.");
                    continue;
                }

                pending[trip[i]] = spot;
                Move(zdo, spot);
            }

            if (pending.Count == 0 || TaumPlugin.Instance == null) return;

            TaumPlugin.Instance.StartCoroutine(Verify(pending, trip.Count));
        }

        /// <summary>
        /// Writes the move here when this machine owns the animal or nobody does, and asks the
        /// owner to when somebody else does. A write to an animal another player owns is
        /// discarded by the server, so making it would only look like it worked.
        /// </summary>
        private static void Move(ZDO zdo, Vector3 spot)
        {
            var owner = zdo.GetOwner();
            if (!zdo.HasOwner() || owner == ZDOMan.GetSessionID())
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
                Place(zdo, spot);
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(owner, PlaceRpc, zdo.m_uid, spot);
        }

        private static void Place(ZDO zdo, Vector3 spot)
        {
            zdo.SetPosition(spot);

            // If the animal is still loaded (a short hop), move the object too, or its own
            // ZSyncTransform writes the old position straight back over the ZDO.
            var instance = ZNetScene.instance.FindInstance(zdo);
            if (instance != null) instance.transform.position = spot;
        }

        /// <summary>
        /// Looks a moment later, and again, at whether each animal is where it was sent. A
        /// move that lost the revision race leaves no error anywhere, so this is the only
        /// place the failure can be seen. An animal that took is counted; one that did not is
        /// tried again against whoever owns it now, and the last look says how many made it.
        /// </summary>
        private static IEnumerator Verify(Dictionary<ZDOID, Vector3> pending, int asked)
        {
            var took = 0;

            for (var look = 0; look < Checks && pending.Count > 0; look++)
            {
                yield return new WaitForSeconds(CheckEvery);

                try
                {
                    var done = new List<ZDOID>();
                    foreach (var entry in pending)
                    {
                        var zdo = ZDOMan.instance.GetZDO(entry.Key);
                        if (zdo == null)
                        {
                            done.Add(entry.Key);
                            continue;
                        }

                        if (Vector3.Distance(zdo.GetPosition(), entry.Value) <= Landed)
                        {
                            took++;
                            done.Add(entry.Key);
                            continue;
                        }

                        if (look < Checks - 1) Move(zdo, entry.Value);
                    }

                    foreach (var id in done) pending.Remove(id);
                }
                catch (Exception e)
                {
                    TaumPlugin.LogOnce("Portal: could not check the animals after the move: " + e);
                    yield break;
                }
            }

            if (pending.Count > 0)
                TaumPlugin.LogOnce("Portal: " + pending.Count + " animal(s) did not follow through the portal; another player's machine kept them.");

            if (TaumConfig.Verbose.Value)
                TaumPlugin.Log.LogInfo("Portal: " + took + " of " + asked + " animal(s) are down beside you.");
        }
    }
}
