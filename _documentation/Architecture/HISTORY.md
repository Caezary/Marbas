# History

What the application remembers of where the user has been and what they did,
and how it hands it back. The decisions behind the phase — its own file, the file
as the source of truth, the moment stamped by the shell, global capped lists,
nothing checked on show, places rather than listings, text rather than values —
are set out in [Phase 16](../Plan/PHASE-16-HISTORY.md); this file records what
the code does about them as it arrives.

## The controls, measured before use

P16.1 rendered the controls the phase leans on — `Tabs` holding a `TableView`
per list, and an editable `DropDownList` — inside a `Dialog` dressed by
`DialogStyle`, and drove them with synthetic keys through a running
`Application`, the way the `render-tui-screen` skill does. Terminal.Gui 2.4.17
answered as follows, and each answer is a constraint on a later task.

**`Tabs` switches on its own keys, not on the ones the plan named.** `←` and `→`
walk the tabs, and so do `Tab` and `Shift+Tab`, while `↑` and `↓` move the cursor
of the table in front; `Ctrl+PgUp` and `Ctrl+PgDn` are not handled at all and
reach the dialog's `KeyDown` untouched. P16.4 therefore binds those two in the
dialog and sets `Tabs.Value` itself, and keeps the arrows as they come. A
`TableView` with `FullRowSelect` does not take `←`/`→` from `Tabs`.

**A `TableView` eats letters until its navigator is removed.** Its
`CollectionNavigator` answers every printable key by looking for a row that
starts with it, and reports the key handled whether or not a row was found, so
the dialog never sees it. With `CollectionNavigator <- null` — the same thing
`TabDialog` and `DriveDialog` do to their `ListView` through
`KeystrokeNavigator = null` — letters, digits, `Space` and `Backspace` all reach
the dialog, which is where the filter of P16.8 reads them. `Home` and `End` stay
with the table and leave the cursor on the row it was on, rather than taking it
to the first or the last one.

**A button's hot key fires on a bare letter.** With the navigator gone, typing
`d` pressed `_Delete` and closed the dialog: once no focused view takes a
letter, Terminal.Gui treats it as the hot key of whichever button carries it,
without `Alt`. A popup that filters by typing therefore gives its buttons no hot
keys at all, and `Alt+G` for `[Go to]` is bound in the dialog's `KeyDown` the way
`DriveDialog` binds `Alt+R` — so the letter reaches the filter and the key with
`Alt` still acts.

**`Enter` presses the default button, and `Result` says which.** `[Open]` made
the default is pressed by `Enter` whatever row is under the cursor, which is the
behaviour P16.4 wants. `Dialog.Canceled` is not the way to tell the buttons
apart: it answered `true` for `[Go to]` as well as for `Esc`, so the popup reads
`Dialog.Result`, the index of the button pressed, the way `DriveDialog` does.
`Del`, `Shift+Del` and `Backspace` are not taken by the table and reach the
dialog. `Esc` closes it.

**The dialog frame, the tab headers and the table draw three line styles.** The
dialog is `LineStyle.Double`, the tab headers are rounded and the table's grid
is single. The colours follow the dialog scheme — `White` on `Blue`, the cursor
row and the header of the tab in front `Black` on `Aqua` — so nothing there
needs a scheme of its own; the headers want `TabLineStyle` settled in P16.4.

**An editable `DropDownList` opens its list on `Space`.** `Space` is one of its
default bindings next to `F4` and `Alt+↓`, which in a field meant for paths
makes `Program Files` impossible to type. Removing that one binding from the
instance, `KeyBindings.Remove Key.Space`, lets it type a space again and leaves
`F4` and `Alt+↓` opening the list.

**`↑` and `↓` walk the history of a closed drop-down.** Each press replaces the
text with the next or previous item of the list, without opening it. In a
dialog of one field that is the history of a shell and costs nothing; in the
larger dialogs of P16.10 it takes away the arrows that move between fields, and
that task decides whether to keep the walk or remove the two bindings.

**`Enter` and `Esc` on the open list act on the whole dialog.** `Enter` on a row
puts it into the field and then presses the default button, so choosing a
remembered target would also start the copy; `Esc` closes the dialog rather
than the list. Both are cured outside the control: the field's `Accepting` is
marked handled while the list has focus, which keeps the choice and stops it
there, and an `Esc` that arrives while the list has focus is turned into the key
that closes the list. With both guards the list behaves as a list — `Enter`
chooses, `Esc` closes it, and the next `Enter` confirms the dialog. Pressed by
hand during the runs of the key probe, whose field carries no guard, the
unguarded `Enter` closed the popup as well, so this is the control's
behaviour and not an artefact of synthetic keys; P16.9 does not ship a field
without the guard. The `Esc`
guard has to sit on `app.Keyboard.KeyDown`, because the list lives in a popover
that is not a subview of the dialog and the dialog's `KeyDown` never sees it.

**The list keeps the stock colours and the width of its longest item.** It is
drawn `Charcoal` on `LightBlue`, bold, without a frame, as wide as its longest
row rather than as the field; the arrow on the right of the field is `Black` on
`Aqua`. P16.9 gives it the dialog's colours. A remembered item chosen from the
list also leaves the insertion point where it was, so the next letter lands in
the middle of the path rather than after it — P16.9 moves it to the end.
