# Phase 22 — What the manual pass found

Goal: every defect the manual pass of 2026-10-12 wrote into
[Rough edges](../ROUGH-EDGES.md) is closed or narrowed to what a library will
not let through, and three places that had grown uneven are written one way:
the prompts of `Adapters`, the transitions of `Update` and the effects of
`Shell`. The phase adds no feature. It pays for P21.18, which walked the published binaries by
hand and found that a program on Linux is neither coloured nor started as one,
that a key in the user menu ends the session, that a directory loses its time
through an archive and a file its hour through a zip, that a backslash becomes
a level, that the rule under a listing stops short of its border, that one
terminal cannot open a tab, that three flags of the properties popup do
nothing on Linux, and that a missing opener is reported in the runtime's
words.

Three rules decide the order. What ends a session or hides a whole kind of
entry comes first. The archive findings come together, because they change the
same two functions and are timed with the same probe. The structural tasks come
after the fixes, so that they move code that has stopped changing, and the walk
closes the phase, against the binaries it ends with.

Three decisions run through the fixes.

**A defect the suite could not see gets a test that can.** Each finding passed
every test because the fake answered what the real system does not:
`MockFileSystem` keeps the mode and the attributes a test sets, and no test
pressed `Del` on the bottom row. Where a mock can say it, the failing test is
on `MockFileSystem`. Where only a real disk can — the executable bit — it is an
integration test in `Marbas.Tests/Integration/Disk`, which runs on both legs of
CI and skips on the system the question does not exist on, as the tests on
symbolic links do.

**The platform is a value.** Nothing below `Adapters` asks which system it runs
on. A fix that differs by platform reads `FileSystemPath.platformOf` off the
`IFileSystem` it already holds, or is handed the answer by
`Dependencies.create`, so both branches are tested on either system.

**The hot paths are timed.** A key in a panel, listing a directory, packing and
unpacking are four of the paths §10 of
[Coding style](../coding-style-conventions.md) names. P22.2, P22.3 and P22.4
change the last three and P22.11 the function every key goes through, and each
is timed before and after in [Measurements](../Progress/MEASUREMENTS.md).

## Stage 1 — What ends a session, what hides a program

- **P22.1 The bottom row of the user menu can be removed** — `fill` in
  `UserMenuDialog` puts the cursor back on the index it had before the list was
  replaced; after `Del` on the bottom row that index is past the end, and after
  `Del` on the only row there is none, and `ListView` throws. A test in
  `UserMenuDialogTests` first removes the bottom row and the only row and fails
  on the exception. Then the cursor is held inside the list it is given, and
  left unset on an empty one. The other popups that take a row away while they
  are open — the history, the settings — are read for the same shape and given
  the same two tests where they have it; the panel and the results window
  already clamp. *Done when:* the bottom row and the only row are removed in a
  test, in the user menu and in every popup found to share the shape, the
  cursor lands on the row above, and the entry in
  [Rough edges](../ROUGH-EDGES.md) is gone.
- **P22.2 The executable bit is read on Linux** — `DirectoryListing` and
  `LaunchProcess` ask `IFileSystemInfo.UnixFileMode`, and the wrapper of
  System.IO.Abstractions 22.2.0 answers `None` for every file. A disk test
  first lists a file it has given the bit to and fails on the Linux leg. The
  task then finds out whether a later release of the package passes the
  question on: that is the fix that costs nothing, since the runtime already
  holds the mode of every entry it enumerates. If none does, the mode is read
  through `fs.File.GetUnixFileMode`, on Unix alone — which is one more question
  to the system per file, on the path the rule forbids exactly that. So a
  directory of ten thousand files is listed under WSL before and after, and if
  the price is out of proportion the task stops there, writes the numbers up
  and leaves the choice — the mode read only for the rows on screen, or the
  runtime's enumeration reached past the wrapper — to be made with them. The
  launch is one file and takes the direct question either way. *Done when:* the
  disk test passes on Linux and is skipped on Windows; a file with the bit is
  drawn as a program, started by `Enter` and refused by `Alt+Shift+R` with the
  platform's reason, by the published binary driven in tmux under WSL; the
  listing is timed before and after; and the entry in Rough edges and the note
  in [What works today](../Progress/WHAT-WORKS.md) are gone.

## Stage 2 — What an archive keeps

- **P22.3 A directory keeps its time through an archive** — packing hands the
  sink `None` for every directory it walks on the disk, and the zip sink drops
  the moment even when it is given one, so each directory entry is stamped with
  the moment of packing. Unpacking creates a directory and never dates it.
  Tests on `MockFileSystem` first pack a dated tree and read the directory
  entries back, and unpack an archive whose directory entries are dated, and
  fail. Then the walk hands over the directory's own time, every writer puts it
  on the entry, and unpacking dates each directory once everything under it is
  written — a file made inside it afterwards would move the time again, so the
  directories are dated last and from the deepest up. *Done when:* a directory
  keeps its time packed into each kind Marbas writes and unpacked from each
  kind it reads, an entry with no time of its own leaves the directory as the
  system made it, a tree of many directories is packed and unpacked in the
  time it took before, and the entry in Rough edges is gone.
- **P22.4 A zip keeps the moment** — Marbas writes the local two-second field
  of a zip entry and nothing beside it, and reads that field alone. A tool that
  turns the local time back with today's offset is then an hour out across a
  change of daylight-saving time, which is what Explorer and 7-Zip did to a
  file of February packed in October; and the zip 7-Zip wrote, with the exact
  moment in an NTFS extra field, was unpacked here an hour late. A spike first
  settles which field each library hands over and which it can write — the
  extended timestamp (`0x5455`), the NTFS field (`0x000A`) that 7-Zip wrote, or
  both — and which of them Explorer, 7-Zip and `unzip` each honour. Then a
  test on `MockFileSystem` packs a file dated in winter and reads the field
  back, and another unpacks an entry whose local field and exact field
  disagree and expects the exact one. *Done when:* both tests pass; a file of
  the fixture packed here comes out of 7-Zip and `unzip` at `04:05:06`, and
  out of Explorer too unless the spike showed it honours neither field;
  7-Zip's zip is unpacked here at the same; what a library or a tool will not
  write or read is said in Rough edges and the rest of the entry is gone.
- **P22.5 A backslash stays in the name** — on Linux `a\b.txt` is one file, and
  `ZipEntry` cleans its name into `a/b.txt` as it is written, so the archive
  gains a level the disk never had. The entry in Rough edges has stood since
  P9.7 and the pass confirmed it. A test that runs where a backslash is part
  of a name first packs such a file and reads one entry of two levels back. The
  task then looks for a way to hand `SharpZipLib` the name as it is — a name
  transform, or an entry made without the cleaning — and checks what `unzip`
  makes of a backslash in a name before it writes one. Windows is untouched:
  there a backslash is a separator and is stored as `/`. *Done when:* the entry
  is stored under its own name and listed as one file here and by `unzip`, or
  the entry in Rough edges says what in the library or in the format stands in
  the way.

## Stage 3 — What is seen and pressed

- **P22.6 The rule joins the border** — the `Line` of `FilePanel` lies inside
  the frame, the border is drawn by the frame's adornment, and the two never
  meet on one canvas, which is where Terminal.Gui joins lines. The task tries
  the ways there are — the frame drawing the rule on its own line canvas, the
  rule drawn onto the border's — and keeps the first that gives `╟────╢` on a
  focused panel and `├────┤` on the other. The look is settled by rendering the
  screen, not by reading a property. *Done when:* the rendered screen shows the
  joints at both ends in both panels, at several widths and after a resize, a
  person has looked at it in a terminal, and the entry in Rough edges is gone;
  or it says which of the ways was tried and why none holds.
- **P22.7 A tab opened and closed with a letter** — `Alt+F11` puts
  gnome-terminal's window in full screen under WSLg, and the command has no
  other key; `Alt+Shift+F11` has none either. Each gets a second binding of the
  shape that has travelled everywhere so far, a letter with `Alt` and the same
  letter with `Alt+Shift`, chosen from those `KeyMap` leaves free and pressed
  with `_probes/KeyProbe` in the five terminals of P13.1 before it is bound.
  *Done when:* both stand-ins are in `KeyMap` and its tests and in the
  [user guide](../USER-GUIDE.md), each arrived in all five terminals, a tab is
  opened and closed with them in gnome-terminal under WSLg, and the entry in
  Rough edges is gone.
- **P22.8 The properties popup offers what the platform keeps** — on Linux
  hidden, system and archive are accepted and dropped: .NET sets none of the
  three on Unix and refuses none. The flags a platform keeps become a function
  of `EntryProperties` over the platform, the popup shows those alone, and
  `write` folds only them. *Done when:* the popup built for Unix shows
  read-only and nothing else, the one built for Windows shows all four, each
  in a test on either system, and the entry in Rough edges is gone.
- **P22.9 A missing opener is named** — on a Linux without `xdg-open` a
  document, a directory handed to the file manager and a drive handed over from
  the popup are all reported as *Could not start* with the runtime's sentence
  after it. `LaunchError` gains a case for an opener that is not installed, the
  adapter answers it when the start it described was the opener's and the
  system says the file is not there, and `LaunchErrorMessage` says in one
  sentence that nothing on this system opens files and what to install.
  *Done when:* the case is worded and held by a test, the description of the
  start says which program it is so that the mapping is tested without a
  process, and the entry in Rough edges is gone.

## Stage 4 — The structure

- **P22.10 Every prompt names what it creates** — `Adapters/Prompts.fs` runs
  each popup through `running app create read`. Eight prompts bind the function
  that builds the dialog to a local name first, as `chooseFavorite` does with
  `createFavourites`; twelve pass it as a lambda at the call, some of them
  spread over three lines: `chooseDrive`, `askPattern`, `askTarget`,
  `askPassword`, `chooseCodePage`, `editProperties`, `askArchive`, `askLink`,
  `askHandler`, `editPrograms`, `chooseHistory` and `chooseTab`. All twenty
  take the shape of `chooseFavorite`: what the request has to be turned into
  first, then a local `create…` function named for the dialog it builds, then
  `running app create… <what is read>` on one line. The two locals called
  `asking` and the two called `createDialog` are renamed with the rest, and
  the names are spelled one way. Nothing else moves: no prompt changes what it
  builds or what it answers. *Done when:* no call of `running` in the file
  takes a lambda, every local it is handed begins with `create`, the suite
  passes unchanged, and — since no test reaches `Adapters` — each popup is
  opened once in the walk that closes the phase.
- **P22.11 `Update.transition` reads as a table** — `transition` matches some
  hundred and fifteen messages. Most arms are one line; about thirty-four run
  to several, `DeleteRequested`, `TargetEntered`, `ViewRequested` and
  `ArchiveEntered` among them, and the long ones hide the short ones between
  them. Each longer arm becomes a private function above the match, named for
  what it decides and taking `fs`, the state and what the message carries, as
  `editing` and `openWithHandler` already are, so that every arm is one line
  after its pattern and the match is an index of what each message does. Where
  it reads so, an arm is a pipeline and not a nest of parentheses:
  `state |> readTabQuietly fs |> stay` for a state on its way to a result,
  `PromptForPattern mode |> request state` for an effect on its way to one —
  the rule §4 of [Coding style](../coding-style-conventions.md) already gives.
  A pipeline Fantomas breaks over several lines is an arm of several lines
  again, and takes a name instead. Messages told apart by what they carry —
  `MeasureFinished`, `EditorStarted`, `CommandFinished`, `ClipboardRefused` —
  keep an arm for each pattern or hand what they carry to one function that
  matches it, whichever is the shorter. No transition changes what it answers.
  `Update.fs` stands at 916 lines and each function taken out adds two or
  three; if it passes a thousand, the functions leave it by feature — the
  transfers, the archives, the clipboard — as modules compiled before it. A
  pipe is inlined and a named function is a direct call, so nothing here
  should cost a key anything, and the rule asks for the number all the same.
  *Done when:* every arm of `transition` is one line after its pattern, the
  suite passes with no test added, removed or changed, `Update.update` over a
  cursor moved is timed before and after, §4 of Coding style says that a match
  which only hands out work has one-line arms, and
  [Overview](../Architecture/OVERVIEW.md) names any module that moved.
- **P22.12 `Shell.interpret` reads the same way** — `interpret` matches some
  fifty effects, about thirty of them over several lines. Each becomes a
  private function above the match that takes `deps`, `dispatch` and what the
  effect carries, as `showUserMenu` and `movePasted` already are, and what is
  left of an arm is a pipeline where one reads:
  `deps.Processes.StartEditor editor path |> EditorStarted |> dispatch`. A
  message of several fields is not a function a value can be piped into —
  `LaunchFinished(request, outcome)` — and keeps its parentheses. Taking the
  arms out puts what they repeat side by side: six prompts ask, remember what
  was typed and dispatch (`PromptForArchive`, `PromptForLink`, `ShowHandlers`,
  `PromptForTarget`, `PromptForFileName`, `PromptForPattern`), four arms hand
  a refusal of the clipboard on as `ClipboardRefused`, and two load the
  favourites and report the same failure. Each group becomes one function
  where that comes out shorter and no less plain, and is left as it is where
  the members differ in more than a name. No effect is interpreted
  differently. An effect is interpreted once for a command, never once for an
  entry, so nothing is timed. *Done when:* every arm of `interpret` is one line
  after its pattern, and the suite passes with no test added, removed or
  changed.

## Stage 5 — Walked again

- **P22.13 The steps that failed, by hand** — the published binaries of this
  phase are walked through the steps of
  [The manual pass](../Architecture/MANUAL-PASS.md) that the first pass
  answered differently — W4, W15, W19, L2, L4, L5, L19, L29, L30, L33, L37 and
  L38 — and through L25 with the archives of `zip` and 7-Zip, which the first
  fixture did not hold. Two steps join the pass for good: a row removed from
  the bottom of the user menu, and one popup of every kind opened and closed.
  What still needs a Linux desktop stays as it is. *Done when:* each of those
  rows of [Cross-platform](../Architecture/CROSS-PLATFORM.md) carries the new
  date and what was seen, the two rows on a started program are walked on
  Linux, *The passes so far* has its second row, and whatever failed again is
  still an entry in Rough edges.

Every task updates [What works today](../Progress/WHAT-WORKS.md) where the
behaviour changes, its row of
[Cross-platform](../Architecture/CROSS-PLATFORM.md) where the pass wrote the
defect into one, and [Rough edges](../ROUGH-EDGES.md), where its entry goes or
is narrowed.

## Left for a later phase

- **A second key for the drive popups and the link** — GNOME keeps `Alt+F1`,
  `Alt+F2` and `Alt+F6`, read from its configuration in P14.1. The pass had no
  desktop to press them on, so the entry stands as it was; `Alt+D` is still
  kept free for it.
- **The rows that need a Linux desktop** — the GNOME key grabs, the rows marked
  for KDE and Xfce, the window a file manager opens, a file manager's
  *Restore* and a desktop's archive manager wait for a machine that has one.
- **A program the system picks on Windows** — a start through a file's
  association can still write over the panels under ConEmu. The pass did not
  meet it and P21.6 left it narrowed to that.
