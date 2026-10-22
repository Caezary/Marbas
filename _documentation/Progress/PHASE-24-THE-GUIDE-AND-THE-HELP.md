# Phase 24 — The guide and the help

The tasks are in
[Phase 24 — The guide and the help](../Plan/PHASE-24-THE-GUIDE-AND-THE-HELP.md).

**P24.1 F1 needs no spare.** `_probes/KeyProbe` got a `help` round: `F1`,
`Ctrl+F1`, `Alt+S` and `Alt+Y` in a panel, in a full-screen window, in a popup
holding a list and in a popup holding a text field, and `Tab`, `Shift+Tab`,
`Backspace` and `Alt+←` in the full-screen window. `Tab` being measured, the
round moves on with `Enter` and back with `Ctrl+P`. A person ran it on
2026-10-21 in cmder, Windows Terminal, the Windows console, WSL and
gnome-terminal 3.52.0 under WSLg, and every key arrived in all five. The plan
expected gnome-terminal to keep `F1` for its own help; 3.52.0 binds no key to
it, and GNOME's window manager binds only `<Alt>F1`. The rows are in
[Cross-platform](../Architecture/CROSS-PLATFORM.md#keyboard-and-terminal), the
key table of the help in [Help](../Architecture/HELP.md).

Those runs showed a defect of the probe: it kept what arrived under the name
of the key alone, so `F1` pressed in the panel stood in the report of every
popup after it. It now keeps it per place. Which view receives a key is decided
inside Terminal.Gui, not by the terminal, so the places were proved with the
fixed probe in tmux under WSL, one key left unpressed in one popup to see it
reported as nothing there and as arrived in the next.

A spike rendered a chapter in Terminal.Gui's own `Markdown` view at 40, 80
and 120 columns. Its links, `LinkClicked` and GitHub anchors work; its tables
lose rows, its alerts lose their kind, and it cannot show a match. The help
keeps a view of its own, as decided; the findings are in
[Help](../Architecture/HELP.md#the-view-is-marbass-own).

**P24.2 a chapter is blocks or a refused line.** The task opened with a script
over the pinned Markdig 1.3.2: `UseAlertBlocks` gives an `AlertBlock` with its
kind, `UseAutoIdentifiers(AutoIdentifierOptions.GitHub)` gives `notes`,
`notes-1` and `sizes-links--the-rest`, and a pipe table keeps its rows as
written — a row longer than the header widens the header by an empty cell, a
shorter one is padded with cells holding no paragraph, an escaped `|` is a
literal of its own. Markdig went into `Directory.Packages.props` at that
version. The model and the parser are a new layer, `Guide/`, which uses no
other: the guide is about no entry, path or setting, and both the help's state
and its view will read it. `Layers/LayerReferences.fs`, §1 of
[Coding style](../coding-style-conventions.md#1-layers) and the chain of layers
in `CLAUDE.md`, Overview, State and effects, Testing and Development name it.
`ChapterMarkdownTests` proves each element of the subset, spans nested inside
strong and emphasis, a nested item, a table with an empty cell and with rows
longer and shorter than its header, an escaped `|`, the anchors of punctuated
and repeated headings, each refused construct and the line it is refused at;
`TableTests` and `ChapterNameTests` prove the two types with a rule of their
own. The model is in [Help](../Architecture/HELP.md#the-model-of-a-chapter).

**P24.3 the guide is sixteen chapters.** `docs/user-guide/` holds the contents
and sixteen chapters written against [What works today](WHAT-WORKS.md) and
the key maps, not copied from the old guide. What that guide lacked is in
them: packing, unpacking and browsing archives, running files with `Enter`,
`Alt+R` and `Alt+Shift+R`, `Alt+W`, *Setup* on `F12`, the viewer's `F2` and
`F4`, the files Marbas keeps and the working directory, and the switches and
variables it reads, `TMPDIR`, `TMP` and `TEMP` among them beside those the
plan named. `_documentation/USER-GUIDE.md` is gone and every link to it
points at `docs/user-guide/README.md`, the released note in `CHANGELOG.md`
included. The subset left two things out: links outside the guide, so the
releases page is named in words, and code blocks, so `user-menu.conf` is a
table of lines. Keys no terminal measured delivers are left out and listed
in [Help](../Architecture/HELP.md#the-chapters) for P24.4. `ChapterLinks.broken`
is new in `Guide/`: `ChapterLinksTests` proves a link to a heading and to a
chapter that exist and that do not, and a link found in a heading, strong and
emphasised text, a nested item, a header and a body cell, and both alerts.
The chapters are an `EmbeddedResource` of `Marbas.fsproj` already, under
`UserGuide` in the project, and a `Chapter` record pairs a name with its
blocks. `UserGuideTests` parses each chapter from those resources and finds
no broken link; breaking one anchor and writing one ordered list were each
seen to fail it. §7 of
[Coding style](../coding-style-conventions.md#7-comments-and-documentation)
names the guide as the place for what the user can do, and
[Development](../DEVELOPMENT.md#working-rules) says a visible change changes
its chapter.

**P24.4 a key bound is a row of the guide.** `Ui/Keys/KeySpelling` spells a
Terminal.Gui `Key` as a `KeyName`, new in `Guide/`: modifiers in the order
`Ctrl`, `Alt`, `Shift`, a name for a key without a character, a letter as its
capital, any other character as itself. `KeySpellingTests` proves every
arrow, function key and named key, each modifier, each pair and all three,
and the printable keys bound — letters, digits, `+`, `-`, `*` and `\`.
`Guide/ChapterKeys` names the keys of the `Key` and `Alternative` cells of
every key table, a run such as `Alt+0` … `Alt+9` expanded; `ChapterKeysTests`
proves the cells read and those skipped, several codes in a cell and the
runs. The popups test keys one by one, so `KeyMap` gained `historyKeys`,
`userMenuKeys` and `favoritesKeys`, and `ViewerKeys` and `FindKeys` a `keys`
of their own; the quick search's and the history's `Backspace` became
`KeyMap.takeBackKey`. `GuideKeysTests` compares the two in both directions,
with the keys no measured terminal delivers and the keys Terminal.Gui's
views handle as named lists, and checks that no undelivered key has a row.
Replacing the `Ctrl+R` row of `02-panels.md` by an `Alt+Q` row failed it on
`Ctrl+R` and on `Alt+Q`. The test loads the guide through
`Marbas.Tests/Guide/UserGuide.fs`, which `UserGuideTests` now shares. The
rules are in [Help](../Architecture/HELP.md#the-guide-and-the-key-maps).

**P24.5 a chapter is read by name and laid out to a width.**
`Guide/EmbeddedGuide` answers the embedded chapters and reads one by its
`ChapterName` into a `Chapter`, or a `GuideFault` — `Absent`, or `Refused`
with the parser's refusal; `ChapterName.contents` names `README.md`.
`EmbeddedGuideTests` finds the resource names equal to the `.md` files of
`docs/user-guide/`, and `UserGuideTests` now reads the guide through
`EmbeddedGuide`. `Guide/ChapterLayout.lay` turns blocks into `LaidLine`s of
`Piece`s for a width: paragraphs reflowed, code never split at its spaces,
headings underlined by level and carrying their anchor, items hung and
indented, tables sized to their content and alerts framed, links kept as
`LinkStop`s numbered through the chapter. A first run at 40 columns cut
`Alt+Shift+F11` in the table of `16-platforms.md` while plain text beside it
could still wrap; a column now narrows to its longest word, then to its
longest code, then to one. `ChapterLayoutTests` proves each rule, a chapter
of every block at 80, 120 and 40 columns, and that no line of any chapter is
wider than those widths. The rules are in
[Help](../Architecture/HELP.md#the-layout).
