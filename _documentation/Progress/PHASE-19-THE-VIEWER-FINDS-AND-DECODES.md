# Phase 19 — The viewer finds and decodes

The tasks are in
[Phase 19 — The viewer finds and decodes](../Plan/PHASE-19-THE-VIEWER-FINDS-AND-DECODES.md).

**P19.1 split each row of the hex dump into two halves of eight bytes, and
widened the offset to ten digits.** `HexDump.row` formats the two halves on
their own, each padded to the width of eight bytes, and puts ` │ ` between them
where a single space stood. The character column was left one block of sixteen:
the rule is there to count bytes by, and the text reads as it lies in the file.

A short last row keeps the rule in its column even when it holds eight bytes or
fewer, so the line runs unbroken down the screen and the character column
starts where it does on every other row. Nothing else in the viewer had to
change for the rule: the dump is still one string per row, and scrolling across
it, the offset in the header and the switch back to text count rows and
offsets, not columns.

The offset went from eight hexadecimal digits to ten, which the task did not
ask for. Eight digits end at four gigabytes; past that the number grew a digit
and pushed the rest of the row one column to the right, so the rows of a large
image or disk dump no longer lined up with the ones above them. Ten digits keep
a fixed column up to a terabyte. `HexDump.offsetText` spells it, and the header
calls the same function, so the place it names reads exactly like the first
column of the row on top. With the rule and the two digits the row grew from
seventy-five columns to seventy-nine and still fits an eighty-column terminal.

The tests: `HexDumpTests` prove a full row, its width of seventy-nine, a short
row that reaches into the second half, a short row that stays in the first and
an offset past four gigabytes; `ViewerHeaderTests` the ten digits in the
header. The rows the viewer shows through `F4` were rendered at eighty columns
and read on the screen.
