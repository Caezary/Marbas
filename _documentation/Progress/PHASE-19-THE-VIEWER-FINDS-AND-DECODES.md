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

**P19.2 let `F8` choose the code page the viewer reads the file in.** `Charset`
gained Windows-1250, CP852, ISO-8859-2, Windows-1252 and CP437, each a
single-byte code page with a one-byte line feed and no mark, and
`TextEncoding.charsets` lists all nine in the order the popup offers them. The
viewer's choice is a `CodePage` — `Automatic`, or `Fixed` of a charset — kept in
`ContentView` beside the `TextEncoding` it reads as, so it is gone when the
window closes and the next `F3` detects as it always did.

`F8` asks through a function `FileViewer.create` is handed, the way it is handed
`close`: the viewer gives a `CodePageOffer` — what detection found and what is
in use — and gets back the page chosen or nothing. `Prompts.showFile` hands it
one that runs `CodePageDialog` on the application; the tests hand it an answer.
`LineIndex.Index` now takes the encoding as well as the source, so a new index
can be built over the same open file. Choosing re-reads at the same place: the
top line's start offset in text, the same row in hex.

Three things the plan left open were settled with the user before the tests
were written. The character column of the dump decodes too, in whatever the
viewer reads as — `Auto` included, so a binary file read as Latin-1 now shows
`¥` where it showed `.`. A character of several bytes stands over its first
byte and leaves the cells of the others blank, so the column keeps a cell to a
byte. And `F8` reads `Code` on the bar.

The column is laid out by `HexText.cells`, over a `RowBytes` that carries three
bytes on either side of the row: enough to finish a UTF-8 sequence that runs
past the row's end, and to start the walk early enough to meet a sequence that
began in the row before, which then leaves its tail blank instead of dotted. A
character two columns wide takes two of its cells; with one cell left in the
row it shows as `.`. `TextEncoding.characterLengthAt` tells the walk where each
character starts and how long it is, and answers nothing for a continuation
byte, a lone surrogate or a sequence cut short.

One decision of the plan was not followed. It had the composition root register
`CodePagesEncodingProvider` for the process; `TextEncoding` asks
`CodePagesEncodingProvider.Instance` for the five code pages directly instead,
which needs no process-wide switch, no start-up order and no setup in the tests.
[File operations](../Architecture/FILE-OPERATIONS.md) records the reasoning.

The tests: `TextEncodingTests` read the same four bytes in all nine charsets and
get nine different texts, prove the line feeds, the units, the mark kept or
taken as content when a charset is chosen, and the character lengths in UTF-8,
both UTF-16s and a single-byte page, the broken cases included;
`LineIndexTests` read lines in an encoding given and find UTF-16 line feeds;
`RowBytesTests` the bytes on either side; `HexTextTests` every layout above;
`HexDumpTests` the column following the encoding, a character split between
two rows, and a row read from a file with its neighbours; `CodePageTests` the
list, its labels and what each page reads as; `ViewerKeysTests` `F8` and its
label; `CodePageDialogTests` the rows, the cursor on the page in use, a choice
accepted, cancelled or made over nothing, and the list inside its frame; and
`FileViewerTests` the offer `F8` makes, the offer after a choice, every page
read over the same four bytes, a choice declined, the header, the place kept
in text and in hex, a UTF-16 choice finding its own lines, the characters of
the dump, a fresh viewer settling afresh, and the bar.

Driven through a running `Application` with the real adapter, `F8` opened the
popup over the viewer with `Auto (Latin-1)` first, the arrows and `Enter`
chose CP852, and a file written in CP852 read `Zażółć gęślą jaźń` in text and
in the dump; `F8` again opened the popup and `Esc` closed it without closing
the viewer. A UTF-8 file in the dump showed `Zaż ó ł ć` with `中` dotted at the
end of a row and its tail blank at the start of the next.
