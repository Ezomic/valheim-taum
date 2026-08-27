using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// Everything tunable, bound in one place so the .cfg reads as a document rather than as
    /// whatever order the code happened to need things in.
    ///
    /// Note the standing BepInEx trap: every entry is written to disk on first run and the
    /// saved value beats a new default in code. Changing a default here does nothing on a
    /// machine that has already run the plugin - edit
    /// <c>&lt;profile&gt;\BepInEx\config\ezomic.valheim.taum.cfg</c> as part of the same
    /// change. When a config-driven change appears to do nothing in game, read the cfg
    /// before reading any code.
    /// </summary>
    internal static class TaumConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> Verbose;
        internal static ConfigEntry<UnityEngine.KeyCode> FollowKey;

        internal static void Bind(ConfigFile cfg)
        {
            // Every mod here has one, and it means the same thing every time: loaded, bound,
            // patched, and deciding nothing. Not "unloaded" - a plugin cannot unload itself,
            // and a switch that pretends otherwise is a lie somebody will debug.
            Enabled = cfg.Bind("Taum", "Enabled", true,
                "Off leaves the plugin loaded and changing nothing.");

            Verbose = cfg.Bind("Taum", "Verbose", false,
                "Write each halter put on, taken off and dropped to BepInEx/LogOutput.log.");

            FollowKey = cfg.Bind("Taum", "FollowKey", KeyCode.LeftAlt,
                "Held with E on a tamed boar or hen to toggle follow/stay. Alt because "
                + "vanilla already spends Shift+E on renaming and plain E on petting - "
                + "all three gestures keep their own key.");

            // ------------------------------------------------------------------ the item

            // Four shapes ship, and swapping is a line here rather than a rebuild. They are
            // deliberately different objects and not four skins of one: a rope halter and an
            // iron bridle disagree about what kind of husbandry this is.
            //
            //   halter_a  straps and buckles - the literal object
            //   halter_b  two fat cords and two knots
            //   halter_c  broad browband and iron cheek plates
            //   halter_d  a neck collar with no face piece at all
            //
            // The model file carries its own icon: <name>_icon.png beside the .obj is used
            // if it is there, and one is rendered from the model at load if it is not.
        }


        /// <summary>
        /// Splits a "Prefab:value" list into a lookup. Case-insensitive, because a prefab
        /// name typed into a config file will not match the game's capitalisation.
        /// </summary>
        internal static Dictionary<string, string> Table(string raw, char separator = ',')
        {
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(raw)) return table;

            foreach (var entry in raw.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                // Split on the FIRST colon only. An offset is "Hen:0,0,0" once the commas
                // have been split away, so the value half can carry its own separators.
                var text = entry.Trim();
                var colon = text.IndexOf(':');
                if (colon <= 0 || colon == text.Length - 1) continue;

                table[text.Substring(0, colon).Trim()] = text.Substring(colon + 1).Trim();
            }

            return table;
        }
    }
}
