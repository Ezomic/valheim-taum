# Taum

Alt+E on a tamed boar or hen and it follows you. Alt+E again and it stays. It is the same
follow/stay command wolves and lox already have, opened up for the animals that never got it.

## Features

- Alt+E toggles follow/stay on any tamed creature Valheim does not let you command. In vanilla
  that is boars and hens.
- The animal's hover text gains a `[Alt + E] Follow / stay` line, using whichever key you have
  bound to Use and whichever modifier is set in the config.
- E still pets and Shift+E still renames. All three gestures keep their own key.
- Follow state is stored by the game on the animal, so it survives a relog and other players
  see it.
- No new items, recipes or prefabs. One DLL, no assets, no asset bundle.

## How it works

Valheim's follow/stay command already exists for every tamed creature. It is gated behind a
flag (`m_commandable`) that boars and hens do not have set, so there has simply never been a
way to ask them. Taum calls the game's own `Tameable.Command` on those animals when you press
Use with the modifier held. The follow AI, the "follows you" and "stays" messages and the
ownership handling on a server are all vanilla's.

It does not flip `m_commandable` itself. That flag also makes petting command the animal, the
way it does on a wolf, and petting your way down a row of boars would then march the whole pen
after you.

A modded tamed creature that is not commandable gets the gesture for free, with no patch or
config entry needed.

The first version of this mod was a crafted halter item with a lead. That design was cut before
release and lives on the `halter` branch.

## Installation

Requires [BepInEx 5.4.2350](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
BepInEx 5 only; this is not compatible with BepInEx 6.

Through a mod manager, install [Taum](https://thunderstore.io/c/valheim/p/Ezomic/Taum/) and you
are done. By hand, put `Taum.dll` in `BepInEx/plugins/Taum/`.

Start the game once and quit after installing. That first run writes the config file, which
does not exist before the mod has loaded.

[Longhouse Core](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse_Core/) is an optional
soft dependency. With it installed, Taum joins the version check and the host's config values
apply to clients. Without it, Taum runs on its own and only the version check is lost.

## Configuration

The file is `BepInEx/config/ezomic.valheim.taum.cfg`, section `[Taum]`.

| Setting | Default | Effect |
| --- | --- | --- |
| `Enabled` | `true` | `false` leaves the plugin loaded and changing nothing: no Alt+E, no hover line. |
| `Verbose` | `false` | Extra logging to `BepInEx/LogOutput.log`. In 1.0.0 that is one line confirming the patches are live when you spawn. Its comment in the cfg still describes the cut halter item; ignore it. |
| `FollowKey` | `LeftAlt` | The modifier held with Use to toggle follow/stay. |

`FollowKey` takes one Unity `KeyCode` name and matches it exactly, so `LeftAlt` is the left key
only. Set `RightAlt` if that is the one you use. Keys are read through the legacy input path,
so use a keyboard key; mouse and gamepad buttons are not reliable here.

BepInEx writes every setting to disk on first run and the saved value beats a new default in a
later version. If a setting looks like it is being ignored, read the cfg before anything else,
and make sure you are editing the profile you actually launch.

## Multiplayer

The command itself is vanilla's, including the ownership handling, so following works on a
dedicated server. Each player who wants the gesture installs Taum; an animal set to follow by
one player behaves the same for everyone.

Taum registers with Longhouse Core as required on both ends. If a server runs Core and Taum,
clients without Taum, or on a different version or build, are rejected when they connect. On a
server without Core, mixed installs are fine: players without Taum just cannot issue the
command.

With Core present, the host's `Enabled` and `Verbose` values are applied on connected clients in
memory, without touching their config files. `FollowKey` is a key and is never taken over by the
host.

## Compatibility

Taum patches `Tameable.Interact` (prefix) and `Tameable.GetHoverText` (postfix), and nothing
else. Conflicts to expect:

- A mod that makes farm animals commandable outright. Taum only acts on creatures the game
  marks non-commandable, so on those animals it becomes inert and the other mod's behaviour
  stands.
- A mod that binds the same modifier plus Use on a tamed creature. Taum's prefix takes the
  interaction and returns, so whichever patch runs first wins.

Wolves, lox and anything else already commandable are untouched and keep their vanilla gesture.

## Troubleshooting

**No hover line on the animal.** Either the mod is not loaded or `Enabled` is `false`. Check
`BepInEx/LogOutput.log` for `Taum 1.0.0 by Robbin Thijssen - ready.`

**Hover line is there, Alt+E does nothing.** The animal has to be tamed, and the modifier has to
be held when you press Use rather than after. If you changed `FollowKey`, check the cfg in the
profile you launched.

**It pets instead.** You pressed Use without the modifier down. If it opened the rename box, you
held Shift, which is vanilla's own gesture on a tamed creature.

## Bug reports

Report in the [Discord](https://discord.gg/hJzAVaZ5wb) or on the
[issue tracker](https://github.com/Ezomic/valheim-taum/issues). Useful to attach:

- `BepInEx/LogOutput.log`
- `BepInEx/config/ezomic.valheim.taum.cfg`
- Whether you were on a server or in single player
- Which animal it was, and whether it was tamed by you or by someone else
- `AppData/LocalLow/IronGate/Valheim/Player.log` if a vanilla mechanic broke, since exceptions
  thrown mid-frame land there and not in the BepInEx log

## Bugs and ideas

Both go to the site. [longhouse.thijssensoftware.nl/bugs](https://longhouse.thijssensoftware.nl/bugs)
is for anything broken, and [longhouse.thijssensoftware.nl/ideas](https://longhouse.thijssensoftware.nl/ideas)
is for what a mod should do next. You can vote on other people's ideas there as well.

Signing in takes a Steam or Discord account. I work from that list, so the votes decide what
I pick up next.

## Discord

The [Discord](https://discord.gg/hJzAVaZ5wb) is used for mod information, updates, support, bug
reports and compatibility questions.

## Server

There is also a small EU server running the pack if you want somewhere to play. Details are in
the Discord.

## Building

Open `Taum.csproj` and build. It targets net462, references the game's managed DLLs and
BepInEx's directly by path, and needs no NuGet restore. The build deploys to `testprofile/`
unless `ProfileDir` is overridden.

## Part of Longhouse

Taum is included in the [Longhouse](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse/)
modpack, which pins an exact version of it. It behaves the same installed on its own.

## Credits

By Robbin Thijssen (Thijssen Software). Licensed MIT, see [LICENSE](LICENSE). Version history is
in [CHANGELOG.md](CHANGELOG.md).
