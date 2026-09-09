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
is already injected. `Dependencies.create` is still the only place naming a real
path (`%APPDATA%\Marbas\settings.conf`), and the parsing, the fallbacks and the
file itself all run against `MockFileSystem` in the tests — which two opaque
function ports would have left untestable.

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

**A panel setting is a directory, whatever the panel is standing in.** Since
P9.10 a panel says where it is with a `PanelLocation`, which can be a place
inside an archive, but `PanelSettings.Path` stays a `FileSystemPath`:
`AppState.settings` puts each panel through `PanelLocation.holding`, so a panel
inside `backup.zip` is written as the directory holding that archive. A session
is restored to a place that is certainly still there, and the file keeps the
one shape it has always had.

**Saving is an effect, so quitting stays a transition.** `QuitRequested` answers
with two effects — `SaveSettings(settings fs state)` and then `Quit` — which keeps
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
tested on both platforms while `HostEnvironment` keeps only the two
environment reads.

**The programs are session settings too, and the key spells the role.**
`editor.default` names the editor `F4` starts when nothing more specific
applies, and `program.<role>.<ext>` names one program per extension per role —
`program.editing.fs=…`, `program.opening.html=…`. A file that names no program
at all keeps the defaults; a key with a blank extension, a blank program or a
role that is neither `editing` nor `opening` is passed over the way every other
unreadable line is, so one mistyped association cannot cost the rest. The
associations come back out of the `key=value` map in key order, which is role
and then extension order, and every one of them is folded back through
`ProgramSettings.remember`, so the file, the popup and the table itself agree
however the lines were written.

**Two keys write into that table now.** `F12` edits it as a table, and
`Alt+R` adds one row to it when the box that remembers is ticked — the
same `ProgramSettings.associate ForOpening`, reached from a different question.
Both land in `state.Programs` and are written on quit like every other setting,
so "open with this from now on" survives a restart without a save of its own.

**A line written before the role existed is one for editing.** Versions before
P10.1 wrote `editor.extension.<ext>=…`, which is still read — as a `ForEditing`
row, since editing was all the table could mean then. That keeps a settings
file written by an older Marbas working, and because the new keys carry the
role there is nothing to migrate: the first save rewrites the file in the new
spelling and the old prefix simply stops appearing.

**An empty default editor still means what it always meant.** `EditorCommand`
was written around one `$EDITOR` and it still is: `EditorProcess.run` resolves
an editor by asking the state first (`ProgramSettings.programFor ForEditing`,
which tries the extension and falls back to the default), then `$EDITOR`, then
`notepad` on Windows and `vi` elsewhere. Leaving the field blank in the settings
popup therefore restores exactly the behaviour of the version before it existed.

**The file describes tabs now, and a numbered block is one tab.** P11.2 turned
`Settings` from one pair of panels into a `Zipper<TabSettings>`: the keys are
`tab.1.left.path`, `tab.1.left.sort`, `tab.1.left.direction`,
`tab.1.left.hidden`, `tab.1.left.cursor`, the same six for the right, one
`tab.1.active` naming the side the tab was left on, and one `tabs.current`
saying which tab was in front, 1-based like the block numbers. The zipper is
what makes "the file describes at least one tab" true by construction, so
`parse` has no empty case to answer and a `tabs.current` outside the range
simply lands on the nearest tab.

**A file with no `tab.` key is a file from before tabs, and is read as one.**
The old `left.`/`right.` keys are still understood — as a single tab, active
side `Left` — exactly the way a role-less `editor.extension.` line is read as an
editing row. The first save rewrites the file in the new spelling, so there is
nothing to migrate and nothing to keep in step: the old keys are read and never
written again. Tab numbers need not start at one or run without gaps either; the
blocks are taken in ascending order, which is what makes a hand-edited file
survive having a block cut out of it.

**`PanelSide` had to move down for this.** The file names the active panel, and
`FileSystem` cannot reach `State`, so `PanelSide` left `AppState` for
`Marbas.Domain` — a namespace for the words the whole application speaks. The
rest of them followed in one pass rather than one type at a time; see
[DOMAIN-TYPES.md](DOMAIN-TYPES.md), *The vocabulary sits below every layer that
speaks it*.

**The cursor is a name, and only a name.** `tab.1.left.cursor=notes.txt` is
resolved on load the way `FocusTarget.ByName` resolves any focus request: an
entry that is no longer there costs the position and nothing else, and the
cursor rests at the top. A cursor sitting on `..` writes no key at all —
restoring the way out is restoring the top of the list, and a line saying so
would be noise in every fresh panel. Marks are still not written: a mark belongs
to an operation, and an operation does not survive a restart.

**A tab nobody has opened keeps the cursor the file gave it.** Only the tab in
front is read at startup, so the panels of the others have no entries and
therefore no cursor entry to write back. `PanelState.Remembered` carries the
name the file supplied until that panel is first read — `loadPanel` and the
reload behind a tab switch focus it, `loadLocationFocusing` clears it once the
panel has real entries, and `AppState.settings` falls back to it when there is
no cursor entry yet. Without it the first save after a restart would wipe the
cursors of every tab except the one on screen.

**The session is written on every tab command, not only on quit.** `TabOpened`,
`TabClosed`, `TabSwitched` and `TabCycled` each answer with `SaveSettings`
beside their new state, and `Shift+F9` is that effect with no state change at
all — a save that succeeded says nothing, because there is nothing to say. The
old complaint that a crash loses the session is therefore smaller than it was:
anything that touches the tabs has already written the file.
