using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Taum
{
    /// <summary>
    /// The halter as it is worn: a mesh hung off the animal's head bone.
    ///
    /// This is a plain child GameObject with a MeshRenderer and nothing else - no ZNetView,
    /// no collider, no component of its own beyond this marker. It is not a network object
    /// and must not become one: the fact that travels between machines is the ZDO bool, and
    /// every client builds its own copy of the visual from it. A ZNetView here would be a
    /// second object in the world for something that is a hat.
    ///
    /// Parenting to the bone rather than driving the transform per frame is what makes it
    /// move with the head for free, including while the animal is eating, sleeping or
    /// running. Unity's skinning updates the bone; a child of the bone comes along.
    /// </summary>
    internal sealed class WornHalter : MonoBehaviour
    {
        /// <summary>
        /// Built once and shared. A Mesh and a Material are assets, not instances - handing
        /// the same two to fifty animals costs one of each, and re-reading the .obj per
        /// animal would be a file read every time one walks into view.
        /// </summary>
        private static Mesh _mesh;
        private static Material[] _materials;
        private static bool _tried;

        internal static void Attach(Tameable tameable)
        {
            if (tameable == null) return;

            if (!Ready())
            {
                TaumPlugin.LogOnce("No halter model loaded, so nothing is drawn on the "
                    + "animal. The halter still works - it is the picture that is missing.");
                return;
            }

            var bone = Bone(tameable.transform);
            if (bone == null)
            {
                // Not fatal on purpose. A halter around an animal's middle looks wrong and
                // is fixable from the config; an exception here would take the whole
                // Tameable.Awake postfix down with it and stop every animal working.
                TaumPlugin.LogOnce("No head bone found on " + Utils.GetPrefabName(tameable.gameObject)
                    + " from Creatures.HeadBones - the halter hangs off its root instead.");
                bone = tameable.transform;
            }

            var go = new GameObject("TaumWornHalter");
            go.transform.SetParent(bone, false);
            go.transform.localPosition = Offset(tameable);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * Scale(tameable);

            go.AddComponent<MeshFilter>().sharedMesh = _mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = _materials;

            // Creature meshes cast and receive shadows, and a piece of tack that does
            // neither reads as a decal floating in front of the face.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            go.AddComponent<WornHalter>();
        }

        /// <summary>
        /// Load the shared mesh and materials once. Failure is remembered as well as
        /// success: a missing .obj should not mean a file check for every animal in a pen.
        /// </summary>
        private static bool Ready()
        {
            if (_tried) return _mesh != null;
            _tried = true;

            var file = TaumConfig.Model.Value;
            if (string.IsNullOrEmpty(file)) return false;

            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var model = ObjMesh.Load(Path.Combine(directory, file));
            if (model == null || model.Mesh == null) return false;

            _mesh = model.Mesh;
            _materials = Skins.Skin(model.Groups);
            return true;
        }

        /// <summary>
        /// First configured bone name that exists anywhere under the animal, matched without
        /// case. Boar spells it "Head" and Hen spells it "head" - which is not a detail worth
        /// discovering twice, and is why this is a list rather than a name.
        /// </summary>
        private static Transform Bone(Transform root)
        {
            var names = (TaumConfig.HeadBones.Value ?? "").Split(',');

            foreach (var wanted in names)
            {
                var name = wanted.Trim();
                if (name.Length == 0) continue;

                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Per-animal scale. A boar's muzzle is about three times a hen's whole skull, so one
        /// model at one size cannot serve both and the number belongs in config where it can
        /// be corrected against what is actually on screen.
        /// </summary>
        private static float Scale(Tameable tameable)
        {
            var table = TaumConfig.Table(TaumConfig.Scales.Value);
            string value;
            if (!table.TryGetValue(Utils.GetPrefabName(tameable.gameObject), out value)) return 1f;

            float scale;
            return float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out scale) && scale > 0f
                ? scale
                : 1f;
        }

        /// <summary>
        /// Per-animal nudge in the bone's own space, for when the model sits in a jaw.
        /// Parsed with the invariant culture: this machine is on a comma decimal separator,
        /// and "0.35" read as 35 would put a halter in the next valley.
        /// </summary>
        private static Vector3 Offset(Tameable tameable)
        {
            var table = TaumConfig.Table(TaumConfig.Offsets.Value, ';');
            string value;
            if (!table.TryGetValue(Utils.GetPrefabName(tameable.gameObject), out value))
                return Vector3.zero;

            var parts = value.Split(',');
            if (parts.Length != 3) return Vector3.zero;

            float x, y, z;
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            const System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Float;

            if (!float.TryParse(parts[0].Trim(), style, culture, out x)) return Vector3.zero;
            if (!float.TryParse(parts[1].Trim(), style, culture, out y)) return Vector3.zero;
            if (!float.TryParse(parts[2].Trim(), style, culture, out z)) return Vector3.zero;

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Drop the cached model when the world does. Materials are borrowed from vanilla
        /// prefabs, and those are torn down and rebuilt per world - holding one across a
        /// world change is a reference to a destroyed object, which Unity reports as a
        /// missing material and draws in magenta.
        /// </summary>
        internal static void Forget()
        {
            _mesh = null;
            _materials = null;
            _tried = false;
        }
    }
}
