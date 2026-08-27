using HarmonyLib;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// Hold E on a tamed farm animal and it follows you; hold again and it stays.
    ///
    /// This replaced the halter - the crafted item, the worn model, the lead physics,
    /// all of it - on his call: "can we maybe have taum without the lead, just an
    /// option for the animals next to pet". The whole first design is preserved on the
    /// `halter` branch; what main keeps is the one thing the mod is actually for,
    /// which is walking a boar to the new pen without building a road of turnips.
    ///
    /// **Why hold-E and not Shift+E.** Both were considered; Shift+E was the ask. But
    /// on a Tameable, alt is already vanilla's rename - `if (alt) { SetName(); }` -
    /// so taking it would cost naming your animals. Hold is genuinely free: vanilla's
    /// Interact opens with `if (hold) return false`, and unlike a container nothing
    /// opens a window first to swallow the gesture (the chest lesson does not apply -
    /// a tap on an animal pets, it does not open UI).
    ///
    /// **Why vanilla's Command and not a custom follow.** Wolves and lox already do
    /// exactly this through Tameable.Command - the follow AI, the "follows you" and
    /// "stays" messages, the ownership RPC so it works on a server - all gated behind
    /// m_commandable, which boars and hens simply lack. Calling Command on them uses
    /// every part of that machinery unchanged. Riding the seam, as ever: flipping
    /// m_commandable instead was rejected because that merges pet and command the way
    /// wolves have it, and petting your way down a row of boars must not march the
    /// whole farm after you.
    /// </summary>
    [HarmonyPatch]
    internal static class Follow
    {
        /// <summary>Per-animal debounce: hold fires every frame, a toggle must not.</summary>
        private static readonly System.Collections.Generic.Dictionary<int, float> Last =
            new System.Collections.Generic.Dictionary<int, float>();

        private static bool Wants(Tameable tameable)
        {
            if (!TaumConfig.Enabled.Value || tameable == null) return false;
            if (!tameable.IsTamed()) return false;

            // Only the animals vanilla refuses to command. Wolves and lox keep their
            // own gesture; doubling it would toggle them twice per press.
            return !tameable.m_commandable;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Tameable), "Interact")]
        private static bool HoldToFollow(Tameable __instance, Humanoid user, bool hold,
                                         ref bool __result)
        {
            if (!hold || !Wants(__instance)) return true;

            var id = __instance.GetInstanceID();
            float last;
            if (Last.TryGetValue(id, out last) && Time.time - last < 1.2f)
            {
                __result = false;
                return false;
            }
            Last[id] = Time.time;

            __instance.Command(user);
            __result = true;
            return false;
        }

        /// <summary>
        /// Say the gesture exists, or nobody finds it. $KEY_Use renders the bound key.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), "GetHoverText")]
        private static void Offer(Tameable __instance, ref string __result)
        {
            if (!Wants(__instance) || string.IsNullOrEmpty(__result)) return;

            __result += "\n[Hold <color=yellow><b>$KEY_Use</b></color>] Follow / stay";
        }
    }
}
