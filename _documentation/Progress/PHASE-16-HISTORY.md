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
