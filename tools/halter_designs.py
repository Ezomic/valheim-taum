"""
Four halters, built and rendered on the animals that will wear them.

    blender --background --python tools/halter_designs.py

The halter is a prop worn on a creature's head bone, not a buildable piece, so the
rules that matter here are not quite the usual ones.

  * It is tiny. A boar's muzzle measures 16cm across and 21cm tall at the point a
    noseband sits, off the devkit rip of Boar. Straps modelled at a realistic leather
    thickness would be two pixels at eye distance and read as dirt on the model. Every
    band here is deliberately over-thick for that reason - vanilla does the same thing
    to the lox saddle's girth straps.
  * It is worn by two animals whose heads differ by a factor of three. A boar skull is
    ~16cm across the muzzle; a hen's is ~5cm and mostly comb. One canonical model
    scaled per species is the plan, and D exists to test whether that is a bad plan.
  * The neighbour is in frame because it has to be. A halter judged on its own is a
    picture of a piece of tack; the only question worth answering is whether it reads
    as tack on an animal, at the distance you stand from a pen.

Measured, not guessed. Every placement number below came out of
own-profile/BepInEx/rips/{Boar,Hen} - the boar mesh is authored in metres, the hen's
is authored at ~128x and scaled down by its rig, which is the same trap that will bite
the runtime offsets later.

The four are meant to disagree:

  A  strap halter   thin bands, many of them, buckles - the literal object
  B  rope halter    two fat cords and two knots - fewest parts, roundest
  C  heavy bridle   broad browband and iron cheek plates - most mass, most metal
  D  neck collar    no face piece at all - the shape that might survive a hen

If two of them share an outline there is only one design, which is why D is not a
variation on a halter.
"""

import bpy
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vhbuild import *  # noqa: F401,F403

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PREVIEWS = os.path.join(ROOT, "assets", "previews")
VARIANTS = os.path.join(ROOT, "assets", "variants")

RIPS = r"E:\Repositories\valheim\own-profile\BepInEx\rips"

# Leather and iron. The runtime borrows the lox saddle's testloxsaddle_m, which is a
# desaturated grey-tan rather than the saddle-brown you picture - so the preview uses
# that, not a warm brown, or every one of these would look better here than in game.
TINTS["hide"] = (0.34, 0.30, 0.25, 1.0)
TINTS["darkhide"] = (0.24, 0.21, 0.17, 1.0)
TINTS["beast"] = (0.30, 0.29, 0.27, 1.0)


# ------------------------------------------------------------------ canonical space
#
# Every halter is built around a muzzle of unit width: half-width 0.5, half-height
# 0.65 (the boar's own 16:21 ratio), origin at the centre of the noseband, +y toward
# the snout, +z up. Placing one on an animal is then a uniform scale by that animal's
# muzzle width in metres, which is exactly what the runtime will do with a config
# number per species.

MW, MH = 0.5, 0.65


def axis_x(obj):
    """Turn a torus to hang in the sagittal plane - a ring seen edge-on from the front."""
    obj.rotation_euler = (0.0, math.radians(90.0), 0.0)
    return obj


def band(radius_x, radius_z, thickness, y, mat, depth=0.6):
    """
    A strap running around the muzzle.

    One torus, not a ring of blocks. The band wraps something solid so only its outer
    wall is ever seen, and four boxes in a square would cost four times the geometry
    to show the same silhouette - the barrel-hoop lesson from Stoker, at 1/20 scale.

    Squashed along y afterwards because a torus is round cord and a leather strap is
    flat. The scale happens on the object before the join, so it bakes in.
    """
    obj = ring(radius_x, thickness, (0, y, 0), mat, major=16, minor=6, rot_x=90.0)
    obj.scale = (1.0, depth, radius_z / radius_x)
    return obj


def strap(start, end, width, thick, mat):
    """A box laid between two points, overlapping both by construction."""
    sx, sy, sz = start
    ex, ey, ez = end
    dy, dz = ey - sy, ez - sz
    length = math.sqrt(dy * dy + dz * dz)
    # R_x(theta) carries +y toward +z, so a strap whose far end rises wants a
    # negative angle when that far end is the -y one.
    pitch = -math.degrees(math.atan2(dz, dy))
    return box((width, length, thick),
               ((sx + ex) / 2.0, (sy + ey) / 2.0, (sz + ez) / 2.0),
               mat, rot_x=pitch, tilt=2.0)


# ------------------------------------------------------------------------- the four

def variant_a():
    """Strap halter. The literal object: noseband, cheeks, crown, throat, buckles."""
    band(MW + 0.06, MH + 0.06, 0.085, 0.0, "hide", depth=0.55)

    for side in (-1, 1):
        strap((side * (MW + 0.08), -0.05, 0.05),
              (side * (MW + 0.14), -1.45, 0.58), 0.15, 0.10, "hide")
        # Buckle where the cheek meets the noseband. Small, but it is the only thing
        # that says "made" rather than "grown" at this size.
        box((0.20, 0.20, 0.26), (side * (MW + 0.10), -0.08, 0.02), "iron", tilt=3.0)

    box((1.34, 0.26, 0.13), (0, -1.50, 0.62), "hide", tilt=3.0)      # crown
    box((1.26, 0.22, 0.12), (0, -1.28, -0.38), "darkhide", tilt=3.0)  # throat

    axis_x(ring(0.28, 0.075, (0, 0.02, -(MH + 0.30)), "iron", major=16, minor=6))
    return finish("HalterA")


def variant_b():
    """Rope halter. Two fat cords and two knots - fewest parts, roundest silhouette."""
    band(MW + 0.08, MH + 0.08, 0.15, 0.0, "hide", depth=0.9)

    # One loop over the poll and under the jaw, in the sagittal plane. This is what
    # makes a rope halter a rope halter: it is one continuous line, not five straps.
    loop = ring(0.92, 0.14, (0, -0.85, 0.10), "hide", major=18, minor=6, rot_x=90.0)
    loop.rotation_euler = (0.0, math.radians(90.0), 0.0)
    loop.scale = (1.0, 1.0, 0.85)

    for side in (-1, 1):
        orb(0.23, (side * (MW + 0.10), -0.20, 0.06), "darkhide", subdivisions=1)

    axis_x(ring(0.31, 0.095, (0, 0.04, -(MH + 0.34)), "iron", major=16, minor=6))
    return finish("HalterB")


def variant_c():
    """Heavy bridle. Broad browband and iron cheek plates - most mass, most metal."""
    band(MW + 0.07, MH + 0.07, 0.11, 0.0, "hide", depth=1.15)   # wide noseband

    for side in (-1, 1):
        strap((side * (MW + 0.09), -0.10, 0.04),
              (side * (MW + 0.16), -1.55, 0.55), 0.17, 0.11, "hide")
        plate = disc(0.27, 0.09, (side * (MW + 0.17), -0.80, 0.28), "iron",
                     sides=11, rot_x=90.0)
        plate.rotation_euler = (0.0, math.radians(90.0), 0.0)

    box((1.40, 0.20, 0.38), (0, -1.62, 0.50), "darkhide", tilt=2.0)  # browband
    box((1.30, 0.26, 0.16), (0, -1.66, 0.74), "hide", tilt=2.0)      # crown

    axis_x(ring(0.35, 0.105, (0, 0.06, -(MH + 0.36)), "iron", major=16, minor=6))
    return finish("HalterC")


def variant_d():
    """
    Neck collar. No face piece at all.

    Here to answer the hen. A hen's skull is 5cm across and most of that is comb, so
    anything strapped to its face is a dark smudge - a collar is the same object at
    both scales because a neck is a neck.
    """
    band(MW + 0.14, MH + 0.02, 0.22, 0.0, "hide", depth=1.6)
    band(MW + 0.17, MH + 0.05, 0.09, -0.30, "darkhide", depth=0.5)

    for side in (-1, 1):
        box((0.16, 0.30, 0.16), (side * (MW + 0.18), 0.0, 0.10), "iron", tilt=3.0)

    axis_x(ring(0.34, 0.10, (0, 0.0, -(MH + 0.30)), "iron", major=16, minor=6))
    return finish("HalterD")


# ------------------------------------------------------------------------- the stage

def grey(obj, tint="beast"):
    mat = material("beastcoat")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = TINTS[tint]
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


def load_rip(name, height=None):
    """
    Import a devkit rip.

    forward_axis Z / up_axis Y is the exact inverse of what vhbuild exports with, so
    a rip lands back in the space it was taken from: Unity (x, y, z) arrives as
    Blender (x, z, y).

    `height` normalises the mesh. The boar is authored in metres and needs none; the
    hen is authored at roughly 128x and scaled down by its rig, and the rip carries
    raw local vertices, so without this it imports as a fifty-metre chicken.
    """
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=os.path.join(RIPS, name, name + ".obj"),
                          forward_axis="Z", up_axis="Y")
    fresh = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]

    bpy.ops.object.select_all(action="DESELECT")
    for o in fresh:
        o.select_set(True)
    bpy.context.view_layer.objects.active = fresh[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name

    if height:
        zs = [(obj.matrix_world @ v.co).z for v in obj.data.vertices]
        obj.scale = (height / (max(zs) - min(zs)),) * 3
        bpy.ops.object.transform_apply(scale=True)

    zs = [v.co.z for v in obj.data.vertices]
    obj.location.z -= min(zs)
    bpy.ops.object.transform_apply(location=True)
    return grey(obj)


def wear(halter, at, scale, pitch=0.0, offset_x=0.0):
    """Copy the halter onto an animal. Uniform scale, exactly as the runtime will."""
    copy = halter.copy()
    copy.data = halter.data.copy()
    bpy.context.collection.objects.link(copy)
    copy.location = (at[0] + offset_x, at[1], at[2])
    copy.scale = (scale, scale, scale)
    copy.rotation_euler = (math.radians(pitch), 0.0, 0.0)
    return copy


def lights():
    """
    Sun 1.4, fill 0.35, world 0.28.

    vhbuild's stage_scene defaults to sun 3.2 and a world at 0.65, which puts a 0.30
    albedo somewhere near 0.72 in sRGB - every material lands on one bright value and
    no silhouette can be judged at all. Leather at 0.34 and iron at 0.19 have to stay
    separable or this whole exercise is four pictures of the same pale shape.
    """
    bpy.ops.mesh.primitive_plane_add(size=40.0, location=(0, 0, 0))
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("ground")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.19, 0.21, 0.16, 1)
    ground.data.materials.append(gm)

    bpy.ops.object.light_add(type="SUN", location=(3, -4, 6))
    sun = bpy.context.active_object
    sun.data.energy = 1.4
    sun.rotation_euler = (math.radians(52), 0, math.radians(200))

    bpy.ops.object.light_add(type="SUN", location=(-4, -3, 3))
    fill = bpy.context.active_object
    fill.data.energy = 0.35
    fill.rotation_euler = (math.radians(64), 0, math.radians(20))

    world = bpy.data.worlds.new("w")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.36, 0.43, 0.53, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.28


# ------------------------------------------------------------------------ placement
#
# Boar, from the rip and with the tusk submeshes excluded: mesh 1.76m long, snout tip
# at y 1.044, and at 15cm back along the muzzle the section is 0.164 wide by 0.215
# tall centred at z 0.515. The neck, 50cm back, is 0.31 wide centred near z 0.65.
#
# Hen, normalised to 0.40m tall: beak tip at y 0.182, skull ~0.05 across just behind
# it at z 0.353, neck ~0.10 across at y 0.09.

BOAR_FACE = ((0.0, 0.894, 0.515), 0.205, -8.0)
BOAR_NECK = ((0.0, 0.544, 0.650), 0.330, 0.0)
HEN_FACE = ((0.0, 0.152, 0.352), 0.072, -14.0)
HEN_NECK = ((0.0, 0.086, 0.300), 0.135, 0.0)

HEN_X = 1.15


def render_variant(key, build, collar=False):
    clear_scene()

    halter = build()
    tris = len(halter.data.polygons)
    print("  %s: %d faces" % (key, tris))

    export(halter, "halter_" + key.lower(), VARIANTS)

    # ---- close, three-quarter, halter alone
    lights()
    halter.location = (0, 0, 0.55)
    halter.scale = (0.42, 0.42, 0.42)
    camera((0.95, -1.25, 0.92), (0, -0.16, 0.52), lens=48)
    render(os.path.join(PREVIEWS, "halter_%s_detail.png" % key.lower()),
           width=760, height=680, bloom=False)

    # ---- eye height, worn, with the reference cube and both animals
    halter.location = (0, 0, -40)          # park the master out of shot

    boar = load_rip("Boar")
    hen = load_rip("Hen", height=0.40)
    hen.location.x = HEN_X
    bpy.ops.object.transform_apply(location=True)

    face, neck = (BOAR_NECK, HEN_NECK) if collar else (BOAR_FACE, HEN_FACE)
    wear(halter, face[0], face[1], face[2])
    wear(halter, neck[0], neck[1], neck[2], offset_x=HEN_X)

    reference_cube((-1.35, 0.15, 0.50))
    camera((1.95, -2.95, 1.70), (0.45, 0.55, 0.48), lens=42)
    render(os.path.join(PREVIEWS, "halter_%s.png" % key.lower()),
           width=1200, height=700, bloom=False)


def main():
    os.makedirs(PREVIEWS, exist_ok=True)
    os.makedirs(VARIANTS, exist_ok=True)

    for key, build, collar in (("A", variant_a, False),
                               ("B", variant_b, False),
                               ("C", variant_c, False),
                               ("D", variant_d, True)):
        render_variant(key, build, collar)

    print("\nRenders in %s" % PREVIEWS)


main()
