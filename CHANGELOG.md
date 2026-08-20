# Changelog

Notable changes to Taum. Format follows [Keep a Changelog](https://keepachangelog.com), and
the mod uses [semantic versioning](https://semver.org).

## [0.1.0] - unreleased

First version. **Built, and not yet run in game** - everything below compiles and deploys,
and nothing here has been stood next to a boar.

### The halter

Craft one at a workbench for two leather scraps. Press Use with it in hand at a tamed adult
boar or hen and it goes on and the animal follows; hold Use to take it back. A full pack means
it lands on the ground rather than being destroyed, and an animal that dies wearing one leaves
it where it fell, the way a lox leaves its saddle.

How many animals you can lead is how many halters you are carrying. That is the whole limit
and there is no config entry for it, because a halter is on exactly one animal and getting it
back means walking to that animal - the cap is an object you can count rather than a number
somebody has to agree with.

### What made it small

Two facts out of the decompiled source, both of which would have been expensive to guess
wrong:

- **`Tameable.Command` does not check `m_commandable`.** The flag only decides whether petting
  toggles following. So a boar has always been able to follow a player and has simply never
  had anything to ask it - and this mod needs no AI of its own, no waypoint, and no patch on
  movement.
- **`Tameable.Interact` returns false the instant `hold` is true**, so hold-Use on a tamed
  animal is an unclaimed gesture. Shift-Use is not: it opens the rename box. Taking that would
  have cost a feature to add one.

The state lives in the animal's ZDO next to where vanilla keeps `s_haveSaddleHash`, so it
survives a relog, shows up for other players, and needs no file of its own.

### Four shapes, all shipped

`halter_a` straps, `halter_b` rope, `halter_c` a heavy bridle, `halter_d` a neck collar.
`Item.Model` picks one and swapping is a config line rather than a rebuild.

They ship as four because the question they answer cannot be settled from a render. A boar's
muzzle is about 16cm across where a noseband sits and a hen's whole skull is about 5cm, and
whether one model scaled per species survives that is something you find out standing in a pen.
`halter_d` is the hedge: a collar has no face piece to fit wrong.

Placement came off devkit rips of Boar and Hen rather than being eyeballed, which is also how
the bone names were found - **Boar spells it `Head` and Hen spells it `head`**, so the search
is a case-insensitive list rather than a name.

### Known limits

- **Nothing has been run in game.** The scales in `Creatures.Scales` are measured guesses and
  the offsets are all zero, so the first session is likely to be a round of nudging numbers.
- No portal message, no straggler catch-up, no whole-pen gesture. Each was considered and
  declined; the mod is meant to be narrower than the problem.
