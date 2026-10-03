# Phase 17 — The marks, the panel and the favourites

Goal: marking reaches further than one entry or one pattern at a time, the panel
gets the keys a classic two-panel manager has for going up and for reading its
sizes at a glance, and ten directories can be kept under a digit and reached in
one keystroke. The phase opens by mending the marks that disappear from the
screen and by measuring every key the next four phases bind, so that a key a
terminal swallows is found before a task is built on it.

Five decisions run through the whole phase.

**Every marking command works on the one selection.** `PanelState.Selected`
stays the single set per panel; marking by the entry under the cursor, marking
all files and inverting are transitions that add to it or take from it, exactly
as `Insert` and the `+` / `-` patterns already do, so every marking key composes
with every other. `..` is never marked, and a command that would take its name
or its extension from `..` does nothing.

**A grey key that cannot be told apart falls back to a letter with `Alt`.** The
classic spelling is the grey `+`, `-` and `*` of the numeric keypad with a
modifier. The plan first meant to fall back on the same modifier with the
main-keyboard key, and to bind a letter with `Alt` only where a terminal claims
the combination itself, as `Alt+W` is beside `Ctrl+PgDn`. P17.2 measured both
and found neither: no terminal tells a grey key from its twin, `Ctrl` and `Shift`
on either arrive as a different key or as nothing, and `Alt` with punctuation is
swallowed by Terminal.Gui —
[Cross-platform](../Architecture/CROSS-PLATFORM.md), *Keyboard and terminal*,
has the measurement. So each command is carried by a letter with `Alt` that
arrived in all five terminals, `Alt` to mark and `Alt+Shift` to unmark, and the
classic spelling is bound beside it for a driver that may one day deliver it, as
`Ctrl+\` is beside `Ctrl+4`. Bare `+` and `-` keep opening the pattern popup.

| Command | Bound for every terminal | Classic spelling, bound beside it |
|---------|--------------------------|-----------------------------------|
| Mark by extension (P17.3) | `Alt+E` | `Ctrl+grey+` |
| Unmark by extension (P17.3) | `Alt+Shift+E` | `Ctrl+grey-` |
| Mark by name (P17.3) | `Alt+N` | `Alt+grey+` |
| Unmark by name (P17.3) | `Alt+Shift+N` | `Alt+grey-` |
| Mark all files (P17.4) | `Alt+A` | `Shift+grey+` |
| Unmark all files (P17.4) | `Alt+Shift+A` | `Shift+grey-` |
| Invert (P17.4) | `Alt+I` and a bare `*` | `Ctrl+grey*` |
| One step back (P17.5) | `Alt+M` | `Ctrl+M` |
| Up one level (P17.6) | `Ctrl+PgUp` and `Alt+U` | — |
| Go to a favourite (P17.9) | `Alt+0` … `Alt+9` | — |
| Keep a favourite (P17.9, P17.10) | `Alt+K` and a digit; `Ins` in the favourites popup | — |
| The favourites popup (P17.10) | `Alt+F8` and `Alt+B` | — |
| The names (P18.1) | `Ctrl+Ins` and `Alt+C` | — |
| The full paths (P18.2) | `Alt+Shift+Ins` and `Alt+P` | — |
| Where the links lead (P18.3) | `Ctrl+Alt+Ins` and `Alt+L` | — |
| Going to the path on the clipboard (P18.4) | `Ctrl+Shift+Ins` and `Alt+J` | — |
| Files to be copied (P18.6) | `Alt+Shift+C` | `Ctrl+Shift+C` |
| Files to be moved (P18.6) | `Alt+Shift+X` | `Ctrl+Shift+X` |
| Pasting files (P18.7) | `Alt+Shift+V` | `Ctrl+Shift+V` |

A bare `*` is the classic invert and arrives everywhere, so it is taken from the
quick search, where a name containing `*` is rare and on Windows impossible.
`Shift+Alt+0` … `Shift+Alt+9` is not bound at all: it arrives nowhere, and what a
driver would make of it is the shifted character of the keyboard layout, not a
key. `Alt+D` stays free for the drive popups, which a GNOME desktop takes from
`Alt+F1` and `Alt+F2`. The keys of phases 19 and 20 arrived as planned and need
no stand-in.

**The memory of the marks is one step deep and dies with the next command.**
`Alt+M` exists to take back a marking command that went wrong — a pattern that
cleared what was wanted, an inversion pressed by mistake. Each panel keeps the
set it held before its last marking command; `Alt+M` swaps the two, so pressing
it again goes forward to where it was. Any message that is not a marking command
forgets the memory, with three exceptions that change nothing a user could lose:
moving the cursor, the quick search, and `Tab`, which only moves the focus.
Changing directory forgets it like everything else, and names that no longer
exist when the memory is restored are left out. P17.5 settled two cases the
paragraph left open: a marking command that changes no mark — a pattern that
matches nothing, `Alt+E` with `..` under the cursor — records nothing, so it
cannot wipe a memory worth having; and the refresh the directory watch raises
keeps the memory as well, because no user asked for it, and it is the one way a
name can vanish while the memory lives, which is what the last clause is for.

**A size in the listing is compact; the exact number lives below it.** The size
column spells files and measured directories through `CompactSize` — `1.5 KB`,
`≥59.6 GB` — which is what the drive popup already uses. The panel footer and
the status line keep the exact number of bytes, so nothing is lost, only moved
to where there is room for it.

**The favourites have a file of their own.** `favorites.conf` sits next to
`settings.conf` and `history.conf` and behaves like the second rather than the
first: a change is an operation on the file — read it, apply the change, write
it — raised as an effect and answered by `Shell`. `settings.conf` is written
only on quit and on `Shift+F9`, so a favourite set silently would be lost by a
crash and erased by a second copy of Marbas quitting later; the history's way
loses neither. The ten slots are global, shared by every tab and both panels.

- **P17.1 The marks stay on screen when the cursor moves** — today a mark
  disappears from the listing as soon as the cursor moves off it, while the
  footer still counts it: the state is right and the view is not. The task finds
  what in the `ListView` drops the highlight — the built-in marking that
  `MarkMultiple` brings, or `FilePanel.render` not re-applying `SetMark` on a
  cursor move — and stops it, so the view keeps showing what
  `PanelState.Selected` says. *Done when:* a panel with marks is rendered and
  driven through a real `Application` with `↑`, `↓`, `PgDn`, `Home`, `End` and
  the side-arrow jumps, and every marked row is still drawn as marked after each
  of them.
- **P17.2 The keys of phases 17 to 20, measured** — `_probes/KeyProbe` gains, in
  the panel: `Ctrl`, `Alt` and `Shift` with the grey `+` and `-`, `Ctrl` with the
  grey `*`, the same with the main-keyboard keys, and whether the two are told
  apart at all; `Ctrl+M`, which a terminal usually sends as `Enter`; `Ctrl+PgUp`;
  `Alt+0` … `Alt+9` and `Shift+Alt+0` … `Shift+Alt+9`, which a terminal may
  deliver as `Alt` with the shifted character of the layout; `Alt+F8`; `F2`;
  `Ctrl+Ins`, `Alt+Shift+Ins`, `Ctrl+Alt+Ins`, `Ctrl+Shift+Ins`, `Ctrl+Shift+C`,
  `Ctrl+Shift+X` and `Ctrl+Shift+V`, several of which Windows Terminal keeps for
  its own copy and paste. In the viewer: `F7`, `Shift+F7` and `F8`. Inside a
  modal `Dialog` holding a list: a bare letter, `Ins`, `Del`, `F4` and
  `Ctrl+↑` / `Ctrl+↓`. The same five terminals as P13.1 answer, and a key that
  does not arrive everywhere gets a second binding that does. *Done when:* the
  table in [Cross-platform](../Architecture/CROSS-PLATFORM.md) has a row for
  every key, and each later task of phases 17 to 20 knows which spelling it
  binds. The two rounds ran on 2026-10-02 and 2026-10-03; what they decided is
  the table above.
- **P17.3 Marking by the entry under the cursor** — `Alt+E` marks and
  `Alt+Shift+E` unmarks every entry with the extension of the entry under the
  cursor; `Alt+N` and `Alt+Shift+N` do the same for every entry with its name
  without the extension. `Ctrl+grey+`, `Ctrl+grey-`, `Alt+grey+` and `Alt+grey-`
  are bound beside them. The extension is what follows the last dot, so
  `archive.tar.gz` has `gz` and the name `archive.tar`; a name with no dot has
  an empty extension, which matches the other names without one. Case is
  ignored, as in the patterns, and directories take part like files. `..` under
  the cursor does nothing, and `..` is never touched. *Done when:* each of the
  four is proved on `MockFileSystem` over a listing mixing files, directories,
  double extensions and names without a dot.
- **P17.4 Marking all and inverting** — `Alt+A` marks every file and
  `Alt+Shift+A` unmarks every file, both leaving the marks of directories as they
  were; `Alt+I` and a bare `*` invert the marks of every entry, directories
  included, and `*` no longer opens the quick search. `Shift+grey+`,
  `Shift+grey-` and `Ctrl+grey*` are bound beside them. `..` is skipped by all
  three. *Done when:* the three are proved on a listing
  with marked and unmarked files and directories, and each composes with a
  pattern applied before it.
- **P17.5 One step back** — `Alt+M` swaps the panel's marks with the set they
  held before the last marking command, as the decision above describes, and
  `Ctrl+M` is bound beside it although every terminal measured turns it into
  `Enter`. Every
  marking command — `Insert`, `Space`, the shift arrows and page keys, `+`, `-`
  and the commands of P17.3 and P17.4 — records the set it found; every other
  message forgets it except a cursor move, the quick search and `Tab`. *Done
  when:* undoing and redoing each kind of marking command is proved, a file
  operation and a change of directory are proved to forget, and the three
  exceptions are proved to keep.
- **P17.6 Up one level** — `Ctrl+PgUp` does what `Enter` on `..` does: the
  parent directory with the cursor on the directory just left, the level above
  inside an archive, out of the archive at its top level. On a root it does
  nothing. In a panel of search results it goes back to the directory the search
  was started from. It is the mirror of `Ctrl+PgDn`, and `Alt+U` is bound
  beside it, because gnome-terminal keeps `Ctrl+PgUp` once it has several tabs.
  *Done when:* each of the four cases is
  proved, and the landing is remembered in the folder history like every other.
- **P17.7 Compact sizes in the listing** — the size column formats with
  `CompactSize` in place of the exact number, for files, for directories that
  `F3` measured (keeping `≥`) and in a panel of search results; below a kilobyte
  it reads `512 B`. The footer and the status line stay exact. The column can be
  narrower than it is, which leaves more room for the name. *Done when:* a panel
  is rendered with sizes from bytes to terabytes and checked on the screen, and
  the footer of the same panel still adds up to the exact number.
- **P17.8 The drive popup beside its panel** — `Alt+F1` opens *Drives* over the
  left panel and `Alt+F2` over the right one, near that panel's outer edge
  rather than in the middle of the screen; a popup wider than its panel is moved
  inwards only as far as it takes to stay on the screen. *Done when:* both
  popups are rendered on a wide and on an eighty-column terminal and each sits
  over the panel it is for. The task settled the reading with the user: the
  popup stands two columns in from its panel's outer edge and reaches inwards,
  over the other panel if it is wider than its own, and it moves only when it is
  wider than the screen less those two columns.
- **P17.9 The favourite directories** — `Alt+K` followed by a digit keeps the
  place the active panel stands in under that digit, overwriting whatever the
  slot held, without a further word; `Alt+K` asks for the digit on one line, and
  anything but a digit leaves the slots as they were. `Alt+0` … `Alt+9` send the
  active panel there. `Shift+Alt+0` … `Shift+Alt+9`, the spelling first planned
  for keeping, reaches Marbas in no terminal and is not bound. A
  place is a directory or a level inside an archive, as in the history; a panel
  of search results keeps nothing. Going to a favourite is remembered in the
  folder history; a slot that is empty does nothing, and a place that has gone
  is reported with the panel left where it was. `favorites.conf` and its
  operations come with the task, on the pattern of `History.load` and
  `History.change`. *Done when:* keeping, overwriting and going are proved on
  `MockFileSystem`, `Alt+K` followed by something other than a digit is proved
  to keep nothing, a change made on top of a file another copy wrote is proved
  to keep both, and the application still starts without the file. The task
  settled the one line with the user: it is a popup of one line and no buttons,
  closed by the first key, and a digit still pressed with `Alt` held from
  `Alt+K` counts as the digit.
- **P17.10 The favourites popup** — `Alt+F8`, labelled in the `Alt` layer of the
  bar, opens a popup with the ten slots in order, each showing its place, a long
  path shortened in the middle as in the history; an empty slot is a row with its
  digit and nothing else. `Alt+B` opens it too, because a GNOME desktop keeps
  `Alt+F8` for itself. `Enter` or the digit sends the active panel to the
  slot's place, `Ins` keeps the active panel's place in the slot under the
  cursor, overwriting it, and `Del` empties the slot, each written at once. It
  is also the way to every slot where a terminal keeps `Alt+0` … `Alt+9`, as
  gnome-terminal does once it has several tabs. *Done when:* the popup is
  rendered and driven through a real `Application`, a slot filled with `Ins` is
  proved, and a slot emptied in it is empty after a restart.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md) and the architecture file it
touches — the selection and the panel in
[UI and input](../Architecture/UI-AND-INPUT.md), the favourites file in
[Settings persistence](../Architecture/SETTINGS-PERSISTENCE.md) — for what it
adds, rather than leaving the description for a task at the end.
