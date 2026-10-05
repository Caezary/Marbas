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

**P19.3 made `F7` search the text forward.** `LineIndex.walkFrom` walks the
lines from a line start one window of bytes at a time, announcing each window
before reading it, and cuts, trims and ends lines by the rules `Index.Text`
reads them with. `WindowSearch.forward` looks for the text in each line,
ignoring case, and answers `Found` with the line's start, the column and the
length, `NotFound`, `Stopped` — asked at each window — or `Unreadable`. The
viewer starts it at the line on top of the screen, in the encoding it reads in,
through `RunSearch`; in the application that is `WindowSearchRunner.run`, the
walk on a background task under the progress window, which reports the
percentage only when it changes and waits for the walk to end after `Esc`.
`FileWindow.Source` locks its stream, since the viewer and the walk may read it
at once.

`FileViewer.create` now takes one record of ports — close, the code page, the
search popup, the history, the runner and the report — where it took two
functions. `Shell` hands the viewer a `ViewRequest`: the source and a
`SearchHistory` over the history file. `ViewerSearchDialog` has the field with
the history and the four boxes the plan names, greyed out until P19.4 and P19.5
give them a meaning, as the history popup's buttons waited for theirs. `F7`
does nothing in the hex dump, and its slot on the bar is blank there, until
P19.6.

The rows the content draws became `ShownRow`s — the line, the column the row
starts at, the text — so the highlight is one rule over any row:
`FoundSpan.ofMatch` turns the column in the decoded line into the columns it
is shown at once tabs are expanded, and `FoundSpan.onRow` finds the part of
the span on a row, plain, scrolled across or wrapped. The span is painted
black on cyan after the rows. A choice of code page drops it.

Three things the plan left open were settled with the user before the tests
were written. The field's history is the one *Find files* keeps for its text,
`input.find.text`, so each offers what the other searched for. A match off the
screen brings its line to the top. And the progress window opens for every
search, like every other runner's, even when the match is on the screen.

The tests: `LineIndexTests` hold the walk to the lines and starts the index
reads over eight files — feeds, a final feed, carriage returns, empty lines,
the empty file, a mark — and over UTF-16, a line across two windows and a line
past the longest, and prove where it starts and where it says it reads;
`WindowSearchTests` a match on the first line and further down, the case, the
first of several, the start, a match past the first window, nothing, nothing
across a break, the empty file, the code page, UTF-16, a stop, the progress and
a stream that fails; `FoundSpanTests` the columns past a tab and across one,
the part on a row and none, and the scroll across; `ViewerSearchTests` the
progress line and the messages; `ViewerKeysTests` `F7` and its label in both
modes; `ViewerSearchDialogTests` the history, the empty and the remembered
field, the four greyed boxes inside the frame, a confirmed and a cancelled
popup; `WindowSearchRunnerTests` a report reaching the dialog, one repeated, one
queued before a stop and one made after it; `FileViewerTests` the offer, the
last text, remembering, an empty field, a cancelled popup, a match on screen
and one past the first window of a ten-thousand-line file, the start at the top
of the screen, nothing found, a stop, a failure, a match past the right edge,
in a wrapped line and split by the wrap, after a tab, in a chosen code page,
the mark dropped by a code page, `F7` in hex and the bar; `ShellTests` the
history handed to the viewer and a text it remembers.

Through the probe, with the real dialog and the synchronous runner the tests
use: `F7` opened *Search* over the viewer with its four boxes in grey, `some
text` typed and confirmed was remembered and drawn black on cyan from column
eight of the first line, past its tab; `needle` at column 100 of the last of
three thousand lines brought that line to the top with the screen moved across
so the word stood at the right edge; and `absent` raised *'absent' was not
found.* over a screen that had not moved. `WindowSearchRunner.run`, driven
directly with a slow walk, showed `Searching… 24%`, closed itself with
`NotFound` when the walk ended, and answered `Stopped` when `Esc` was pressed.
A search opened from a probe's timeout never finishes, as P18.6 found for the
batches, so the two halves were driven apart.
