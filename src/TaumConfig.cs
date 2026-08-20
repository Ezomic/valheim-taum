using System;
using System.Collections.Generic;
using BepInEx.Configuration;

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

        internal static ConfigEntry<string> Model;
        internal static ConfigEntry<string> Cost;
        internal static ConfigEntry<string> CraftingStation;
        internal static ConfigEntry<string> Donor;

        internal static ConfigEntry<string> Animals;
        internal static ConfigEntry<string> HeadBones;
        internal static ConfigEntry<string> Scales;
        internal static ConfigEntry<string> Offsets;

        internal static ConfigEntry<bool> RefuseHungry;
        internal static ConfigEntry<bool> RefuseAlerted;
        internal static ConfigEntry<bool> DropOnDeath;

        internal static ConfigEntry<string> HideDonors;
        internal static ConfigEntry<string> IronDonors;

        internal static void Bind(ConfigFile cfg)
        {
            // Every mod here has one, and it means the same thing every time: loaded, bound,
            // patched, and deciding nothing. Not "unloaded" - a plugin cannot unload itself,
            // and a switch that pretends otherwise is a lie somebody will debug.
            Enabled = cfg.Bind("Taum", "Enabled", true,
                "Off leaves the plugin loaded and changing nothing.");

            Verbose = cfg.Bind("Taum", "Verbose", false,
                "Write each halter put on, taken off and dropped to BepInEx/LogOutput.log.");

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
            Model = cfg.Bind("Item", "Model", "halter_a.obj",
                "Which halter model to wear and to show in the inventory. Ships halter_a "
                + "(straps), halter_b (rope), halter_c (heavy bridle) and halter_d (neck "
                + "collar). A file that is not there falls back to the donor's own look, "
                + "with a warning. Needs a restart: the model and its material are cached "
                + "for the life of the process.");

            Cost = cfg.Bind("Item", "Cost", "LeatherScraps:2",
                "What one halter costs, as Item:Count separated by commas. Two scraps is "
                + "deliberately near-free - the halter is a gesture, not a tier. What "
                + "limits how many animals you lead is how many of these you are carrying, "
                + "so a costly halter does not make leading rarer, it makes it fiddlier.");

            CraftingStation = cfg.Bind("Item", "CraftingStation", "piece_workbench",
                "Prefab name of the station you must stand near to make one. Empty makes it "
                + "craftable anywhere, which is wrong for leather work but is left "
                + "available. An unknown name is logged and treated as empty.");

            Donor = cfg.Bind("Item", "Donor", "LeatherScraps,DeerHide",
                "Prefab cloned for its machinery - ItemDrop, ZNetView, Rigidbody, the "
                + "floating and pickup behaviour. Its mesh and icon are both replaced, so "
                + "this is not a visual choice. First name that resolves wins.");

            // ------------------------------------------------------------------ the animals

            // Adults only, and the roster is exactly two. A saddled animal already solves
            // transport, a wolf is already commandable, and the young stay where they are
            // and grow up there - that last one is a design line, not an apology.
            Animals = cfg.Bind("Creatures", "Animals", "Boar,Hen",
                "Prefab names that accept a halter. Adults only: young animals are left to "
                + "grow up where they stand, which is the whole of why this does not turn "
                + "into a pied-piper mod. Lox and Asksvin are absent because a saddle "
                + "already answers the question, and Wolf because it is commandable "
                + "already - a halter there would be jewellery.");

            // Boar spells it Head and Hen spells it head, which is exactly the kind of fact
            // that has to come off a rip rather than a guess. The search is case-insensitive
            // and takes the first match, so both spellings are one entry.
            HeadBones = cfg.Bind("Creatures", "HeadBones", "Head,head,Neck,neck",
                "Bone names to hang the halter from, tried in order, case-insensitive. An "
                + "animal with none of them wears it on its root transform instead, which "
                + "looks wrong rather than crashing.");

            // Two animals whose heads differ by a factor of three. One model scaled per
            // species is the plan; halter_d exists to test whether that plan is wrong.
            Scales = cfg.Bind("Creatures", "Scales", "Boar:1.0,Hen:0.35",
                "Per-animal scale for the halter model, as Prefab:Scale. A boar's muzzle is "
                + "about 16cm across where a noseband sits and a hen's whole skull is about "
                + "5cm, so one number cannot serve both. Anything unlisted wears it at 1.");

            // Semicolons between entries, not commas: the value itself is a comma-separated
            // vector, and the suite's other prefab:x,y,z settings already read that way.
            Offsets = cfg.Bind("Creatures", "Offsets", "Boar:0,0,0;Hen:0,0,0",
                "Per-animal nudge in metres from the head bone, as Prefab:X,Y,Z separated "
                + "by semicolons, in the bone's own space. Zero everywhere until the models "
                + "have been looked at in game - this is the setting to reach for when one "
                + "sits in a jaw.");

            // ------------------------------------------------------------------ the rules

            RefuseHungry = cfg.Bind("Rules", "RefuseHungry", true,
                "A hungry animal will not take a halter. Feeding it first is the same "
                + "courtesy taming asked for, and it keeps the halter from being a way to "
                + "drag a starving animal across a continent.");

            RefuseAlerted = cfg.Bind("Rules", "RefuseAlerted", true,
                "A frightened animal will not take a halter. Vanilla already refuses to "
                + "tame one, and an animal that is fleeing something is not standing still "
                + "to be handled.");

            DropOnDeath = cfg.Bind("Rules", "DropOnDeath", true,
                "A haltered animal that dies drops the halter where it fell, the way a lox "
                + "drops its saddle. Off destroys it with the animal.");

            // ------------------------------------------------------------------ surfaces

            // Which vanilla prefab yields a material that reads as leather is a question for
            // the game, not a guess - so it is answerable without a rebuild.
            HideDonors = cfg.Bind("Surfaces", "HideDonors", "",
                "Prefabs to borrow the leather material from, comma separated, first hit "
                + "wins. Empty uses the built-in list, which starts at Lox: its saddle "
                + "child wears testloxsaddle_m, the one surface in the game that was "
                + "painted as tack rather than as a crate.");

            IronDonors = cfg.Bind("Surfaces", "IronDonors", "",
                "Prefabs to borrow the buckle and cheek-plate material from. Empty uses the "
                + "built-in list, which starts at a cauldron - the smallest piece of worked "
                + "iron the game models, and one that is in every base.");
        }

        /// <summary>
        /// Configured donor list for a material group, or null to use the built-in table.
        /// </summary>
        internal static string[] DonorsFor(string group)
        {
            string configured = null;

            if (string.Equals(group, "iron", StringComparison.OrdinalIgnoreCase))
                configured = IronDonors == null ? null : IronDonors.Value;
            else if (string.Equals(group, "hide", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(group, "darkhide", StringComparison.OrdinalIgnoreCase))
                configured = HideDonors == null ? null : HideDonors.Value;

            if (string.IsNullOrEmpty(configured)) return null;
            return configured.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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
