using HarmonyLib;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// Alt+E on a tamed farm animal and it follows you; Alt+E again and it stays.
    ///
    /// This replaced the halter - the crafted item, the worn model, the lead physics,
    /// all of it - on his call: "can we maybe have taum without the lead, just an
    /// option for the animals next to pet". The whole first design is preserved on the
    /// `halter` branch; what main keeps is the one thing the mod is actually for,
    /// which is walking a boar to the new pen without building a road of turnips.
    ///
    /// **Why Alt.** Shift+E was the first ask and is taken: the `alt` flag vanilla
    /// passes into Interact is the AltPlace button, Left Shift by default, and on a
    /// Tameable it already means rename. Hold-E was built next and worked, but a held
    /// key that does something is invisible - nothing in the game teaches the gesture.
    /// Alt+E is genuinely unbound in vanilla, sits beside E where he asked for it, and
    /// the hover text can say it plainly. The modifier is config, not constant.
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
        private static bool AltToFollow(Tameable __instance, Humanoid user, bool hold,
                                        bool alt, ref bool __result)
        {
            // hold repeats while E is down, and alt is vanilla's rename - both stay
            // vanilla's. Only a plain press with the follow modifier down is ours.
            if (hold || alt || !Wants(__instance)) return true;
            if (!Input.GetKey(TaumConfig.FollowKey.Value)) return true;

            __instance.Command(user);
            __result = true;
            return false;
        }

        /// <summary>
        /// Say the gesture exists, or nobody finds it.
        ///
        /// Localized HERE, not left to the caller: vanilla localizes inside
        /// GetHoverText and returns finished text, so anything a postfix appends is
        /// shown verbatim - the first cut printed a literal "$KEY_Use" in the hover.
        /// The modifier drops its Left/Right prefix for display only; the config
        /// value stays the exact KeyCode.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), "GetHoverText")]
        private static void Offer(Tameable __instance, ref string __result)
        {
            if (!Wants(__instance) || string.IsNullOrEmpty(__result)) return;

            var key = TaumConfig.FollowKey.Value.ToString()
                .Replace("Left", "").Replace("Right", "");

            __result += Localization.instance.Localize(
                "\n[<color=yellow><b>" + key + " + $KEY_Use</b></color>] Follow / stay");
        }
    }
}
