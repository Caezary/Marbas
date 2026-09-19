# Phase 14 — Finding files

Goal: find entries by name and by what they contain, anywhere from the directory
a panel stands in to every drive the machine has, and do something with what was
found without retyping a path. `Alt+F7` asks the question in a popup of ordinary
size; the answer arrives in a second, full-screen window while the search is
still running, grouped by the directory each hit lives in, with a summary line
saying how much was found and where the walk is now. From there `Enter` puts the
active panel on a hit, `F3` views one, and the whole answer can be sent into the
panel as a temporary listing whose `..` leads back to where the panel stood
before the search. The last answer of the session can be reopened, so the walk
from one hit to the next is `Alt+F7`, `↓`, `Enter` rather than a new search each
time.

What the keys really do was measured before anything was bound to them, the way
P10.7, P11 and P13.1 measured theirs, and P14.1 found two things worth carrying
into the tasks below. `Alt+F7` is delivered by every terminal tried, Windows and
VTE alike, and is nevertheless lost on a GNOME desktop, whose window manager
claims `<Alt>F7` for moving a window before the terminal is sent anything — so
the phase binds `Alt+Shift+F` beside it, the letter-with-`Alt` shape that has
travelled everywhere since P10.7. And `Enter` reaches a full-screen `Window`
with no buttons but not a `Dialog`, where it presses the default button before a
handler sees it. The results window is therefore built like the `F3` viewer
rather than like a popup, and everything it offers is reached by a key rather
than by a button.

Two decisions run through the whole phase. The first is where a running search
keeps its hits: in the results window, not in `AppState`. A search over a whole
disk can find a hundred thousand entries, and turning each of them into a message
through `Update` would pay a state transition per hit for a list nobody is
looking at yet; what reaches `Update` is the finished answer — or the stopped one
— once, when the window closes. The second is that this is the one walk in the
app allowed to follow a directory link, and only when asked. P13.8 stopped every
recursive walk at a link because a link to an ancestor spins; a search that
follows links therefore carries a set of the real paths it has entered and refuses
to enter one twice.

- **P14.1 The keys the terminals deliver** — `Alt+F7` is the classic key, and it
  is also what a desktop takes for "move the window" before the terminal sees it.
  The measurement therefore splits in two, which is new for this checklist: what
  a terminal encodes, which a probe can press, and what a window manager claims,
  which is read out of its own configuration and cannot be pressed without a
  desktop. `_probes/KeyProbe` gains `Alt+F7` and the stand-in the phase would fall
  back on, `Alt+Shift+F`, and asks besides whether `F3`, `Enter` and `Esc` reach a
  full-screen window that is up rather than the panel behind it, since the results
  window wants all three. Four runs — cmder, Windows Terminal, the Windows
  console, and `gnome-terminal` under WSLg, which is a real VTE terminal and the
  half of a Linux desktop that can be reached from this machine — and GNOME's
  defaults read with `gsettings` for the other half. *Done when:* the search is
  reachable by a key that arrives everywhere, and what was measured is a row in
  [Cross-platform](../Architecture/CROSS-PLATFORM.md).
- **P14.2 The question is a value** — `FindQuery` in `Domain` carries the name
  masks, the text, the scope and the options, and makes the combinations that
  mean nothing impossible to build rather than checked later. `NameMasks` reads
  the typed text split at `;`, each mask trimmed: an empty field and `*.*` both
  mean every name, which is what a user of the classic managers expects and what
  `FileSystemName.MatchesSimpleExpression` does not do on its own (`*.*` there
  wants a dot, and passes `Makefile` over); a mask with no `*` or `?` is a
  fragment, so `report` is `*report*`. Masks are matched against the name alone,
  never against a relative path, and always without regard to case — case is an
  option of the text and only of the text. `FindScope` is one of `FromDirectory`,
  `DirectoryOnly`, `MarkedDirectories`, `FromVolumeRoot`, `AllDrives` and
  `PathDirectories`. The options are the text's case, whole words, not
  containing, binary files, archives, directories (on by default), links (on by
  default) and hidden entries (defaulting to what the panel conceals). A query
  with text finds no directories, since a directory contains no text; "not
  containing" and "binary files" exist only with text. *Done when:* the reading of
  masks and every refused combination are covered by tests, and `NamePattern` —
  what `+` and `-` mark with — is left exactly as it is.
- **P14.3 Matching text in a stream** — `ContentMatch.search` reads a file through
  `IFileSystem` buffer by buffer and answers whether the text occurs, without ever
  holding the file whole. The encoding is settled first with the same
  `TextEncoding.detect` the viewer calls, over the same first
  `TextEncoding.ProbeLength` bytes — a byte order mark, then the zero pattern of
  UTF-16, then a NUL meaning Latin-1, then valid UTF-8 — so a UTF-16 file is
  searched as text rather than as bytes that happen to spell nothing, and a file
  is always searched in the encoding the viewer would show it in. Whole words
  means the characters either side of a match are not a letter, a digit or `_`.
  A match that straddles two buffers is found, which the tests prove at every
  offset around the boundary; a match that spans a line break is not looked for.
  Whether a file is binary is not a second guess made here: `TextEncoding` gains
  it as part of what `detect` answers, and the rule is the branch `detect`
  already has — no byte order mark, no UTF-16 pattern, and a NUL in the probe —
  so the search and the viewer can never disagree about a file, and the viewer's
  header can say so whenever that is wanted. A binary file is skipped unless the
  option asks for it, in which case it is searched as the Latin-1 the viewer
  shows it as. "Not
  containing" is the negation of the answer, and a file that cannot be read is
  neither — it is counted as skipped, so an unreadable file never passes as one
  that lacks the text. Each buffer asks the control whether the search was
  stopped. *Done when:* ASCII, UTF-8 and UTF-16 files, the buffer boundary, whole
  words, case, the binary rule and an unreadable file are each covered on
  `MockFileSystem`.
- **P14.4 Walking the disk** — `FileSearch.run` walks the places a scope names
  through `IFileSystem` and reports as it goes: every hit, with its full path and
  the entry the listing would give for it, and every directory it enters, which is
  what the summary line says the walk is doing. `FromDirectory` walks the tree
  below the panel's directory, `DirectoryOnly` that directory's entries alone, and
  `MarkedDirectories` the tree below each marked directory — a marked file is
  ignored, and with no directory marked the scope cannot be chosen. A directory
  that refuses to be read is skipped and counted, the way `DirectoryMeasure`
  counts it. Hidden entries follow the query's option, and a hidden directory
  that is not searched is not walked into either. A directory link is a leaf
  unless the query follows links; followed, it is entered only if the real path
  it resolves to has not been entered before, so a link to its own ancestor
  finishes. The walk answers `Finished` or `Stopped` with its counts of files,
  directories and skipped entries. *Done when:* a nested tree on `MockFileSystem`
  answers its hits for each of the three scopes, the hidden option and an
  unreadable directory are covered, and a cycle through a link terminates on
  `Fakes.LinkFollowing` with each directory reported once.
- **P14.5 The volume, the drives and `PATH`** — the three scopes that do not start
  at the panel. `FromVolumeRoot` starts at the root `Drives.holding` names for the
  panel's location — the disk holding the archive when the panel stands inside
  one — and does not cross into a directory that is itself the root of another
  listed drive, so `/` does not wander into `/media/user/stick`; the mounts
  P13.12 filters out are never listed and so never entered. `AllDrives` walks
  each row of `Drives.list` in its order, the home row excepted, since it lies on
  one of the drives already walked. `PathDirectories` walks the directories of
  `PATH` without descending: `HostEnvironment` gains them, read and split in
  `Adapters` at the platform's separator — `;` on Windows, `:` on Unix — with an
  entry that does not exist left out and a directory named twice walked once.
  *Done when:* each scope is covered on `Fakes.MountedFileSystem` with a second
  mount, and the `PATH` reading is covered for both separators.
- **P14.6 Inside the archives** — with the option on, an archive the walk meets
  is a hit candidate by its own name like any file, and then its index is read
  once through `ArchiveReader` and every entry at every level is matched by name;
  with text, each file entry is streamed into `ContentMatch`, which for a tar.gz
  means decompressing it whole, a price the option says it pays. An archive
  inside an archive is matched by name and not entered. An archive that needs a
  password is skipped and counted rather than asked about, since a disk full of
  them would put one prompt per archive between the user and the answer. A hit
  inside an archive carries the archive's `FileSystemPath` and an `ArchivePath`,
  and a panel already standing inside an archive searches below that level with
  `FromDirectory` and `DirectoryOnly` whether the option is on or not. *Done
  when:* a zip and a tar.gz on `MockFileSystem` answer hits by name and by text at
  several levels, a nested archive is not entered, and a protected one is counted
  as skipped.
- **P14.7 The question popup** — `Alt+F7` opens *Find* over the active panel: the
  masks, the text, the scope as an option selector and the options as check
  boxes, greyed out where P14.2 says they mean nothing — the text options while
  the text is empty, directories while it is not, marked directories while none
  is marked. The hidden box opens on what the panel conceals. A *Last results*
  button, which P14.10 fills, is there and disabled. From a panel showing search
  results (P14.11) the directory the scopes start from is the one that panel
  returns to. `Esc` changes nothing, and what the popup answers is a `FindQuery`
  or nothing. Read off a rendered screen, not off a property of the view. *Done
  when:* the popup answers every scope and option, the greying follows the typed
  text and the marks, and it stays inside an 80-column terminal.
- **P14.8 The results window** — the query runs off the UI thread through a
  `RunSearch` port, and a full-screen window shows what it finds as it finds it.
  It is a `Window` with no buttons, run modally the way `Prompts.showFile` runs
  the viewer, because P14.1 measured that a `Dialog` answers `Enter` with its
  default button before a handler sees it; what the window offers is reached by
  keys, and the key bar at its foot says which.
  Hits reach the window through `app.Invoke` gathered on a short timer rather than
  one call per hit, which is the rough edge the progress window already records.
  The list is grouped by the directory a hit lives in: a header row carrying the
  directory's path — `C:\work\backup.zip\docs` for a level inside an archive — and
  the hits under it, a found directory being a row in the group of its parent.
  Groups appear in the order the walk reached them and are never re-sorted while
  it runs, so a new hit never moves the row under the cursor. A row shows the
  name, the size and the date. The summary line counts files, directories and
  skipped entries and says `Searching: <directory>` while the walk runs,
  `Finished` once it is done and `Stopped` when it was stopped; an answer with no
  hits says so in words. The first `Esc` stops a running search and leaves the
  window up over what it found; the next one closes it. *Done when:* a search
  over a tree shows its hits grouped while it runs, the summary follows the walk
  through all three states, and two presses of `Esc` stop and then close.
- **P14.9 `Enter` and `F3` from the results** — `Enter` on a hit closes the
  window and puts the active panel in the directory holding it with the cursor on
  it, whether it is a file or a directory; a hit the panel conceals turns the
  panel's hidden entries on first, the way `Alt+H` would, since the user just
  asked to stand on it. A hit inside an archive walks the panel into that level of
  the archive. `Enter` on a group header puts the panel inside that directory.
  `F3` on a file hit opens the viewer over the results window — a hit inside an
  archive streamed as P9.11 streams it, asking for the password first — and
  `Esc` in the viewer returns to the results with the cursor where it was; `F3`
  on a directory does nothing. Whatever the window answers goes through `Update`
  as a message, together with the hits and the cursor, which P14.10 keeps. *Done
  when:* `Enter` lands on a file, a directory, a concealed entry, an entry inside
  an archive and a group's directory, each as a test over the update, and `F3`
  views a hit and returns to the list.
- **P14.10 The last results** — `AppState.LastSearch` holds the query, the scope's
  starting place, the hits, whether the walk finished or was stopped and the
  row the cursor was on when the window closed. It belongs to the session and
  not to a tab, is replaced by the next search, and is not written to the
  settings file. The *Last results* button of P14.7 reopens it, carrying the
  count — `Last results (123)` — and is disabled while there is nothing to
  reopen. Reopening checks that every hit still exists, drops the ones that do
  not and says how many went, and the summary line names the query and where it
  ran, so a list from half an hour ago cannot pass for a fresh one. The cursor
  comes back on the row it left, or on the nearest one still there. *Done when:*
  `Enter` on a hit, `Alt+F7` and *Last results* land the cursor on that hit
  again, a file deleted in between is gone from the list and counted, and a new
  search replaces the old answer.
- **P14.11 The results in the panel** — a key in the results window — the bar
  names it, and it is a key rather than a button for the reason P14.1 gives —
  sends the answer into the active panel. `PanelLocation` gains a third case,
  `SearchResults`, holding the hits and the location the panel stood on before,
  and the listing is those hits as entries: the name column shows each hit's path
  relative to where the search started, or the full path when the scope had more
  than one start (`AllDrives`, `PathDirectories`, several marked directories),
  because two `README.md` from two directories must not look like one. `..` goes
  back to the location stood on before, with the cursor where it was then. The
  title says `Find: *.jpg in C:\photos`. Sorting, hidden entries, marking, the
  quick search, the colours and the footer work on it as on a directory. `Enter`
  on a directory goes into that real directory and leaves the results behind;
  `Enter` on a file runs it as P10.3 does, and `F3` views it. `Ctrl+R` checks the
  hits still exist and drops the ones that do not. The panel is not watched,
  since a result can span more directories than a watch should hold. The session
  file keeps the location stood on before, a new tab opened beside it opens on
  that location rather than on the results, and `Alt+F7` from the panel searches
  from that location. *Done when:* the hits show in the panel with their relative
  paths, `..` returns to the directory stood on before with its cursor, and a
  restart and `Alt+F11` both land on that directory.
- **P14.12 Operations on the results in the panel** — the operation paths of a
  results panel come from each entry's own path instead of from the panel's
  directory joined to a name, so the marks can lie in as many directories as the
  search found them in. `F4`, `F5`, `F6` as a move, `F8`, `Shift+F8` and `Alt+Del`
  act on them as they act on a directory's entries, into the other panel's
  directory where they have a target. Everything that needs the panel itself to
  be a directory refuses by name before a popup opens: `F7`, `Shift+F4`,
  `Shift+F1`, `Shift+F2`, `Alt+F6`, `F6` given a bare name as a rename, and any
  key that would use the results panel as the *target* of the other panel's
  operation. What was moved, deleted or wiped leaves the listing afterwards and
  the marks of what failed stay. Hits inside archives refuse in this task with a
  sentence saying so, which P14.13 removes. *Done when:* marked hits from three
  directories copy, move, delete and wipe in one batch, each refusal is covered
  by a test over the update, and the listing afterwards holds what is still there.
- **P14.13 Operations on the results inside archives** — the marks of a results
  panel may lie on the disk and in several archives at once, and every operation
  so far assumes one source place. The operation therefore splits its targets by
  the place holding them — the disk, or one particular archive — and runs each
  group through the path that place already has: copying on the disk, extracting
  out of an archive for `F5`, the extract-then-drop of P9.13 for `F6`, and
  removing entries for `F8`. All of the groups run in one batch behind one
  progress window, `Esc` stopping the whole of it, a password is asked once per
  archive that needs one, and the capability gate is asked per group: a group in
  a kind that cannot be changed is refused by the archive's name, and the rest of
  the batch still runs. *Done when:* a mark on the disk, one in a zip and one in
  a rar copy out together, moving the same three moves two and refuses the rar
  by name, and the listing afterwards holds what is still there.
- **P14.14 The documentation** — `Architecture/FINDING-FILES.md` says what a query
  is and why `*.*` is read the classic way, how text is matched and why binary
  files are an option, each scope and what it refuses to cross, why the hits live
  in the window and not in the state while the walk runs, the last results and
  the results panel; [Links](../Architecture/LINKS.md) gains the one walk that may
  follow a link and the set that keeps it from spinning;
  [State and effects](../Architecture/STATE-AND-EFFECTS.md) gains `LastSearch` and
  the third `PanelLocation`; [UI and input](../Architecture/UI-AND-INPUT.md) gains
  the two windows and their keys; [Cross-platform](../Architecture/CROSS-PLATFORM.md)
  gains the `Alt+F7` row, the `PATH` separator and the mount boundary; and the
  user guide gains the command. *Done when:* the phase is described where the
  rest of the design is, not only in this plan.

## Left for a later phase

- **History.** The query popup opens empty every time in this phase. Remembering
  the last masks, text, scope and options in the settings file, and a history of
  earlier masks and texts to pick from, is its own phase — and the natural home
  for keeping `LastSearch` across a restart as well.
- **Excluding masks** in the classic spelling, `*.* | *.bak`.
- **A depth limit** on the walk.
- **Regular expressions**, and text that spans a line break.
- **The viewer opening at the match**, with the text highlighted, and a line
  preview of the match in the results window.
