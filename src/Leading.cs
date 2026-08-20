using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// Putting a halter on, taking it off, and what happens when the animal dies.
    ///
    /// Every seam here is one vanilla already uses for the lox saddle, and that is the whole
    /// design. <c>Tameable.UseItem</c> is where a saddle goes on. <c>Tameable.Command</c> is
    /// what a wolf's follow toggle calls. The ZDO carries the fact across the network and
    /// across a reload the same way <c>s_haveSaddleHash</c> does. Nothing here reimplements
    /// following, ownership or persistence, because all three already exist and are correct.
    ///
    /// Two things about vanilla make this cheaper than it looks, and both were read out of
    /// the decompiled source rather than assumed:
    ///
    ///   <c>Tameable.Command</c> does NOT check <c>m_commandable</c>. It invokes an RPC that
    ///   toggles the MonsterAI's follow target, so it works on a boar exactly as it does on a
    ///   wolf. The flag only gates whether petting triggers it, which is why a boar has never
    ///   followed anybody without this.
    ///
    ///   <c>Tameable.Interact</c> returns false immediately when <c>hold</c> is true, so
    ///   hold-E on a tamed animal is an unused gesture. It costs nothing to take and it does
    ///   not shadow anything a player already does.
    /// </summary>
    [HarmonyPatch]
    internal static class Leading
    {
        /// <summary>
        /// The ZDO key carrying "this animal is wearing a halter". Hashed with
        /// GetStableHashCode like every other ZDO key - the exception is ZSyncAnimation's,
        /// which is not this.
        /// </summary>
        private const string HalterKey = "taum_halter";

        private const string AddRpc = "Taum_AddHalter";
        private const string SetRpc = "Taum_SetHalter";

        /// <summary>
        /// Last release per animal, to swallow the repeats of a held key. Player.Interact
        /// throttles a hold to one call every 0.2s, and the ZDO write travels to the owner
        /// and back - so between the press and the fact arriving, a second call would read
        /// "still haltered" and hand back a second halter out of nothing.
        /// </summary>
        private static readonly Dictionary<int, float> Released = new Dictionary<int, float>();

        // ------------------------------------------------------------------ registration

        /// <summary>
        /// Register the two RPCs on every Tameable as it wakes.
        ///
        /// It has to be Awake and it has to be every client: ZNetView.Register builds a local
        /// table of what this instance will answer to, so an RPC invoked at an instance that
        /// never registered it is dropped with a warning and no effect. Vanilla registers its
        /// saddle RPCs here too, and only when there is a saddle - which is exactly why a
        /// boar has no AddSaddle to borrow.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), "Awake")]
        private static void TameableAwake(Tameable __instance)
        {
            if (!TaumConfig.Enabled.Value) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;
            if (!Accepts(__instance)) return;

            nview.Register(AddRpc, sender => RpcAdd(__instance, nview));
            nview.Register<bool>(SetRpc, (sender, on) => Show(__instance, on));

            // A halter already on this animal when the object spawns - a world reload, or
            // walking back into a zone. The ZDO is the truth; the visual is rebuilt from it.
            if (Wearing(nview)) Show(__instance, true);
        }

        /// <summary>Runs on the owner. Vanilla's RPC_AddSaddle in every respect but the key.</summary>
        private static void RpcAdd(Tameable tameable, ZNetView nview)
        {
            if (!nview.IsOwner()) return;
            if (Wearing(nview)) return;

            nview.GetZDO().Set(HalterKey, true);
            nview.InvokeRPC(ZNetView.Everybody, SetRpc, true);
        }

        // ------------------------------------------------------------------ putting it on

        /// <summary>
        /// A postfix, and it only ever acts when vanilla has already declined.
        ///
        /// UseItem returns false for anything that is not this animal's saddle item, so
        /// false is the signal that the item was not handled. Acting only on false means a
        /// lox wearing a halter-shaped item still gets its saddle, and any mod that handles
        /// an item before this one wins - which is the polite order.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.UseItem))]
        private static void UseItem(Tameable __instance, Humanoid user, ItemDrop.ItemData item,
            ref bool __result)
        {
            if (__result) return;
            if (!TaumConfig.Enabled.Value) return;
            if (!Halter.Is(item)) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;

            // Consume the press whatever the answer is. Returning false here would let the
            // item fall through to whatever vanilla does with an unhandled Misc item, and
            // "nothing happened" is a worse answer than being told why.
            __result = true;

            if (!__instance.IsTamed())
            {
                Refuse(user, __instance, "will not take a halter until it is tame");
                return;
            }

            if (!Accepts(__instance))
            {
                Refuse(user, __instance, "is not an animal a halter fits");
                return;
            }

            // Young animals are a different prefab with a Growup component on it, and they
            // are left out on purpose: they stay where they are and grow up there. Checking
            // the component rather than the name catches every young thing, including any
            // the game adds later.
            if (__instance.GetComponent<Growup>() != null)
            {
                Refuse(user, __instance, "is too young - it will grow up where it stands");
                return;
            }

            if (Wearing(nview))
            {
                Refuse(user, __instance, "is already wearing one");
                return;
            }

            if (TaumConfig.RefuseHungry.Value && __instance.IsHungry())
            {
                Refuse(user, __instance, "is hungry - feed it first");
                return;
            }

            var ai = __instance.GetComponent<MonsterAI>();
            if (TaumConfig.RefuseAlerted.Value && ai != null && ai.IsAlerted())
            {
                Refuse(user, __instance, "is frightened - it will not stand to be handled");
                return;
            }

            // The order matters. Take the item first: if the RPC is what removed it, a lost
            // packet would leave the halter both in the inventory and on the animal.
            user.GetInventory().RemoveOneItem(item);
            nview.InvokeRPC(AddRpc);

            // And it walks with you. Command toggles, and an animal that was already
            // following would be told to stay by this - so only push it the way it is not.
            if (ai != null && ai.GetFollowTarget() == null) __instance.Command(user, message: false);

            user.Message(MessageHud.MessageType.Center,
                __instance.GetHoverName() + " takes the halter and follows you");

            if (TaumConfig.Verbose.Value)
                TaumPlugin.Log.LogInfo("Haltered " + Name(__instance) + ".");
        }

        // ------------------------------------------------------------------ taking it off

        /// <summary>
        /// Hold Use to take the halter back.
        ///
        /// Vanilla's Interact returns false the moment hold is true, so this gesture is
        /// unclaimed. Alt is not: shift-Use on a tamed animal opens the rename box, and
        /// taking that would have cost a feature to add one.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
        private static void Interact(Tameable __instance, Humanoid user, bool hold, bool alt,
            ref bool __result)
        {
            if (__result || !hold || alt) return;
            if (!TaumConfig.Enabled.Value) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !Wearing(nview)) return;

            var id = __instance.GetInstanceID();
            float last;
            if (Released.TryGetValue(id, out last) && Time.time - last < 1f) return;
            Released[id] = Time.time;

            var player = user as Player;
            if (player == null) return;

            // Ownership first, then the write. A ZDO written by anybody but the owner is
            // discarded silently, which would present as a halter that comes off and then
            // reappears the moment the animal is looked at again.
            nview.ClaimOwnership();
            nview.GetZDO().Set(HalterKey, false);
            nview.InvokeRPC(ZNetView.Everybody, SetRpc, false);

            // Stop following. Command toggles, so this is only sent when it is following -
            // otherwise taking the halter off an animal that was already told to stay would
            // set it walking after you without one.
            var ai = __instance.GetComponent<MonsterAI>();
            if (ai != null && ai.GetFollowTarget() != null) __instance.Command(user, message: false);

            Return(player, __instance);
            __result = true;

            if (TaumConfig.Verbose.Value)
                TaumPlugin.Log.LogInfo("Unhaltered " + Name(__instance) + ".");
        }

        /// <summary>
        /// Hand the halter back, and drop it at the animal's feet if there is no room.
        /// Destroying it because an inventory is full is the kind of quiet loss that reads
        /// as a bug in the mod, which it would be.
        /// </summary>
        private static void Return(Player player, Tameable tameable)
        {
            var prefab = ObjectDB.instance == null
                ? null
                : ObjectDB.instance.GetItemPrefab(Halter.PrefabName);

            if (prefab == null)
            {
                TaumPlugin.LogOnce("The halter prefab is missing from ObjectDB, so one could "
                    + "not be handed back. It is gone rather than duplicated.");
                return;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return;

            if (player.GetInventory().AddItem(prefab, 1) != null)
            {
                player.Message(MessageHud.MessageType.Center,
                    tameable.GetHoverName() + " stays here. Halter returned.");
                return;
            }

            UnityEngine.Object.Instantiate(prefab,
                tameable.transform.position + Vector3.up * 0.5f, Quaternion.identity);

            player.Message(MessageHud.MessageType.Center,
                tameable.GetHoverName() + " stays here. Your pack is full, so the halter is "
                + "on the ground.");
        }

        // ------------------------------------------------------------------ death

        /// <summary>
        /// A haltered animal that dies leaves the halter where it fell, the way a lox leaves
        /// its saddle. Vanilla does its own version of this in Tameable.OnDeath, which is
        /// private and reached through the Character.m_onDeath delegate.
        ///
        /// Note that this runs on the OWNING CLIENT ONLY. Character.OnDeath's own IsOwner
        /// early-return is dead code because CheckDeath is its only caller and that already
        /// sits inside an IsOwner branch - so everything here happens once, on one machine,
        /// and spawning the item is correct rather than six halters for six players.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), "OnDeath")]
        private static void OnDeath(Tameable __instance)
        {
            if (!TaumConfig.Enabled.Value || !TaumConfig.DropOnDeath.Value) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !Wearing(nview)) return;

            var prefab = ObjectDB.instance == null
                ? null
                : ObjectDB.instance.GetItemPrefab(Halter.PrefabName);
            if (prefab == null) return;

            nview.GetZDO().Set(HalterKey, false);

            UnityEngine.Object.Instantiate(prefab,
                __instance.transform.position + Vector3.up, Quaternion.identity);

            if (TaumConfig.Verbose.Value)
                TaumPlugin.Log.LogInfo("Dropped the halter of a dead " + Name(__instance) + ".");
        }

        // ------------------------------------------------------------------ hover text

        /// <summary>
        /// Say that the gesture exists, and name the key the player has actually bound.
        /// Localization.Localize("$KEY_Use") returns the current binding, so this follows a
        /// rebind for free rather than insisting on E.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        private static void GetHoverText(Tameable __instance, ref string __result)
        {
            if (!TaumConfig.Enabled.Value || string.IsNullOrEmpty(__result)) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !Wearing(nview)) return;

            __result += "\n[<color=yellow><b>" + Localization.instance.Localize("$KEY_Use")
                        + " (hold)</b></color>] Take off the halter";
        }

        // ------------------------------------------------------------------ the visual

        /// <summary>
        /// Show or hide the worn halter. Called on every client through the SetRpc, which is
        /// what makes other players see it rather than only the one who put it on.
        /// </summary>
        private static void Show(Tameable tameable, bool on)
        {
            var existing = tameable.GetComponentInChildren<WornHalter>(true);

            if (!on)
            {
                if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
                return;
            }

            if (existing != null) return;
            WornHalter.Attach(tameable);
        }

        // ------------------------------------------------------------------ small answers

        private static bool Wearing(ZNetView nview)
        {
            return nview.IsValid() && nview.GetZDO().GetBool(HalterKey);
        }

        /// <summary>
        /// Whether this animal is on the list. Compared on the prefab name with "(Clone)"
        /// stripped, which is what Utils.GetPrefabName is for.
        /// </summary>
        private static bool Accepts(Tameable tameable)
        {
            var name = Name(tameable);
            if (string.IsNullOrEmpty(name)) return false;

            foreach (var allowed in (TaumConfig.Animals.Value ?? "").Split(','))
            {
                if (string.Equals(allowed.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string Name(Tameable tameable)
        {
            return Utils.GetPrefabName(tameable.gameObject);
        }

        private static void Refuse(Humanoid user, Tameable tameable, string because)
        {
            user.Message(MessageHud.MessageType.Center,
                tameable.GetHoverName() + " " + because + ".");
        }
    }
}
