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

**P16.5 remembered the files started, and decided which starts count by what
was started.** `LaunchFinished` now carries its request, and a successful start
whose target is a file raises the history change; `Alt+R` on a directory or a
drive hands the system a directory and is passed over by the same test, so no
call site had to mark its request. The *History* tab gained a *How* column —
the program's name and *as administrator* — and two answers: `RunAgain`, which
the state turns back into the same `RunProgram`, and `GoTo`, which stands the
active panel beside the file through `Place.beside` and a new
`AppState.standBeside`. `[Go to]` is enabled while that tab is in front and not
empty. Wiring it to `Tabs.ValueChanged` broke fifteen dialog tests at once: the
event also fires while the dialog is disposed, when the tables it looked up are
already gone, so the tab in front is now read from the `Id` of `Tabs.Value`.
Five existing tests changed for the new message shape, one of them renamed,
since a start that succeeds no longer asks for nothing. In the real window, with
the launcher replaced by a recorder so nothing ran on the machine, `Enter` on
`readme.txt`, `Enter` on its row in the popup and `Alt+G` on it gave two
identical requests and a left panel in `alpha` with the cursor on the file.

**P16.6 remembered the files viewed and edited on the way in, and found that
`Dialog.Result` takes only a button.** `F3`, `F4` and `Shift+F4` raise the
history change in front of `ViewFile`, `ViewArchiveEntry` or `EditFile`, from
one function that turns a `FileVisit` into its effect — so the file is written
before a console editor takes the screen, and the popup, choosing a row, goes
through the same function the panel does. A directory measured with `F3`, the
properties `F4` shows for one and an archive entry `F4` refuses are not
remembered, and a visit chosen from the popup whose file is gone is not moved
to the top. *View history* gained its `V`/`E` column and three answers:
`[Open]` repeats the row, `F3` and `F4` view or edit it whatever it says, and
`[Go to]` stands beside it, inside its archive where it is one. The first try
gave `F3` and `F4` result codes of their own past the last button;
`Dialog.Result` threw `ArgumentOutOfRangeException` at the first one, so the
keys press `[Open]` and leave the use they asked for on the dialog. Ten existing
tests compared whole effect lists that now open with the history change, and
name it. Rendered through a real `Application` with an entry on disk, one
edited and one inside a zip, the tab read `V`, `E`, `V` newest first; `F4` on
the viewed row answered editing it, `Enter` on the edited one editing it again,
and `Alt+G` on the zip entry going to its level inside the archive.

**P16.7 let the popup change the file in place, after a first shape that went
round the loop.** That first shape closed the popup on `Del` with the row to
forget, had the state raise the change and ask for the popup again, and carried
the tab and the cursor's row across the reopening. It worked, but everything
the popup holds that the file does not — the scroll already, the filter of
P16.8 next — would have had to travel the same way, so it was redone before
the task closed. Now `HistoryPrompt` carries a `Change` function that `Shell`
builds from the same code that answers the `ChangeHistory` effect: it writes
the file, dispatches the result to the state, and hands the history back to the
popup, which fills its tabs again and keeps the cursor on the row number it was
on. `Shift+Del` asks through a `confirm` the adapter hands the dialog — a
`MessageBox` over the popup, with *No* as its default — and a *Yes* clears the
tab's list the same way. `[Delete]` is a dialog button, which closes the dialog
unless its `Accepting` is handled; a running `Application` showed that handling
it keeps the popup open. The rewrite tripped once over what P16.5 found — the
handler of `Tabs.ValueChanged` runs while the dialog is disposed, so it is given
the container and the buttons rather than looking them up — and once over the
screen: a row stood on far down the list was selected out of sight, since
`SetSelection` does not scroll, so the table now scrolls to its cursor after a
change and whenever its frame changes. The state gained nothing and lost the two
choices of the first shape; the one existing test that compared whole prompts
now compares their fields, since a prompt carries a function. Driven in the real
window over a real disk with `alpha`, `beta` and `gamma` in `history.conf`,
`Del` on `beta` left the file and the open popup holding `gamma` and `alpha`,
`Shift+Del` then `Y` left the file empty and the tab with it, and `Esc` closed
the popup onto the panels.

**P16.8 gave the popup a filter, and it cost nothing to carry because P16.7
had stopped reopening the popup.** The filter is a `SearchText` held by the
popup: typing narrows the tab in front to the rows whose path or *How* holds
it, ignoring case, `Backspace` shortens it, `Esc` clears it and a second `Esc`
closes the popup, and switching tabs clears it. Each row now carries the
position of its entry in the list, and the popup reads the entry from the row
under the cursor, so `Enter`, `F3`, `F4`, `Alt+G` and `Del` act on what a
filtered row shows. `SearchText.foundIn` joined `matches`, and the panel's test
for a typed character moved to `KeyMap.printable` so both read the same rule.
Driven through a running `Application`, typing `Dir1` over twelve directories
left four rows and *Filter: Dir1* under them, `Backspace` brought back all
twelve, and the two presses of `Esc` cleared the filter and closed the popup.
Typing `+` in the real window then showed the one surprise: the letter after
the one that emptied the table never arrived. A `TableView` with no rows takes
printable keys even without its navigator, so every table now hands them to
the filter from its own `KeyDown`, and a test that types past an empty result
holds it.

**P16.9 gave the fields that ask one question a history, and the rendering
overruled one line of the plan.** The `To:` of `F6`, the names of `F7` and
`Shift+F4` and the pattern of `+` and `-` became `HistoryField`s — a
`DropDownList` with the four cures P16.1 found — whose lists `Shell` reads from
`history.conf` just before asking. What was typed is remembered by `Shell` when
the popup is confirmed, before the text is read as a name or a pattern, so the
ports now answer the raw text; `F7` alone remembers in the state, once the
directory exists, and its suggestion is the head of its list, which took
`LastCreatedDirectory` out of `AppState` and `mkdir.name` out of the settings.
The plan also named `F5`, which turned out to ask nothing — it copies straight
into the other panel — so the targets are `F6`'s list. Driven through the real
window with remembered entries in the file, the four cures held — `Space`
typed, `Enter` on the list chose without confirming, `Esc` on it closed only
the list — and three things did not. `App.Invoke` moved the insertion point
before the chosen row arrived, so `x` landed inside it (`D:\newer targext`); the
move now follows the `TextChanged` a choice causes. The list in the dialog's
colours, which the plan asked for, could not be told from the dialog and let
the buttons show through beside it, so it wears the field's colours and the
field's width instead. And a list opened on a text it did not hold had no row
chosen, so the first `Enter` only closed it and `+` confirmed `*`; the newest
row is now chosen when the list opens. With that, `F6` offered and kept its
targets, `F7` opened on `reports` and remembered `new` once it was made, `+`
picked `*.fs` with one `Enter` and applied it with the next, `Shift+F4` offered
`notes.md`, and a cancelled `F6` wrote nothing.
