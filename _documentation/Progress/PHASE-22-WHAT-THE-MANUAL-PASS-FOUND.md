# Phase 22 — What the manual pass found

The tasks are in
[Phase 22 — What the manual pass found](../Plan/PHASE-22-WHAT-THE-MANUAL-PASS-FOUND.md).

**P22.1 let the bottom row of the user menu be removed.** `fill` in
`UserMenuDialog` read the cursor, replaced the list and put the same index
back. After `Del` on the bottom row that index is one past the end, after `Del`
on the only row there is no index at all, and `ListView.SelectedItem` throws an
`ArgumentException` on either — which, raised from a key handler, ended the
session. The refill now asks `UserMenuState.cursorWithin` for the row: the
entry at the index the cursor had or the next one below it, else the last
entry, and no row when the menu holds no entry. A line is never the answer, so
a menu left ending in a line puts the cursor on the entry above the line.

Three tests in `UserMenuDialogTests` were written first and failed on that
exception: the bottom row removed with an entry right above it, the bottom row
removed under a line, and the only row removed. They pass now, the cursor on
the row above, on the entry above the line and on no row.

Two things went differently from the plan.

- **No other popup shares the shape.** The plan had the history and the
  settings read for it and given the same two tests where they have it. The
  history popup refills a `TableView` and then stands on `min row last`, on
  nothing when no row is left, and `HistoryDialogForgetTests` already forgets
  the last row and the only one. The settings popup never puts a cursor back:
  `SetSource` leaves a list with no cursor, which two throwaway tests
  confirmed by removing the row under the cursor at the bottom of one list and
  as the only row of the other, and pressing `↓`, `↑` and `End` afterwards.
  The favourites popup does put its index back, into a list that always has
  ten rows. The popups of tabs, drives, code pages and handlers fill their
  list once. So the two tests exist for the user menu alone.
- **What the popup remembers moved into one record.** [Coding style](../coding-style-conventions.md)
  holds `UserMenuDialog` to its rule on what a view keeps the next time the
  view is changed, and this was that time. The menu and the entry chosen were
  two settable properties of `UserMenuView`; they are `UserMenuState`, one
  record behind one `let mutable`, changed through `Change` as
  `ResultsView` is. The decisions the handlers made — which row holds an
  entry, which entry a hotkey names, where a step lands, where the cursor
  stands after a refill — are functions of that record and the cursor the list
  reports, in `Ui/Dialogs/UserMenuState.fs`, compiled before the view and
  opening no `Terminal.Gui` namespace. `changed` used to work out the nearest
  entry a second time after `fill` had put the cursor back; it now only moves
  the cursor to the entry the change was about, when there is one.
  `UserMenuStateTests` holds one test per rule, thirty for these, and the
  twenty-five tests that press keys on the real popup pass unchanged.

The screen was read with the `render-tui-screen` probe, through
`Prompts.chooseFromUserMenu` — the real adapter, with its real question — over
a menu of two entries: `↓`, `Del` and *Remove entry* leave one row and a popup
one row shorter; `Del` and *Remove entry* again leave an empty list; `↓`,
`Enter` and `Del` on the empty menu do nothing, and `Esc` closes it with
nothing chosen.

**A second defect of the same refill was closed with it.** It was not in the
plan: it was seen in the running application once the first was fixed. An
entry added to a menu of one was shown alone, the first entry out of sight
until `↑` was pressed, as if the new one had replaced it. The popup grows with
its menu, but the list gets its new height from the layout pass after the key,
and the cursor was put on the new row while the list was still one row tall —
so the list scrolled down to it and kept the scroll when it grew. A test in
`UserMenuDialogTests` adds an entry to a menu of one, lays the popup out as the
loop would and failed on a list shown from its second row.
`UserMenuState.topRowWithin` now answers the row a list of a given height may
start at, and `fill`, which already runs when the list's frame changes, sets
the viewport to it. Eleven tests in `UserMenuStateTests` hold the rule, and the
probe shows both entries after `Ins`, where it showed one before.

4159 tests pass and 16 skip on Windows.
