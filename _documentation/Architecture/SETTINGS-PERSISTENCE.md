# Settings persistence

**The session is a value, and the state is built from it.** `Settings` holds
exactly what survives a restart — path, sort order and hidden toggle, per panel,
and the name the last directory made was given — in the panel's own domain
types, so nothing has to be converted twice.
`AppState.fromSettings` builds a state from one and `AppState.settings` reads one
back, which makes `AppState.create path` simply
`fromSettings (Settings.forPath path)`. The first-start defaults therefore live
in one place, `Settings`, rather than in the state constructor.

**Settings go through `IFileSystem`, not through a port of their own.** The plan
called for a load port and a save port; what it got is one `SettingsPath` on
`Dependencies` plus `Settings.load` and `Settings.save` over the file system that
is already injected. `Program.fs` is still the only place naming a real path
(`%APPDATA%\Marbas\settings.conf`), and the parsing, the fallbacks and the file
itself all run against `MockFileSystem` in the tests — which two opaque function
ports would have left untestable.

**The config file is `key=value` lines, and every value is optional.**
`left.path`, `left.sort`, `left.direction`, `left.hidden`, and the same four for
`right`. `Settings.parse` takes the defaults and folds onto them whatever it
understood: an unknown key, a line with no `=`, a blank path and a word that
names no sort key are all simply not read. A truncated or hand-edited file
therefore degrades one setting at a time instead of being rejected whole. Keys
and value words are matched lowercased; a path keeps the case it was written in.

**The name of the last directory made is a session setting, not a panel one.**
It is written as `mkdir.name` — one key, outside the `left.`/`right.` pairs,
because the suggestion `F7` opens with does not belong to a side. `AppState`
carries it as an `EntryName option` that `makeDirectory` sets only when the
directory was actually created, so a refused name leaves the previous
suggestion standing, and `PromptForName` hands it to the shell. It reaches the
file the way everything else does, when `QuitRequested` saves the session.

**A stored path is checked before it is used.** `Settings.load` ends by asking
the file system whether each stored path is still a directory and falls back to
the default start path when it is not, so a removed drive cannot greet a fresh
start with an error dialog. Nothing else needs checking: a sort order and a
hidden toggle cannot go stale.

**Saving is an effect, so quitting stays a transition.** `QuitRequested` answers
with two effects — `SaveSettings(settings state)` and then `Quit` — which keeps
*what* is written a pure function of the state, asserted in `UpdateTests`, and
keeps the writing itself in `Shell.interpret` with everything else that touches
the world. A write that fails is swallowed: by then there is no screen left to
report it on.

**`Ctrl+Q` joined the key table, so quitting passes through the state.** It used
to be only an application-level `Command.Quit` binding, which bypasses `Update`
entirely — `F10` would have saved the session and `Ctrl+Q` would have dropped it.
`KeyMap` now owns `quitKey` (off the bar, since `F10 Quit` already carries the
label) and `MainWindow.configureQuitKey` binds the same key application-wide, so
the panel handler wins wherever a panel has focus and the old binding stays as
the fallback everywhere else. `Esc` is bound alongside it, because that is what
closes a popup — [UI-AND-INPUT.md](UI-AND-INPUT.md) says why, and why the panel
has to swallow it.

**`SpecialFolder.ApplicationData` is empty on a Linux machine without
`~/.config`.** The old `Program.fs` combined that empty string with `Marbas` and
`settings.conf`, which yields a *relative* path — so the session was written to
`./Marbas/settings.conf`, next to wherever the app was started, and a start from
another directory found no session at all. Running the app under WSL is what
caught it; no test could have, because the value comes from the environment.
`Settings.location` now holds the rule — application data if the platform names
one, `$HOME/.config` otherwise — and it takes both values as arguments, so it is
tested on both platforms while `Program.fs` keeps only the two environment
reads.
