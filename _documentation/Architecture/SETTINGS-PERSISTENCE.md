# Settings persistence

**The session is a value, and the state is built from it.** `Settings` holds
exactly what survives a restart — path, sort order and hidden toggle, per panel,
and the name the last directory made was given — in the panel's own domain
types, so nothing has to be converted twice.
`AppState.restore` builds a state from one and `AppState.settings` reads one
back, which makes `AppState.create path` simply
`restore (Settings.forPath path)`. The first-start defaults therefore live
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

**The name of the last directory made moved to the history.** Until phase 16
it was written as `mkdir.name`. Since P16.9 the suggestion `F7` opens with is
the newest entry of its input list in `history.conf`, so the settings file no
longer writes the key; a file that still carries it is read like one with any
other unknown key, and the line is passed over.

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
the world. `Settings.save` answers `Result<unit, SettingsError>` — the one place
that turns a write's exception into a value is `SettingsError.mapException` —
and `Shell` reports the failure through `SettingsErrorMessage` rather than
passing it over. The dialog arrives before `Quit` runs, because `QuitRequested`
raises `SaveSettings` first and the effects are interpreted in order, so a
session that could not be written says so instead of looking saved.

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
was written around one `$EDITOR` and it still is: `EditorProcess.describe`
resolves an editor by asking the state first (`ProgramSettings.programFor ForEditing`,
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
the layer that reads it — `FileSystem` then, `Profile` since P23.5 — cannot
reach `State`, so `PanelSide` left `AppState` for
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

**The favourites have a file of their own, written the way the history is.**
`favorites.conf` sits beside `settings.conf` and `history.conf` —
`Favorites.beside` takes the settings path, as `History.beside` does — and
`Dependencies` carries it as `FavoritesPath`. The settings file is written only
on quit and on the tab commands, so a favourite kept there would be lost by a
crash and erased by a second copy of Marbas quitting later; a favourite is
therefore an operation on its file. `Favorites.change` is `load`, `apply`,
write, and answers what it wrote, so a slot another copy filled since is kept
beside the one this copy fills, and a file that cannot be read ends the change
before anything is written. `FavoritesError` and `FavoritesErrorMessage`, titled
*Favourites not saved*, are the history's pair under another name, and every
failure is reported, since a favourite is only ever written when asked for.
Nothing reads the file at startup: going to a favourite reads it when the key is
pressed, so a missing file is ten empty slots and the application starts as it
did. The ten slots are global, shared by every tab and both panels.

```
favorite.1=C:\work
favorite.7=C:\backup.zip
favorite.7.inside=docs/2024
```

A slot is written as its digit and its place, an archive's level as an `inside`
part, slots in the order of their digits and empty ones not at all. A line whose
key names no slot from `0` to `9`, an empty value, and a slot whose `inside`
cannot be read — one that climbs — are passed over, each costing that slot
alone. A place whose path breaks a line is not kept, as the history does not
remember one.

**A slot is filled or emptied, and both are changes of the file.** `FavoriteChange`
is `Keep slot place` or `EmptySlot slot`; the second drops the slot from the
map, so it is not written at all, and emptying a slot that holds nothing
changes nothing. The favourites popup hands both to `Shell` through its
`Change` function, which writes them at once, so a slot emptied there is empty
the next time Marbas starts.

**The user menu has a file of its own, written the way the favourites are.**
`user-menu.conf` sits beside `favorites.conf`, and `UserMenu.beside` takes the
settings path as `Favorites.beside` does. Every change is an operation on the
file: `UserMenu.change` is `load`, `apply`, write, and answers what it wrote,
so an entry another copy added since is kept beside the one this copy adds, a
file that cannot be read ends the change before anything is written, and a
change the menu refuses writes nothing either. `UserMenuError` is
`UserMenuAccessDenied` and `UserMenuFailed`, the favourites' pair, and
`MenuRefused` of a `MenuRefusal` — `HotkeyTaken` when an entry would share a
hotkey with another, `CommandGone` when the entry being changed was removed by
another copy. Nothing reads the file at startup, and a missing file loads as
an empty menu, so the application starts as it did without one. Since P20.4
`Dependencies` carries the path as `UserMenuPath`, and `F2` reads the file
afresh each time it is pressed, so an entry another copy added is in the menu
the next time it opens; a file that cannot be read is reported under *User
menu* by `UserMenuErrorMessage`, which also words the two refusals for the
editing of P20.5. Since P20.5 the popup hands each change it makes to
`Shell`'s `changeUserMenu`, which is `UserMenu.change` on that path: the file
is read, the change applied and the file written before the popup redraws, so
a menu edited and closed is the same menu after a restart. The popup refuses a
hotkey it can see is taken before it asks; `HotkeyTaken` and `CommandGone`
from the file are what is left for a second copy that changed the menu in the
meantime, and they reach the user as a dialog over the popup.

```
entry.1=command
entry.1.hotkey=E
entry.1.label=Edit
entry.1.command=vim %p
entry.1.run=direct
entry.1.elevation=user
entry.2=separator
```

An entry is written under its number in the order of the menu: its kind as the
value, and for a command the hotkey, the label, the command line, the way of
running (`direct`, `shell-close`, `shell-keep`) and the elevation as parts. The
parser forgives as `Settings.parse` does. A way of running or an elevation that
is missing or cannot be read falls back to a direct start as the user, the
safer reading of either; an entry of a kind not known, or a command without a
hotkey, a label or a command line it can read, is passed over and costs that
entry alone; of two commands holding one hotkey the first is kept. Entries are
read in the order of their numbers, so a file edited by hand may leave gaps.

**A change is one of four, and each is named the way the popup will ask for
it.** `MenuChange` is `AddItem(item, after)` — after the entry named, at the
top when nothing is named, at the end when the entry named is gone —
`ChangeCommand(hotkey, command)`, `RemoveItem` and `MoveItem(reference,
Earlier | Later)`. Removing or moving something already gone changes nothing,
and moving the first entry up or the last down leaves it where it is; adding a
command whose hotkey is taken and changing one that is gone are the two
refusals, since each would otherwise lose what the user typed without saying
so.
