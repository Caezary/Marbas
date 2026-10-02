# Phase 20 — The user menu

Goal: `F2` opens a menu of programs and commands the user has defined, each
reached by a letter of its own and each able to be handed the files under the
cursors or the marked ones. The menu is edited where it is shown. The phase
opens by making `F4` start the editor without waiting for it, so that nothing
Marbas starts holds the panels up. The keys were measured in P17.2.

Five decisions run through the whole phase.

**Nothing Marbas starts is waited for.** A program from the user menu, like a
file run with `Enter`, is started and left to itself; from P20.1 the editor of
`F4` is too. A console program gets a console window of its own instead of
sharing the terminal the panels are drawn in, and what it changes on the disk
reaches the panels through the directory watch of phase 12, not through a
re-read when it ends.

**The user menu has a file of its own.** `user-menu.conf` sits beside
`favorites.conf` and is written the same way: every change made in the popup is
an operation on the file, read, applied and written at once, so two copies of
Marbas do not erase each other's entries and a crash loses nothing. The parser
forgives as `Settings.parse` does — a line it cannot read costs that one entry.

**An entry is a label, a hotkey and a command line.** The hotkey is one letter
or digit, unique within the menu; the command line names a program or is a shell
command. How it runs is a choice of three: started directly, through the shell
with a window that closes when it ends (`cmd /c`), or through the shell with a
window that stays open (`cmd /k`). Running as administrator is a box beside it.
A separator is an entry with no command, which the cursor steps over.

**The notation names the panels by side.** In the command line `%n`, `%p`, `%s`
and `%d` stand for the active panel: the name under the cursor, its full path,
the marked entries — the entry under the cursor when nothing is marked — and the
panel's directory. `%Ln`, `%Lp`, `%Ls` and `%Ld` say the same of the left panel,
`%Rn`, `%Rp`, `%Rs` and `%Rd` of the right one. `%f` is the path of a temporary
file listing the marked entries one per line, for a list too long for a command
line, and `%%` is a percent sign. Every path is quoted when it is put in, `%s`
puts in each entry as an argument of its own, and the program is started once
with all of them. A placeholder that names an entry inside an archive refuses
the run, the way `Enter` refuses to run a file there.

**A run is remembered.** A command started from the menu is a row on the
*History* tab of `Alt+F12`, showing the label and the command line as it was
expanded, and `Enter` there runs it again exactly as it ran.

- **P20.1 `F4` no longer waits** — the editor is started detached, and on
  Windows a console editor gets a window of its own; the panel is no longer
  re-read when the editor closes, because the directory watch already follows
  the file. On Unix, where a console editor such as `vi` can only share the
  terminal, `F4` keeps waiting, and [Rough edges](../ROUGH-EDGES.md) says so in
  place of the entry it has now. *Done when:* the start request `F4` raises is
  proved to ask for no waiting on Windows and for waiting on Unix, and editing a
  file by hand shows the change in the panel after it is saved.
- **P20.2 The entries and their file** — the types — an entry, its hotkey, its
  way of running and its elevation, a separator — and `user-menu.conf` with its
  parser, its fallbacks and the operations add, change, remove and move, applied
  to the file as the history applies its own. *Done when:* the operations, a
  duplicate hotkey refused, the parser's fallbacks and a change applied on top
  of a file another copy wrote are proved on `MockFileSystem`, and the
  application still starts without the file.
- **P20.3 The notation** — expanding a command line against the two panels is a
  pure function from the line and the panels to the program and its arguments,
  or to the reason it refuses. *Done when:* every placeholder is proved on each
  side, with nothing marked and with several entries marked, with names that
  hold spaces and quotes, in a panel of search results, inside an archive, and
  with `%` followed by a letter that means nothing, which is kept as it was
  typed.
- **P20.4 The menu and its hotkeys** — `F2`, labelled `Menu` on the bar, opens a
  popup listing each entry as its hotkey and its label, separators drawn as
  lines. A letter runs its entry at once and closes the popup; `↑` / `↓` and
  `Enter` do the same by choosing. The run is an effect `Shell` hands to the
  launch adapter, which starts the program directly or through the shell as the
  entry says, raises it to administrator through the verb `Alt+Shift+R` uses,
  writes the list file of `%f` into the session's working directory, and
  remembers the run on the *History* tab. *Done when:* the popup is rendered and
  driven through a real `Application`, a letter and `Enter` are proved to raise
  the same run, and each way of running is tried by hand.
- **P20.5 Editing the menu** — in the same popup `Ins` adds an entry after the
  one under the cursor through a dialog holding the label, the hotkey, the
  command line, the way of running and the administrator box; `F4` changes the
  entry under the cursor through the same dialog; `Del` removes it after asking;
  `Ctrl+↑` / `Ctrl+↓` move it; and the dialog can add a separator instead of an
  entry. Each change is written at once. Inside the popup a bare letter runs its
  entry, so the editing keys are the ones P17.2 found reaching a dialog with a
  list. *Done when:* each change is proved to reach the file and the popup, a
  duplicate hotkey is refused in the dialog, and a menu edited and closed is the
  same menu after a restart.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md), the running of programs in
[State and effects](../Architecture/STATE-AND-EFFECTS.md), the popup in
[UI and input](../Architecture/UI-AND-INPUT.md) and the file in
[Settings persistence](../Architecture/SETTINGS-PERSISTENCE.md) for what it
adds.

## Left for a later phase

- **Submenus** — an entry that opens a menu of its own, with hotkeys per level,
  stored as a tree in `user-menu.conf` and edited by moving entries between
  levels.
