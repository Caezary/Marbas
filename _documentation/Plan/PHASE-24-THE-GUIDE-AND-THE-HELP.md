# Phase 24 — The guide and the help

Goal: the user guide is rewritten as short chapters, each a few sentences, a
table of keys and the rules that qualify them. The same Markdown files are read
on GitHub and, built into the binary, shown full screen under `F1`. `F1` opens
the guide where it is relevant: in a panel at the command used last, in the
viewer at the viewer's chapter, in a popup at that popup's section. A test
fails when a key the code binds is missing from the guide, or when the guide
names a key nothing binds.

Eight decisions run through the whole phase.

**The guide is a directory of chapters.** `docs/user-guide/` holds
`README.md`, the contents, and one file per chapter, numbered so GitHub lists
them in reading order: `01-getting-started.md`, `02-panels.md`, … The chapters
are: getting started; panels, the cursor and sorting; marking; tabs; file
operations; directories and favourites; archives; the clipboard; finding
files; running files and the user menu; the viewer; settings; history; the
command line and the environment; the files Marbas keeps; differences between
platforms. `_documentation/USER-GUIDE.md` is removed and every link to it is
changed. A GitHub wiki was rejected: it is a separate repository, not
versioned with the code and not checked by CI.

**A chapter is a description, a table and rules.** One or two sentences say
what the feature does. The table has three columns, `Key`, `Alternative`,
`Action`, and an action fits on one line. Rules and exceptions follow as short
bullets. A platform difference is an alert beside the key it concerns, and is
also collected in the platform chapter. A key no measured terminal delivers,
such as the grey `+` and `-` with `Ctrl` or `Alt`, is left out.

**The guide is written in a subset of Markdown.** The subset is headings `#` to
`###`, paragraphs, bullet lists, tables without column alignment, `inline code`,
`**bold**`, `*italic*`, links to a chapter or a heading, and the alerts
`> [!NOTE]` and `> [!WARNING]`. GitHub renders all of it, and the help view
needs to draw nothing more. A test fails on anything outside the subset.

**Markdig parses it, behind a model of Marbas's own.** GitHub renders with
cmark-gfm; Markdig implements the same CommonMark and GFM rules, its pipe
tables and alerts included, and computes a heading's anchor the way GitHub
does. A parser of our own would differ from GitHub on edge cases — `*` and `_`
inside words, `\|` in a cell, escapes — without any test noticing.
FSharp.Formatting was rejected for the compiler service it brings and for
having neither GFM tables nor alerts; FParsec would still leave the grammar to
be written. Markdig is confined to one module, which maps its syntax tree onto
an F# union and refuses every node outside the subset; nothing else sees a
Markdig type.

**The Markdown is the only source.** The chapters are an `EmbeddedResource` of
`Marbas.fsproj`; no step generates code from them or them from code. Generating
the tables from `KeyMap` was rejected because the descriptions would become
strings in code, and because Terminal.Gui's views and several popups handle
keys outside `KeyMap.bindings`. A third format producing both was rejected as
tooling out of proportion. The help reads the resources when `F1` is pressed,
not at start.

**A test holds the guide and the key maps together.** Each key of
`KeyMap.bindings`, `ViewerKeys`, `FindKeys` and the keys of the history, user
menu and favourites popups is spelled the way the guide spells it (`Ctrl+PgUp`,
`Ins`, `Alt+←`). The spellings are compared with the `Key` and `Alternative`
cells of every table, in both directions. The keys Terminal.Gui's views handle
themselves (`↑`, `↓`, `PgUp`, `PgDn`, `Home`, `End`, `Tab`, `Enter`, `Esc`) are
one named list the test accepts without a binding. A key is checked against the
guide as a whole rather than against the chapter of its context; that catches a
key removed or added without its row, which is the drift that matters.

**The help is a viewer of linked text, full screen.** The first page is the
contents. A link is drawn in a colour of its own, `Tab` / `Shift+Tab` move to
the next or previous link on screen, `Enter` follows it, and `Backspace` or
`Alt+←` goes back. `F7` / `Shift+F7` search the chapter on screen as the viewer
searches a file, and `Esc` / `F10` close. A side panel listing the chapters was rejected: at 80 columns it leaves
about 55 for the text, and the three-column tables break. The help is a view of
its own in `Ui/Help/`, not a mode of `FileViewer`, which works on the bytes of
a file. Parsing the subset and laying it out to a width are pure functions:
paragraphs reflow on resize, and a table too wide for the screen wraps inside
its cells.

**Links are GitHub's links.** A link is `[text](chapter.md#heading)`, and the
anchor of a heading is computed the way GitHub computes it: lower case, spaces
to `-`, punctuation dropped. One link therefore works on GitHub and in the help.
A test fails on a link to a chapter or a heading that does not exist.

- **P24.1 Measure F1 and the keys of the help** — `_probes/KeyProbe` checks
  `F1`, `Tab`, `Shift+Tab`, `Backspace`, `Alt+←`, `F7` and `Shift+F7` in a
  panel, in the viewer and in a popup, in Windows Terminal, the Windows console,
  cmder and gnome-terminal under WSL. gnome-terminal binds `F1` to its own help
  by default. A key that does not arrive gets a spare, chosen the way P22.7
  chose them. *Done when:* what arrives where is in
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) and the key table of the
  help, spares included, is written down.
- **P24.2 Parse the Markdown subset** — Markdig in `Directory.Packages.props`,
  the model of the subset and the module mapping Markdig's tree onto it, a
  function from text to blocks or to the line it refuses. The task opens by
  confirming that the pinned Markdig version has alerts and GitHub anchors.
  The model names every level instead of nesting lists:
  - a `Span` is text, code, strong, emphasis or a link; `Phrase` is a run of
    spans, the content of a paragraph, a heading, a cell and an item;
  - an `Item` is a phrase and the items nested under it, so a bullet holds no
    table, heading or second paragraph;
  - `Prose` is a paragraph or a list of items, and is all an alert can hold;
  - a `Table` is a header `Row` and body rows of `Cell`s; its constructor is
    private and `Table.create` pads or cuts every row to the header's width,
    as GFM does, so a row of another width cannot be built;
  - a `Heading` is a record of its level, its anchor and its title;
  - a `Block` is a heading, prose, a table or an alert of a kind and its prose.

  `Text` and `Code` carry a `string` on purpose: the text is parsed, and
  nothing after the parser interprets it. Column alignment (`:---:`) is
  outside the subset. *Done when:* every element of the subset, nesting of
  spans, a nested item, a table with empty cells and with a row longer than its
  header, an escaped `|`, anchors of duplicate and punctuated headings, and
  each refused construct are proved on strings.
- **P24.3 Rewrite the guide as chapters** — `docs/user-guide/` written to what
  the application does today. It fills what the old guide lacks: archives
  (`Shift+F1`, `Shift+F2`, browsing inside one), running files (`Enter`,
  `Alt+R`, `Alt+Shift+R`), `Setup` on `F12`, `Alt+W`, and the viewer's `F2` and
  `F4`. Also: the files Marbas keeps (`settings.conf`, `favorites.conf`,
  `history.conf`, `user-menu.conf`, `marbas.log`, the working directory) and the
  switches and variables it reads (`--debug`, `--version`, `EDITOR`, `PATH`,
  `PATHEXT`, `XDG_CONFIG_HOME`, `XDG_DATA_HOME` and their `_DIRS`). The old file
  goes; the links in `README.md`, `CHANGELOG.md`, `DEVELOPMENT.md`, the
  architecture, the plans and the journals point at the new one. §7 of
  [Coding style](../coding-style-conventions.md#7-comments-and-documentation)
  names the guide as the place for what the user can do. *Done when:* a test
  parses every chapter with the P24.2 parser and resolves every link, and the
  chapters render on GitHub.
- **P24.4 Hold the guide and the key maps together** — the spelling of a `Key`
  as the guide writes it, and the test of the fifth decision. *Done when:* the
  spelling is proved for every modifier, arrow, function key and printable key
  bound, and the test fails on a binding removed from a table and on a row
  whose key nothing binds.
- **P24.5 Embed the guide and lay it out to a width** — the chapters as
  resources, read by name, and the layout: blocks to styled lines for a width,
  with paragraphs reflowed, list items indented, tables sized to their content
  and wrapped inside cells when wider than the screen, alerts framed, and links
  kept as spans with their target. *Done when:* the layout is proved at 80, 120
  and 40 columns on a chapter with each block, and the resource names match the
  files.
- **P24.6 Show the help** — the help view: the contents first, scrolling as
  in the viewer, the link under the cursor highlighted, `Tab` / `Shift+Tab`,
  `Enter` to follow, back to where the link was left, and a bar of its own.
  *Done when:* following a link to a chapter and to a heading, going back, and a
  resize keeping the line in view are proved on the help's state, and the view
  is rendered and driven through a real `Application`.
- **P24.7 Search the help** — `F7` and `Shift+F7` with the viewer's search
  popup and its options, matching the laid-out lines of the chapter on screen;
  a link followed starts the next search in the new chapter. *Done when:* a
  match is found, highlighted and scrolled into view, and reaching the end of
  the chapter without a match is reported, proved on state and rendered.
- **P24.8 Open the help with F1 where the user stands** — `F1`, labelled
  `Help` on the bar of the panels and of the viewer, opens the help on an
  anchor. In a panel the anchor is the section of the last command a binding
  of `KeyMap.bindings` raised in the session; moving the cursor and the quick
  search are not commands, and before the first command the contents open. In
  the viewer it is the viewer's chapter, and in a popup that popup's section.
  The messages, the viewer and the popups map to their anchors in one table in
  `Ui/Help/`. *Done when:* a test resolves every anchor of the table and finds
  an anchor for every message `KeyMap.bindings` raises, the last command is
  proved on state, and `F1` is driven through a real `Application` from a
  panel before and after a command, the viewer and at least two popups.

Every task updates [What works today](../Progress/WHAT-WORKS.md),
[UI and input](../Architecture/UI-AND-INPUT.md) and a new
[Help](../Architecture/HELP.md) in the architecture for what it adds to them;
[Development](../DEVELOPMENT.md) says that the guide is compiled into the
binary and is held to the subset.
