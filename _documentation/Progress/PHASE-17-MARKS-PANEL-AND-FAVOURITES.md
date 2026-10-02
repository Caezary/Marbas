# Phase 17 — The marks, the panel and the favourites

The tasks are in
[Phase 17 — The marks, the panel and the favourites](../Plan/PHASE-17-MARKS-PANEL-AND-FAVOURITES.md).

**P17.1 found the marks being erased by the list that drew them.** Of the two
suspects the plan named, it was the first: the panel's `ListView` ran with
`MarkMultiple = true` so that Terminal.Gui would paint marked rows in
`Highlight`, and in Terminal.Gui 2.4.17 every built-in move — `↑`, `↓`, `PgUp`,
`PgDn`, `Home`, `End` — ends in `ListView.SetSelection(item, false)`, which in
that mode clears every mark in the source before moving. `FilePanel.render`
was innocent in the sense that it did what it was built to do: a cursor move
leaves the rows `Kept`, so it had no reason to copy the marks in again, and the
footer, read from `PanelState.Selected`, went on counting what the screen no
longer showed. The side-arrow jumps were the exception that hid the pattern:
they go through the state and set `SelectedItem` directly, which marks nothing
and clears nothing.

The cure was to stop the list from marking rather than to keep repairing it
after every move. The panel's `ListView` now runs with `MarkMultiple` and
`ShowMarks` both off, so no move, click or `Ctrl+A` of its own ever writes a
mark, and the `RowRender` handler paints a marked row itself in the new
`ClassicScheme.markedEntry` — the same bright yellow on blue `Highlight` was.
The row under the cursor of the focused panel is still handed back to the
list, so it keeps the cursor bar; a marked row under the cursor of the panel
that lost focus is painted as marked. Re-applying `SetMark` on every render was
the other way, and was turned down: it would have been a pass over the whole
listing on every arrow key, undoing work Terminal.Gui should not have done in
the first place, and it would have left the mouse marking rows the state never
heard of.

Two theories in `PanelPaintingTests` press each of the eight movement keys over
a laid-out panel of forty files with two marks: one asserts the source still
holds both marks, the other that `RowRender` still paints both rows in the mark
colour. Before the fix the first failed for the six built-in moves and passed
for the two jumps; the second failed for all eight, the jumps included, because
a marked row used to be handed back to the list rather than painted. The look was then read off a running
`Application` with the `render-tui-screen` probe — three entries marked with
`Ins`, followed by `↓ ↑ ↓ PgDn PgUp → ← End Home ↓` — and compared with the
same run on the old `FilePanel.fs`:

```
              before                         after
after Ins     file01..03 BrightYellow,Blue   file01..03 BrightYellow,Blue
after Down    file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
after PgDn    file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
after End     file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
after Home    file01..03 BrightBlue,Blue     file01..03 BrightYellow,Blue
```

Before the fix the marks went at the first `↓` and never came back; after it
they stand through every key, and give way to the cursor bar only on the row the
cursor stands on.
