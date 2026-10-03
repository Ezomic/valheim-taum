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
    /// **Ownership.** A write to a ZDO you do not own is discarded, so the ZDO is claimed
    /// first, the same call ZNetView.ClaimOwnership makes. That is a claim, not a lock: if
    /// another player's machine owns the animal it is a race for the owner revision, and the
    /// higher one wins. A rider-less boar near its leader is nearly always owned by the leader,
    /// which is why this is accepted and not routed through an RPC to the old owner.
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
            if (__state || player == null || player != Player.m_localPlayer) return;
            if (!player.IsTeleporting()) return;
            if (!TaumConfig.Enabled.Value || !TaumConfig.FollowThroughPortals.Value) return;

            _trip = Followers(player);
            _started = Time.time;

            if (TaumConfig.Verbose.Value)
                TaumPlugin.Log.LogInfo("Portal: " + _trip.Count + " following animal(s) will come through.");
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
            if (!__state || __instance.IsTeleporting()) return;
            if (__instance != Player.m_localPlayer || _trip == null) return;

            var trip = _trip;
            _trip = null;

            if (Time.time - _started > Patience) return;

            Put(__instance, trip);
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
            var moved = 0;

            for (var i = 0; i < trip.Count; i++)
            {
                var zdo = ZDOMan.instance.GetZDO(trip[i]);
                if (zdo == null)
                {
                    TaumPlugin.LogOnce("Portal: a following animal's ZDO was no longer known here, so it stayed behind.");
                    continue;
                }

                var side = (i + 1) / 2 * 40f * (i % 2 == 1 ? 1f : -1f);
                var spot = origin + Quaternion.Euler(0f, side, 0f) * player.transform.forward * 2.5f
                           + Vector3.up * 0.5f;

                zdo.SetOwner(ZDOMan.GetSessionID());
                zdo.SetPosition(spot);

                // If the animal is still loaded (a short hop), move the object too, or its own
                // ZSyncTransform writes the old position straight back over the ZDO.
                var instance = ZNetScene.instance.FindInstance(zdo);
                if (instance != null) instance.transform.position = spot;

                moved++;
            }

            if (TaumConfig.Verbose.Value)
                TaumPlugin.Log.LogInfo("Portal: put " + moved + " of " + trip.Count + " animal(s) down beside you.");
        }
    }
}
