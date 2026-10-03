# Phase 19 — The viewer finds and decodes

Goal: the viewer of `F3` can search the file it shows and can be told which code
page the file is written in, and its hex dump reads in two halves of eight
bytes. The keys were measured in P17.2, and `F7`, `Shift+F7` and `F8` reach a
full-screen window in every terminal measured.

Three decisions run through the whole phase.

**A search reads the file the way the viewer does.** The viewer owns no
document and reads only the rows on screen, and the search keeps it that way: it
walks the file through the same window of bytes, off the UI thread behind the
progress window, so `Esc` stops it on a file of any size. Text is matched after
it is decoded with the code page the viewer is using, so what is found is what
is shown; in the hex dump the field holds bytes (`4D 5A`) and they are matched
as bytes. A match is scrolled into view and highlighted, and the end of the file
in the direction of the search ends it with a message rather than wrapping
round.

**The code page is chosen for the viewing, not for the file.** `F8` offers a
fixed list — `Auto`, which is what the viewer settles on today, then UTF-8,
UTF-16 LE, UTF-16 BE, Latin-1, Windows-1250, CP852, ISO-8859-2, Windows-1252 and
CP437. The choice holds until the viewer is closed and is not remembered: the
next `F3` settles the encoding as it always did. The code pages .NET does not
carry by default are registered through its code-page provider, once, by the
composition root.

**The header and the bar say what is on.** The header already names the
encoding; it names the one chosen. The bar shows `F7` and `F8` with the viewer's
own commands, the way it shows `F2` and `F4` today.

- **P19.1 The rule in the hex dump** — a vertical line separates the eighth byte
  of a row from the ninth; the character column stays one block of sixteen. The
  row grows by two columns and still fits eighty. *Done when:* `HexDump.row` is
  proved for full rows and for a short last row, and a dump is rendered and
  checked on the screen.
- **P19.2 Choosing the code page** — `F8` opens a popup with the list above, the
  code page in use under the cursor; choosing one re-decodes the file at the
  same place, and in the hex dump the character column follows it. The
  `Charset` of `TextEncoding` gains the new members, each knowing its line
  break and how many bytes it takes. *Done when:* each code page is proved on a
  file whose bytes differ between them, the popup is driven through a real
  `Application`, and the next `F3` is proved to settle the encoding afresh.
- **P19.3 Searching forward** — `F7` opens a popup with the text, a field with
  the history of earlier searches as the fields of P16 have, and four boxes:
  case sensitive, whole words, regular expression and backwards. This task makes
  a plain forward search work, from the top of the screen, behind the progress
  window, with the match highlighted. `F7` pressed again opens the popup again,
  holding the last text. *Done when:* a match on screen, a match further down a
  file larger than one window of bytes, no match at all and a search stopped
  with `Esc` are proved, and the highlight is checked on the screen.
- **P19.4 Again, backwards, and by whole words** — `Shift+F7` repeats the last
  search from the match onwards without the popup; the backwards box searches
  towards the start, and `Shift+F7` then goes on backwards; case sensitivity and
  whole words narrow what matches. *Done when:* each option is proved on its own
  and combined with the others, at both ends of the file.
- **P19.5 Regular expressions** — the regular expression box matches the text as
  a .NET regular expression, one line at a time; a pattern that does not parse is
  reported when the popup is confirmed, before anything is read. *Done when:* a
  match, a pattern that matches nothing and a broken pattern are proved, forwards
  and backwards.
- **P19.6 Searching the hex dump** — in hex mode the field takes byte values
  separated by spaces, and the match is the run of bytes, highlighted in both
  columns. The text options do not apply and are greyed out. *Done when:* a run
  inside one window of bytes, a run split across two, and a field that is not
  bytes are proved.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md) and the viewer's section of
[UI and input](../Architecture/UI-AND-INPUT.md) for what it adds.
