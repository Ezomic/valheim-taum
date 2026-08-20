using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// The halter item: the prefab, its look, and the recipe that makes it.
    ///
    /// It is a clone of a vanilla item rather than a GameObject built from nothing, because
    /// what is wanted here is entirely the machinery - ItemDrop, ZNetView, Rigidbody, the
    /// floating, the pickup radius, the despawn timer. None of that is worth reimplementing
    /// and all of it is one line to inherit. The mesh, the material and the icon are all
    /// replaced afterwards, so nothing of the donor's appearance survives.
    /// </summary>
    internal static class Halter
    {
        /// <summary>
        /// Permanent. ZNetScene keys on <c>name.GetStableHashCode()</c> and saved ZDOs store
        /// that hash, and ZNetScene DISCARDS a ZDO whose prefab name no longer resolves - so
        /// renaming this destroys every halter lying on the ground in every world that has
        /// ever run this mod, silently. It is not a display name and it is not tidy-able.
        /// </summary>
        internal const string PrefabName = "TaumHalter";

        /// <summary>What a player sees. Safe to change; unlike the prefab name, nothing keys on it.</summary>
        private const string DisplayName = "Halter";

        private const string Description =
            "A loop of hide for the head of an animal that already trusts you. "
            + "Put it on a tamed boar or hen and it walks with you; take it off and it stays "
            + "where it stands.";

        /// <summary>
        /// Registered through the shared keeper rather than by hand, because the hard part is
        /// not registering once - it is registering again for every world. ZNetScene and
        /// ObjectDB are torn down and rebuilt on every world load, including a trip out to
        /// the menu and back, and a flag that says "already done" is how a prefab quietly
        /// stops existing in the second world of a session.
        /// </summary>
        internal static void Keep()
        {
            Ezomic.Shared.Prefabs.Keep(PrefabName, Build, item: true);
        }

        /// <summary>
        /// Called by the keeper whenever a world needs this prefab built. Everything here is
        /// done inside an inactive holder with init suppressed - see Prefabs.Clone - so the
        /// clone never tries to network-register itself half-built.
        /// </summary>
        private static GameObject Build()
        {
            string chosen;
            var donor = Ezomic.Shared.Prefabs.Donor(TaumConfig.Donor.Value, out chosen);
            if (donor == null)
            {
                TaumPlugin.Log.LogError(
                    "No donor item resolved from '" + TaumConfig.Donor.Value
                    + "' - the halter cannot be built. Set Item.Donor to something loaded.");
                return null;
            }

            var clone = Ezomic.Shared.Prefabs.Clone(donor, PrefabName);
            if (clone == null) return null;

            var drop = clone.GetComponent<ItemDrop>();
            if (drop == null)
            {
                TaumPlugin.Log.LogError(chosen + " has no ItemDrop, so it cannot donate to an item.");
                UnityEngine.Object.DestroyImmediate(clone);
                return null;
            }

            Dress(clone, drop);
            return clone;
        }

        /// <summary>
        /// Everything about the clone that is Taum's rather than the donor's.
        /// </summary>
        private static void Dress(GameObject clone, ItemDrop drop)
        {
            var shared = drop.m_itemData.m_shared;

            shared.m_name = DisplayName;
            shared.m_description = Description;

            // Misc, not Material. A halter is used from the hotbar - Humanoid.UseItem only
            // reaches Tameable.UseItem for an item the player can actually hold - and the
            // material category is the one bucket that never gets there.
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;

            shared.m_maxStackSize = 10;
            shared.m_weight = 0.5f;
            shared.m_teleportable = true;

            // Not equipment, not a tool, not consumed by use. The one thing it must be is
            // usable, which is what m_useDurability = false and no attack leave it as.
            shared.m_useDurability = false;
            shared.m_questItem = false;

            drop.m_itemData.m_stack = 1;
            drop.m_itemData.m_quality = 1;
            drop.m_itemData.m_durability = 0f;

            Model(clone);
            Icon(shared);
        }

        /// <summary>
        /// Swap the donor's mesh for the configured halter, wearing borrowed vanilla
        /// materials rather than an authored texture.
        /// </summary>
        private static void Model(GameObject clone)
        {
            var file = TaumConfig.Model.Value;
            if (string.IsNullOrEmpty(file)) return;

            var model = ObjMesh.Load(Path.Combine(AssetDir, file));
            if (model == null || model.Mesh == null)
            {
                TaumPlugin.LogOnce(
                    "No halter model at " + file + " - it keeps the donor's look. "
                    + "Check Item.Model against what is beside the DLL.");
                return;
            }

            // An item's visible mesh is not always on the root: the donor carries an
            // "attach" child that the ItemDrop shows. Take whichever renderer the donor
            // actually draws with rather than assuming.
            var renderer = clone.GetComponentInChildren<MeshRenderer>(true);
            var filter = clone.GetComponentInChildren<MeshFilter>(true);

            if (renderer == null || filter == null)
            {
                TaumPlugin.LogOnce("The donor draws with no MeshRenderer, so the halter "
                    + "model cannot replace it. It will look like its donor.");
                return;
            }

            filter.sharedMesh = model.Mesh;
            renderer.sharedMaterials = Skins.Skin(model.Groups);

            // A dropped halter is a small object and the donor's collider is sized for the
            // donor. Leaving it is deliberate: the collider is what a player's pickup sphere
            // finds, and a collider slightly too generous is a halter that is easy to pick
            // up rather than one that hides in the grass.
        }

        /// <summary>
        /// The inventory icon. A rendered one beside the model wins, because a Blender render
        /// at 128px with its own exposure beats anything shot in-process; failing that one is
        /// taken from the model at load.
        /// </summary>
        private static void Icon(ItemDrop.ItemData.SharedData shared)
        {
            var sprite = Icons.Load(Icons.For(TaumConfig.Model.Value), "the halter");

            if (sprite == null)
            {
                // Not fatal, and worth saying out loud: an item with no icon is invisible in
                // the inventory grid rather than obviously broken, which reads as "the
                // recipe did not work".
                TaumPlugin.LogOnce("No icon for the halter - it will wear the donor's.");
                return;
            }

            shared.m_icons = new[] { sprite };
        }

        // ------------------------------------------------------------------ the recipe

        /// <summary>
        /// Add the recipe to the ObjectDB that exists now, if it is not already there.
        ///
        /// Asked of the live database every time rather than remembered in a flag, for the
        /// same reason the prefab is: ObjectDB is rebuilt per world and a recipe list from
        /// the last one is a different object. A flag here does not lose anything permanent -
        /// it just means the halter cannot be crafted in the second world of a session,
        /// which reads as the mod having stopped working.
        /// </summary>
        internal static void KeepRecipe()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0) return;
            if (db.m_recipes == null) return;

            var prefab = db.GetItemPrefab(PrefabName);
            if (prefab == null) return;

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null) return;

            if (db.m_recipes.Any(r => r != null && r.m_item == drop)) return;

            var requirements = Requirements(db);
            if (requirements == null) return;

            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_" + PrefabName;
            recipe.m_item = drop;
            recipe.m_amount = 1;
            recipe.m_enabled = true;
            recipe.m_resources = requirements;
            recipe.m_minStationLevel = 1;
            recipe.m_craftingStation = Station(db);

            db.m_recipes.Add(recipe);
            TaumPlugin.Log.LogInfo("Registered the halter recipe ("
                + string.Join(", ", requirements.Select(r => r.m_resItem.name + " x" + r.m_amount).ToArray())
                + ").");
        }

        /// <summary>
        /// The cost, read from config against the live item database.
        ///
        /// Returns null rather than a short list when an ingredient cannot be resolved. A
        /// recipe missing one of its requirements is craftable for free, which is a worse
        /// failure than no recipe at all and one nobody would report as a bug.
        /// </summary>
        private static Piece.Requirement[] Requirements(ObjectDB db)
        {
            var wanted = TaumConfig.Table(TaumConfig.Cost.Value);
            if (wanted.Count == 0)
            {
                TaumPlugin.LogOnce("Item.Cost is empty, so the halter has no recipe.");
                return null;
            }

            var requirements = new List<Piece.Requirement>();

            foreach (var entry in wanted)
            {
                var item = db.GetItemPrefab(entry.Key);
                if (item == null)
                {
                    TaumPlugin.LogOnce("No item called '" + entry.Key + "' for the halter's "
                        + "cost - the recipe is left out rather than made cheaper.");
                    return null;
                }

                var drop = item.GetComponent<ItemDrop>();
                if (drop == null) return null;

                int amount;
                if (!int.TryParse(entry.Value, out amount) || amount < 1) amount = 1;

                requirements.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,
                    m_recover = true
                });
            }

            return requirements.ToArray();
        }

        /// <summary>
        /// The crafting station the recipe needs, or null for craftable anywhere. An unknown
        /// name is a warning and a null, never an exception.
        /// </summary>
        private static CraftingStation Station(ObjectDB db)
        {
            var name = TaumConfig.CraftingStation.Value;
            if (string.IsNullOrEmpty(name)) return null;

            var prefab = ZNetScene.instance == null ? null : ZNetScene.instance.GetPrefab(name);
            if (prefab == null)
            {
                TaumPlugin.LogOnce("No crafting station called '" + name
                    + "' - the halter is craftable anywhere.");
                return null;
            }

            var station = prefab.GetComponent<CraftingStation>();
            if (station == null)
            {
                TaumPlugin.LogOnce("'" + name + "' is not a crafting station - the halter is "
                    + "craftable anywhere.");
                return null;
            }

            return station;
        }

        /// <summary>Where the .obj and .png files sit: beside the DLL, always.</summary>
        private static string AssetDir
        {
            get { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
        }

        /// <summary>
        /// Whether an item is a halter. Compared on the shared name rather than the prefab,
        /// because what arrives at Tameable.UseItem is an ItemData out of an inventory and
        /// its prefab reference is not guaranteed to be the one this mod built.
        /// </summary>
        internal static bool Is(ItemDrop.ItemData item)
        {
            return item != null
                   && item.m_shared != null
                   && string.Equals(item.m_shared.m_name, DisplayName, StringComparison.Ordinal);
        }
    }
}
