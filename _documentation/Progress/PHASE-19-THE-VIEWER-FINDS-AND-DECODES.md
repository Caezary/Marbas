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

**P19.4 added `Shift+F7`, the search backwards, case sensitivity and whole
words.** `WindowSearch.forward` became `WindowSearch.run`, which takes a
`Query` — the text, a `LetterCase`, a `WordMatch` and a `SearchDirection`, the
one new type in `Domain` — and a `Place`, a line start and a column. Forwards
it takes the first match at or after the column, then the first of each line
below; backwards the last match before the column, then the last of each line
above. Going back needed a walk of its own: `LineIndex.walkBackFrom` reads the
windows of bytes towards the start, finds the feeds from each window's end and
answers the lines before a line start, nearest first. A line it finds whole in
one window is sliced from it; one that began earlier or ended later is read
again from its start, so a line is cut at `MaxLineLength` the way the screen
cuts it. The line the search starts on is read forwards first, then the
backward walk takes over. `LetterCase.comparison` and
`WordMatch.isWordCharacter` moved into `Domain` beside their types, and
`ContentMatch` uses them too, so *Find files* and the viewer agree on what a
word is.

The popup answers a `SearchAnswer` — the typed text and the three choices —
and opens on the last query, text and boxes, while the viewer is open; the
regular expression box stays grey until P19.5. The content answers where a
search starts: `ScreenPlace` for `F7`, `PlaceAfterMatch` for `Shift+F7`, which
keeps the match on display beside its span and loses both with a choice of
code page.

Four things the plan left open were settled with the user before the tests
were written. A search backwards from the popup starts at the end of the line
on the last row, the mirror of a search forwards from the top, so a match on
the screen is found either way. `Shift+F7` with nothing searched for yet opens
the popup. Going forward it starts past the end of the match, so matches do not
overlap. And the boxes remember their state until the viewer closes.

The tests: `LineIndexTests` hold the backward walk to the index's lines,
reversed, over seven files and over UTF-16, a line across two windows and a
line past the longest, and prove the first line, a marked first line and the
offsets it reads at; `WindowSearchTests` were moved to `run` and prove a
search from a column, a match at the very end, the end of the file, the case
both ways, whole words against part of a word, a whole line, digits and
underscores and punctuation, backwards to a line above, before the column,
from a line's end, the last match of a line above, the start of the file, past
a window, in UTF-16, stopped and reporting its progress, and the options two
and three at a time, at both ends; `ViewerSearchTests` the answer turned into
a query; `ViewerKeysTests` `Shift+F7`; `ViewerSearchDialogTests` the last
options ticked, nothing ticked at first, the three boxes enabled and the
answer with and without options; `FileViewerTests` `Shift+F7` asking when
nothing was searched, finding the next match without asking, going past the
match's end, reporting the end and keeping the mark, a search back from the
bottom of the screen and on backwards, to a line above the screen, past the
first match, the case and whole words through the popup, the options repeated
and offered back, a fresh start after a code page and `Shift+F7` in hex.

Through the probe, with the real dialog and the synchronous runner: `Alt+B`
and `Alt+A` ticked *Backwards* and *Case sensitive* in the popup; `line 1`
searched backwards marked `line 19`, the lowest match on the screen, and
`Shift+F7` moved the mark to `line 18`; `F7` again opened on `line 1` with the
two boxes still ticked.

**P19.5 let the field be a regular expression.** `SearchPattern`, new in
`Domain`, is a pattern that parsed: `tryCreate` builds it once with the linear
engine and answers `PatternDoesNotParse` or `PatternNotSupported` with the
sentence .NET gave, and `regex` builds the expression for a case. The query
changed shape so a broken pattern and whole words over a pattern cannot be
written down: `WindowSearch.Query` is `Wanted` — `Literal` of a text and its
`WordMatch`, or `Pattern` — with the case and the direction beside it. Both
kinds answer every position where a match starts with its length, so the
direction, the place and `Shift+F7` work for a pattern exactly as for a text;
a match of no characters is passed over. `ViewerSearch.query` turns the
popup's answer — now with `TypedAs`, `AsText` or `AsExpression` — into the
query, or nothing for an empty field, or the `PatternError`. `TextNotFound`
names what was wanted, text or pattern, and `PatternRefused` is a new report.

The popup checks the pattern itself. `ViewerSearchDialog.create` takes a
`PatternError -> unit`, which `Prompts` fills with its error box, and the
dialog is now a `SearchView` overriding `OnAccepting`. The override was the
second try: the first refused in `Accepting` handlers on `[Ok]` and on the
field, which the tests drove and passed, but the probe pressed `Alt+R` and
`Enter`, and with the focus on the box the dialog accepted through its own
`OnAccepting`, set its result there and closed — what showed was the viewer's
own check reporting the pattern. After a refusal the focus goes back to the
field, which the probe's next keystrokes needed: they had gone to the box, and
the space in them unticked it.

Four things the plan left open were settled with the user before the tests
were written. A pattern that cannot be used leaves the popup open, the error
over it. The engine is the linear one, which refuses look-arounds,
back-references and atomic groups but cannot hang on a line. A match of no
characters is no match. And whole words are greyed out while the regular
expression is ticked.

The tests: `SearchPatternTests` patterns that parse, four that do not, four
the linear engine refuses, the reason kept, and the case both ways;
`WindowSearchTests` were moved to `Wanted` and prove a pattern's first match
and its length, the case, `^` and `$` at the line's ends, no running past a
line, a match of nothing passed over, `^$` finding nothing, a pattern that
matches nothing, from a column, backwards to a line above, before a column,
past a window and to the start of the file, and in a chosen code page;
`ViewerSearchTests` the messages, the answer turned into a literal, a
pattern, a pattern without whole words, a refusal and nothing; and
`ViewerSearchDialogTests` the pattern offered back with its box, whole words
greyed by it and given back, every box enabled, the answer as an expression,
a broken pattern refused from `[Ok]`, from the field and from the box with
the popup left open, the focus handed back, and nothing refused for a good
pattern or for plain text; `FileViewerTests` a pattern forwards from the top,
backwards from the bottom, repeated with `Shift+F7`, matching nothing in both
directions, a broken pattern reported in both directions before anything is
run or remembered, and the pattern offered back by `F7`.

Through the probe, with the real dialog and the synchronous runner: `(` with
`Alt+R` and `Enter` raised *The regular expression does not parse: Invalid
pattern '(' at offset 1. Not enough )'s.* over the popup; `Esc` closed the box
and left the popup with `(` in the field, the box ticked and *Whole words*
grey; `Backspace`, `line 1[5-9]` and `Enter` marked `line 15`.

**P19.6 let the hex dump search for bytes, and closed the phase.**
`WantedBytes`, new in `Domain`, is what the hex field asks for, read from
hexadecimal digits, and `BytesError` says why a field is not bytes.
`ByteSearch.run` looks for the run window by window — forwards with an overlap
of one byte short of the run, backwards from the window that ends where a run
starting before the offset would — and answers `WindowSearch.Answer`, which
became generic so the text search answers a `WindowSearch.Match`, a line
and a column, and this one a `ByteSearch.Match`, the offset and the length.
The runner was already generic in all but its annotation, so the viewer has a
second port, `RunByteSearch`, filled by the same `WindowSearchRunner.run`.

The byte match was first called `Run`, after the plan's "run of bytes". It
was renamed before the commit: the two types play one part in `Found` and
should not read as two different things, and `Run` sat beside the verb — the
port `RunByteSearch` and the function `ByteSearch.run` — in the same lines as
`ShowRun` and `PlaceAfterRun`, which meant the match. The viewer's members
became `ShowBytesMatch`, `BytesScreenPlace` and `PlaceAfterBytesMatch`.

The popup learned which mode it opens in from the offer: `SearchOffer.Last`
is `LastText` or `LastBytes`, each with the last query of its kind, and in hex
only *Backwards* can be ticked and the answer is `AsBytes`. `ViewerSearch.query`
answers an `Asked` — `TextAsked` or `BytesAsked` — or a `SearchRefusal`,
`BrokenPattern` or `BrokenBytes`; the report that carried a pattern's refusal
became `Refused` of either, and `BytesNotFound` names the bytes. The viewer
keeps a last query and a mark for each mode. `HexDump.hexColumn` and
`HexDump.textColumn` say where a byte stands in a row, from the constants the
row is written with, and `HexMark.spans` turns a run into the columns it covers
on a row, split at the rule; the content draws them through the same
`FoundSpan.onRow` as the text. `F7` reads `Search` on the bar in hex as well.

Three things the plan left open were settled with the user before the tests
were written. The bytes searched for keep a history list of their own,
`input.bytes`. The field is read loosely: pairs spaced or not, and a single
digit as a byte of its own. And *Backwards* applies to bytes as it does to
text.

The tests: `HistoryTests` the new list under its key; `WantedBytesTests`
pairs spaced, unspaced, in either case and between tabs, single digits, a mix,
four tokens that are not hexadecimal, an odd run, a field of spaces and the
spelling; `ByteSearchTests` a run inside one window, at the start, at the end,
split across two windows, past the first window, nowhere, in an empty file,
from an offset past a run and at one, past the last run, backwards before the
offset, across it, from the end, before the first run, split across two
windows and before the window it starts in, stopped both ways, the progress
both ways and a stream that fails; `HexDumpTests` the hex and the text column
of a byte in either half; `HexMarkTests` a run in one half, across the rule,
from the row above, into the next row, over a whole row and on rows it does not
reach; `ViewerSearchTests` the messages for bytes and the answer as bytes,
good and bad; `ViewerSearchDialogTests` the options greyed for bytes, the last
bytes offered back with their direction, the bytes history, the answer as
bytes, and a field that is not bytes refused with the popup left open;
`ViewerKeysTests` the label in hex; `FileViewerTests` the offer in hex, a run
on screen marked in both columns, a run split across two windows brought to
the top and marked on two rows, `Shift+F7` forwards and backwards, a search
back from the bottom of the screen, bytes not found, a field that is not bytes
reported before anything is read, the bytes remembered in their own list,
offered back, a hex mark not drawn in text, a code page keeping it, and
`Shift+F7` in hex asking when no bytes were searched for; `ShellTests` the
bytes history handed to the viewer apart from the texts, and remembered in it.

Through the probe, with the real dialog and the synchronous runner: `F4`,
`F7` and `fc fd fe fb` opened the popup with only *Backwards* in white; `Enter`
marked `FC FD` and `FE FB` on either side of the rule and `üýþû` in the
character column of the third row; `Shift+F7` marked `FC FD`/`üý` at the end
of the fifth row and `FE FB`/`þû` at the start of the sixth; `MZ` raised *'MZ'
is not a byte* over the popup, which stayed open with `MZ` in the field. The
sentence then still said the bytes had to be spaced, which the field does not
ask for, and was reworded.
