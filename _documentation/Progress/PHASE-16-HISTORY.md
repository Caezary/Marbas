# Phase 16 — History

The tasks are in [Phase 16 — History](../Plan/PHASE-16-HISTORY.md).

**P16.1 measured the controls before any of them was used, and three of them
would have gone wrong in the first real task.** A spike rendered `Tabs` holding
a `TableView`, and an editable `DropDownList`, inside a dialog in the classic
colours and drove both with synthetic keys through a running `Application`.
The popup of P16.4 comes out close to the plan: `Enter` presses the default
`[Open]`, `Esc` closes, `Del` and `Shift+Del` reach the dialog, and the tabs walk
on `←`/`→` and `Tab` on their own — only `Ctrl+PgUp` and `Ctrl+PgDn` have to be
bound by hand. The filter of P16.8 cost two findings: the table swallows every
letter until its navigator is removed, and once it is removed a bare letter
presses the button whose hot key it is, so `d` closed the dialog through
`[Delete]`. The popup's buttons therefore carry no hot keys and `Alt+G` is bound
in the dialog. The drop-down field of P16.9 cost three more: `Space` opens the
list instead of typing a space, `Enter` on a chosen row also presses the
dialog's default button, and `Esc` on the open list closes the whole dialog.
Each has a cure that was tried in the same spike — one binding removed, the
field's `Accepting` stopped while the list has focus, and an application-level
`Esc` that closes only the list — and the lot is written up in
[History](../Architecture/HISTORY.md). GNOME's defaults, read with `gsettings`
under WSL, claim none of the phase's keys.

The key probe then ran by hand in the five terminals of P13.1, and every key the
phase binds arrived in all of them with the same code — `Alt+F12` and its
stand-in `Alt+Shift+H` in the panel, `Ctrl+PgUp`, `Ctrl+PgDn`, `Del`,
`Shift+Del` and `Alt+G` in a popup, `Alt+↓` and `F4` in a drop-down field. Two
things were noticed at the keyboard. `Esc` would not close the probe's popups,
which was the probe binding `Command.Quit` to `Ctrl+Q` alone, where the
application binds `Esc` beside it; the probe binds both now. And choosing a row
of the drop-down with `Enter` closed the popup with it — the spike's finding,
confirmed by hand on a field without the guard, and now written into P16.9 as
something the field must not do rather than left as a note.

**P16.2 made the history a value and gave it a file of its own.**
`FileSystem/History.fs` holds the four lists — places visited, files started,
files viewed or edited, and text typed into each of nine kinds of field — each
entry stamped with the moment it was last used. `History.apply` performs one of
three changes, `Remember`, `Forget` and `ClearList`, as a pure function: the
item goes on top, the entry it replaces goes, a list keeps a hundred. What counts
as the same entry is what it points at, so a file viewed and then edited is one
entry, a program started again as administrator is one, and a directory spelled
in another case is one on Windows and two on Unix. `History.change` reads
`history.conf`, applies the change and writes it back, which is what lets two
running copies share one history without either erasing the other; a file that
cannot be read stops the change before anything is written. Two new types came
with it: `Place` in `FileSystem`, the part of `PanelLocation` worth keeping, and
`EnteredText` in `Domain`, the one constructor there that does not trim, because
a field hands back what was typed. `Dependencies` gained `HistoryPath`, beside
the settings file, and `Clock`; nothing reads either yet. The file format and the
reasons are in [History](../Architecture/HISTORY.md).

**P16.3 remembered the directories by comparing, the way the watches are
armed.** Rather than raising a history change in each of the transitions that
move a panel, `Update.update` gained a third step after `transition` and
`watching`: `remembering` compares where both panels of the tab in front stood
before and after the message and appends `ChangeHistory(Remember(Visited
place))` for a side that landed somewhere new. Six messages move a panel
without the user moving it — the external change and the re-read that may walk
a panel up, and the four tab messages that put another tab in front — and they
are named in `movesPanelsItself` and skipped. `Shell` answers the effect with
`History.change`, stamped by `deps.Clock`, and dispatches the file's answer back
as `HistoryChanged`; a failure comes back as `HistoryUnwritten` and raises a
dialog only the first time in a session, which `AppState.HistoryReport`
remembers. Eight existing tests compared whole effect lists that now carry the
history change as well: the watch tests read only the watch effects, and the
two others name the change they expect. Driven through the real window over a
real disk, into `alpha`, into `beta`, back out and into `gamma` in the other
panel, the file read `gamma`, `alpha`, `beta` — newest first, `alpha` once.

**P16.4 put the history on the screen, and the popup behaved as P16.1 said it
would — except for its drawing.** `Alt+F12` and `Alt+Shift+H` raise
`HistoryRequested`; the state answers `ShowHistory` with the tab it was left
on, and `Shell` reads the file afresh, dispatches it back as `HistoryChanged`
and runs `HistoryDialog`. The dialog is `Tabs` over three tables built the way
the spike found necessary — no navigator, buttons without hot keys, `Alt+G` and
`Ctrl+PgUp`/`Ctrl+PgDn` in `KeyDown`, the answer read from `Dialog.Result`. The
answer always carries the tab, so the next opening lands where the last one was
left, and on *Folder history* `[Open]` carries the place, which the state stands
the active panel in through `standInside`. Driven in the real window after
walking into `alpha`, `beta` and `gamma`: the popup opened on *Folder history*
with the three newest first, a bare `d` pressed nothing, and `Enter` on the
third row put the left panel in `alpha\beta` and the file put `beta` on top.
The first picture showed two things to fix: a path wider than the table pushed
its right-hand frame off the screen, so the *What* column now shortens it in the
middle to the table's width, and the table drew its own frame inside the tab's,
so its outer lines are off. A third could not be fixed — `Tabs` misdraws the
joints of its strip once the tab in front changes, by the arrows it handles
itself as much as by our keys — and is in [Rough edges](../ROUGH-EDGES.md). The
colours are the dialog's.
