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
