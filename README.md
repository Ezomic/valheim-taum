# Taum

Lead your tamed boar and hens on a halter. They follow; nothing else changes.

Moving animals in Valheim is the one husbandry job the game never gave you a tool for. A pen
in the wrong place stays in the wrong place, and the alternatives players actually use are
worse than the problem: fence a corridor across half a meadow, or kill the herd and start
again somewhere better with two tames. Neither is a decision about farming. Both are a
decision about the absence of a lead rein.

So this is a lead rein, and deliberately nothing more. Put a halter on an animal that already
trusts you and it walks with you until you take it off. It does not run to catch up, it does
not teleport, it does not follow through a portal, and it will not do anything at all for an
animal you have not tamed. **The mod is narrower than the problem it solves**, on purpose: a
halter that solved every case would be a mod about moving animals instead of a mod about
walking with one.

The name is Old Norse *taumr*, a lead-rein - Icelandic *taumur*, Norwegian *tømme*. English
*team* comes down from the same word, which is the mod in one etymology: a team is what you
get when you put animals on leads and they walk together.

## Using it

Craft a halter at a workbench for two leather scraps. Hold it in your hotbar and press Use
while looking at a tamed adult boar or hen. The halter goes on and the animal follows you.

**Hold Use on a haltered animal to take the halter back.** It stays where it stands and the
halter returns to your pack. If your pack is full it lands on the ground rather than being
destroyed.

An animal wearing a halter says so in its hover text, and the prompt names the key you have
actually bound rather than insisting on E.

### What it refuses, and why

| It will not | Because |
| --- | --- |
| Halter an untamed animal | The halter is a courtesy between you and something that already trusts you, not a capture tool |
| Halter a hungry one | Taming asked you to feed it first; leading asks the same |
| Halter a frightened one | Vanilla will not tame an alerted animal either, and one fleeing a wolf is not standing to be handled |
| Halter a young one | Piglets and chicks stay where they are and grow up there. This is the line that keeps it from becoming a pied-piper mod |
| Halter a lox or an asksvin | You can saddle both. If you can ride it, transport is already solved |
| Halter a wolf | Already commandable. A halter there would be jewellery |

**How many animals you can lead is how many halters you are carrying.** There is no number in
the config for it, because there does not need to be one: each halter is on exactly one
animal, and getting it back means walking up to that animal and taking it off. The limit is a
physical object you can count in your inventory.

## What it rides rather than reimplements

Everything here is a seam the game already uses for the lox saddle, and the mod is small
because of it.

- **`Tameable.UseItem`** is where a saddle goes on. It returns false for any item that is not
  this animal's saddle, so a halter is handled exactly where an unhandled item lands, and any
  other mod that claims the press first still wins.
- **`Tameable.Command`** is the follow toggle. Reading the decompiled source turned up the
  fact this mod is built on: **`Command` does not check `m_commandable`**. That flag only
  decides whether *petting* toggles following, so a boar has always been able to follow you
  and has never had anything to ask it.
- **The ZDO** carries "this one is wearing a halter", the same way `s_haveSaddleHash` carries
  the saddle. That is what makes it survive a relog, show up for other players, and cost no
  save file of its own.
- **`Tameable.Interact` returns false the moment `hold` is true**, so hold-Use on a tamed
  animal is an unclaimed gesture. Shift-Use is not - it opens the rename box - and taking that
  would have cost a feature to add one.

Following, ownership, persistence and unsummon behaviour are all vanilla's. None of it is
reimplemented here, which is the reason this should survive a game update that a custom AI
would not.

## The halter itself

Four shapes ship, and which one is worn is a line in the config rather than a rebuild.

| | |
| --- | --- |
| `halter_a` | straps and buckles - the literal object |
| `halter_b` | two fat cords and two knots, fewest parts |
| `halter_c` | a broad browband with iron cheek plates |
| `halter_d` | a neck collar with no face piece at all |

They are four different objects rather than four skins of one, because a rope halter and an
iron bridle disagree about what kind of husbandry this is. `halter_d` also exists to answer a
question: a boar's muzzle is about 16cm across where a noseband sits and a hen's whole skull is
about 5cm, so one model scaled per species may simply not work on a bird. A collar is the shape
that would survive if it does not.

Every band is deliberately thicker than leather really is. At the distance you stand from a
pen, a realistic strap is two pixels and reads as dirt on the model - vanilla does the same
thing to the lox saddle's girth straps.

The surfaces are borrowed rather than painted: the leather comes off the Lox, whose saddle
child wears `testloxsaddle_m`, which is the one material in the game that was painted as tack
rather than as a crate. So it matches by construction and survives a game update.

## Installing

Needs BepInEx. Nothing else. Through a mod manager it is one install. By hand, put `Taum.dll`
and the `.obj` and `.png` files beside it in `BepInEx/plugins/Taum/`.

Then start the game once and quit. That first run writes the config file. It does not exist
before the mod has loaded, which is the usual reason people think it is broken.

## Settings

The file is `BepInEx/config/ezomic.valheim.taum.cfg`. Every setting has a comment above it, so
the file explains itself. The ones worth knowing about:

- `Item.Model` picks which of the four halters is worn and shown.
- `Creatures.Animals` is the list that accepts one. Adding a prefab name here is how a modded
  animal joins; it does not need this mod to know about it.
- `Creatures.Scales` and `Creatures.Offsets` place the model per species, which is where to go
  when a halter sits in a jaw.

Note that changing a default in a new version does nothing on a machine that has already run
the mod. BepInEx writes every entry on first run and the saved value wins.

## Multiplayer

**Everyone needs it.** The halter is a registered item prefab, and a client that cannot
resolve a prefab name does not fail loudly - ZNetScene discards the ZDO as junk, so a halter
lying on the ground would quietly cease to exist for anyone without the mod.

If [Core](https://github.com/Ezomic/valheim-core) is installed, this registers with its version
gate and the host's settings apply to everyone connected to it, in memory only - your own
config file is never written to and comes back the moment you disconnect. Without Core the mod
still runs; what is lost is the enforcement.

## Reporting bugs

[Discord](https://discord.gg/hJzAVaZ5wb), or an issue on
[the repo](https://github.com/Ezomic/valheim-taum). Either way, attach
`BepInEx\LogOutput.log` - it names every mod and version that was loaded, which is most of the
answer before anyone has read a line of it.

## Licence

MIT. See `LICENSE`.
