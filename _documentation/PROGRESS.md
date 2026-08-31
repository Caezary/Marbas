# Progress

Status as of 2026-08-30. Phases and task numbers refer to [PLAN.md](PLAN.md).

## Where we are

**All nine phases are complete** (46 of 46 tasks). Marbas
runs as a full-screen two-panel file browser: both panels list a real directory,
the keyboard moves between and inside them, entries can be marked one by one or
by wildcard for later operations, a status line describes the entry under the
cursor, and failures surface as dialogs instead of crashes. Every function key
does its job — `F3` views the file under the cursor and `F4` opens
it in the system editor, `F5` copies and `F6` moves or renames into the other
panel, `F7` makes a directory, `F8` deletes to the trash and `Shift+F8` for
good — the operations all acting on the
same operation targets, with copying and moving running off the UI thread behind
a progress window that `Esc` stops. Since P7.1 the screen wears the classic
colours: blue panels with cyan entries, a cyan cursor bar, yellow marks and a
cyan function-key bar, and since P7.2 each panel sorts by name, extension, date
or size in either direction, saying so in its title. P7.5 put every key binding
into one table: the bottom bar is a fixed row of twelve slots read out of it,
one per function key, and holding `Ctrl` or `Alt` swaps the commands on those
slots for the ones those modifiers reach without moving a key name. Since P7.3 each panel
also decides on its own whether the hidden entries — dotfiles and whatever the
OS marks hidden or system — are listed at all. P7.4 closed the phase: where each
panel stands, how it sorts and whether it conceals are written to a config file
when the app quits and read back when it starts, so a session survives a
restart. P8.1 put a number on the suite and a workflow around it, and P8.2 made
both hold on Linux as well as on Windows. P8.3 closed the plan: two publish
profiles turn the project into one self-contained, single-file executable per
operating system, and a tag builds both on their own runners and hangs them off
a GitHub release.

**Since the plan closed, one round of panel work** answered five things a user
met straight away: the cursor no longer falls out of a panel at its last entry,
the left and right arrows jump by page instead of switching panels, entries are
coloured by what they are, the bottom row of a panel is separated by a rule and
says what the directory holds, and typing a letter searches the panel. **A
second round widened marking**: holding `Shift` and running an arrow marks or
unmarks an entry per step, in the direction of the arrow, and a shifted page
key does the same to the whole page at once. **A third round fixed two things
about names carrying an underscore**: they now sort where a file manager puts
them — first, ahead of the letters — and they are drawn with the underscore in
them, which Terminal.Gui had been reading as a hot key marker. **A fourth round
rebuilt the file viewer.** It had buffered the whole file, capped it at a
megabyte, called every UTF-16 file a binary, taken the page keys but not the
arrows, and rested on a `TextView` that 2.4.17 deprecated. It is now a
frameless full-screen window that reads only the rows it shows, over a file held
open and a line index scanned forward on demand; it settles the encoding before
deciding anything, always opens as text, and puts wrapping and the hex dump on
`F2` and `F4`. **A fifth round took the popups in hand.** They wore
Terminal.Gui's own defaults — a heavy frame and brackets in glyphs a console
font does not carry, and a shadow under each button that pushed the buttons past
the frame and squeezed the prompt's own label and input field off the dialog.
Every popup now wears a double frame, centred buttons, no shadow and the
classic colours, message boxes included; `Esc` closes any of them again, which
the quit key had quietly taken away; and `F7` opens on the name of the last
directory made, remembered across a restart. **A sixth round widened `F4`.** It
had one editor, `$EDITOR` or `notepad`, and it did nothing at all on a
directory. There is now a default editor and an editor per extension, both kept
in the settings file and both edited from a settings popup on `F12`;
`Shift+F4` creates a file in the panel's directory and opens it in the editor
its extension asks for; and `F4` on a directory opens a properties popup — the
read-only, hidden, system and archive flags and the three timestamps — writing
back only what it was given and re-reading the panel afterwards. The same round
moved every timestamp in the app from `DateTime` to `DateTimeOffset`, so a
moment always says which offset it was read in. **A seventh round was
housekeeping.** `Ui` had grown to forty-three modules in one folder, so they
were split into eight — `Theme`, `Format`, `Keys`, `Messages`, `Panels`,
`Viewer`, `Dialogs` and `App` — each folder a namespace, with the tests
following into the same folders on both sides of the unit/integration fence.
Nothing the application does changed: the compile order simply became the
folder order, and every dependency inside `Ui` now points at a folder above it.

**An eighth round emptied `Program.fs`.** It had become initialisation, the
wiring of the dependencies, the implementations of those dependencies and four
separate questions about which operating system this is. The real
implementations of `Shell.Dependencies` now live in `Adapters/` — the prompts,
the recycle bin, the editor process, the batch runner and the modifier poll —
the platform and the environment are read once into `HostEnvironment` and
carried as a value, and `Dependencies.create` is the single place where a
Windows function or a Unix one is chosen per port. `Program.fs` is down to
starting the application. Nothing the application does changed.

**A ninth phase opened, and archives can now be created.** `Shift+F1` packs the
operation targets of the active panel — the marks, or the entry under the
cursor — into one archive in the other panel, the way copying works. A popup
asks first: the target, offered as the other panel's path plus a name deduced
from what is being packed, the format (zip, tar.gz or 7z), the compression level and
a password. Choosing a format renames the extension in place; a password locks a
zip with AES-256 and is greyed out for a format that cannot carry one. The
writing itself streams every entry through `IFileSystem`, so it runs behind the
progress window with `Esc` stopping it — and a stopped run deletes the
half-written file and keeps every mark. An archive that already exists asks
before it is replaced, one that would land inside what it packs is refused
outright, and when the batch finishes the other panel is re-read with the cursor
on the new archive.

**The reading half of the phase now exists underneath the keys.** Every archive
kind Marbas will ever handle has a name, an extension and a record saying
whether it can be read, created or changed, and the formats the pack dialog
offers are that table's creatable half rather than a second list. `ArchiveListing`
reads one level of an archive as the same entries a directory gives — sizes,
times, and directories synthesised where the archive stores none — and
`Archive.extract` takes named entries back out into a directory, a directory
target bringing the tree below it, times restored, entries that would climb out
of the target refused, each entry reported through the same batch control that
copying uses.

**`Shift+F2` now empties an archive into the other panel.** It takes the same
operation targets every other key takes — the marks, or the entry under the
cursor — and unpacks each of them into the other panel's directory behind the
progress window. Before the batch starts it asks what it has to ask: a
protected archive asks for its password once, in a popup that hides what is
typed, and every top-level entry already in the target directory asks the
ordinary five-answer overwrite question. A target that is no archive Marbas can
read — a plain file, or a `tar` whose reader has not landed yet — reports itself
in the failures dialog instead of doing nothing, and both panels are read again
afterwards.

**A panel now says where it stands with a type, not with a path.** A
`PanelLocation` is a directory on disk or a level inside an archive, and
`PanelState.Path` is one of those. Nothing puts a panel inside an archive yet —
that is the next task — but every operation had to stop assuming a disk path
and ask the location for one instead: the operation paths of an archive panel
are empty, `F5`, `F6`, `F7`, `Shift+F1`, `Shift+F2` and `Shift+F4` check for a
directory before they open a dialog, and a reload leaves such a panel as it
stands. The panel title spells a place inside an archive
`C:\work\backup.zip\docs`, and the session file keeps the directory *holding*
the archive, so a restart lands somewhere that is certainly still there. On
disk nothing changed.

**`Enter` now walks into an archive.** A readable archive under the cursor
opens in the panel it was pressed in and lists its top level like a directory —
`..` first, then what the archive holds, in the panel's own sort order and
concealing what it conceals. Directories inside walk in, `..` walks back out a
level with the cursor on the level just left, and `..` at the top leaves the
archive with the cursor back on the archive file. The title says where the
panel stands (`C:\work\backup.zip\docs`), `Ctrl+R` re-reads the archive,
marking, the search, the colours and the footer work as they always did, and a
damaged archive says so in a message box instead of opening. `F3` views a file
inside an archive by streaming that entry into the viewer — asking for the
password first when the archive carries one — while `F4` is the one key that
refuses, because editing an entry in place would mean rewriting the archive
when the editor returns.

**`F5` now copies out of an archive.** From a panel standing inside one it
takes the same operation targets every other key takes — the marks, or the
entry under the cursor — and extracts them into the other panel's directory, a
marked directory bringing the tree below it. It runs behind the progress window
titled "Copying" with `Esc` stopping it where it stood, asks the archive's
password once when there is one, asks the ordinary five-answer overwrite
question for every entry that would land on something already there, and
finishes by re-reading both panels with the marks of whatever did not make it
still on — the same walk `F5` between two directories follows. The archive
itself is only read: its bytes are the same afterwards.

**And now the ordinary keys change an archive.** From a panel standing inside a
zip, `F5` from the other panel adds its targets at that level, `F8` removes the
targets of the archive panel, `F7` makes a directory entry, and `F6` moves in
either direction — out of an archive it extracts first and drops the entry only
for what arrived, into an archive it deletes a source only once that source is
in, so a failure loses nothing and a moved entry is in exactly one of the two
places. The zip is updated where it lies rather than rewritten, every change
runs behind the progress window and `Esc` leaves the archive exactly as it was.
A kind that cannot be changed refuses by name before anything runs — a plain tar
never opens the name prompt for `F7` nor the confirmation for `F8`, and moving
out of a rar is refused where copying out of it works — and an archive locked
with a password says so instead of quietly storing a new entry in the clear,
because the library cannot write an encrypted entry.

**The ninth phase closed with the remaining formats.** 7z can now be created —
it stands beside zip and tar.gz in the pack dialog, written with LZMA2 and
without a password, which the dialog's field already knew how to refuse — and
its entries can be changed by the same keys a zip's can, an existing 7z being
rewritten whole where a zip is updated where it lies. Tar, tar.bz2 and tar.xz
joined the kinds that can be read and browsed, and rar stays readable and
uncreatable for good. Every kind now does exactly what the capability table
claims: a round trip for each creatable one, an add and a remove for each
changeable one, and a listing and an extraction for each readable one — rar
excepted, since nothing in the toolchain can create a rar to read back.

**Tar.gz joined the kinds that can be changed.** What stood in the way
was that nothing rewrote it, not that a gzip stream forbade it — and 7z had
already shown the shape. `Archive.rewriting` now takes a kind, asks
`ArchiveFormat` for it and writes the new archive through the same
`intoArchive` the packing walk goes through, so the rule is one sentence: a kind
that can be created can be changed, zip in place and everything else by being
written again. `Sink` grew to carry a `Stored` — a size, a modification time
that may be unknown, and a function that opens the content — which is what lets
one writer take a file from disk and an entry buffered out of the old archive
without knowing which it has. The price is a full recompression per change and
the memory of the largest entry, the same 7z already paid.

**`F5` and `F6` now cross between two archives (TD-016).** Standing inside an
archive on both sides used to be the one pairing of the two panels that
silently did nothing. An entry now goes through the session working directory of
TD-015: extracted into a scratch directory with the same `Archive.extract` that
copies out of an archive, added to the other one with the same
`Archive.addEntries` that copies into one, and the scratch directory removed
whole afterwards — whether the batch finished, was cancelled or threw. Nothing
about it is per-kind, so a zip entry lands in a tar.gz as readily as in a zip.
`F6` drops the entry from the archive it came from only once the other one holds
it, and needs *both* archives changeable where `F5` needs only the target, which
is why the gate in `Update` now takes a list of archives and names the first
that refuses.

**The suite passes on both operating systems** — a handful of tests skip on
Linux, the ones that assert on drive letters, which exist nowhere else. Unit
tests cover the logic modules and the pure `Ui` ones; integration tests drive
real Terminal.Gui views over an in-memory file system. All of them follow the
arrange/act/assert shape and the folder layout described in
[Testing](Architecture/TESTING.md). `dotnet test Marbas.slnx` is the whole run,
and GitHub Actions runs it on both operating systems on every push and pull
request; coverage stays close to complete for everything but `Program.fs` and
`Adapters/`, the composition root no test can reach. What no test can reach
otherwise — colours, a resized terminal, a real permission bit, the mouse — is
written up as the checklist in
[Cross-platform](Architecture/CROSS-PLATFORM.md).

| Phase | Tasks | State |
|-------|-------|-------|
| 1 — Application skeleton | P1.1–P1.4 | done |
| 2 — Directory listing | P2.1–P2.4 | done |
| 3 — Navigation | P3.1–P3.5 | done |
| 4 — Selection | P4.1–P4.3 | done |
| 5 — File operations | P5.1–P5.6 | done |
| 6 — Viewing files | P6.1–P6.2 | done |
| 7 — Polish & configuration | P7.1–P7.5 | done |
| 8 — Quality & release | P8.1–P8.3 | done |
| 9 — Archives | P9.1–P9.14 | done |

## What works today

- Full-screen window split into two panels, resizing with the terminal.
- `Ctrl+F3`, `Ctrl+F4`, `Ctrl+F5` and `Ctrl+F6` sort the panel they are pressed
  in by name, extension, date or size; pressing the key of the sort already in
  use reverses it. `..` stays first and directories stay above files whatever
  the order, entries the key cannot tell apart fall back to their names, the
  cursor keeps the entry it was on, marks are untouched, and the panel title
  says which sort is on (`C:\start [size ↑]`). Each panel sorts on its own and
  keeps its order through every reload and every directory it is pointed at.
- `Alt+H` (or the classic `Ctrl+H`, where the terminal delivers it) shows or
  hides the hidden entries of the panel it is pressed in — a
  name starting with a dot, or an entry the OS marks hidden or system. They are
  concealed until asked for, they take their usual place in the order once shown
  (`.config` sits among the files, not above them), and the title says so
  (`C:\start [name ↑ hidden]`). The cursor keeps the entry it was on and the
  marks of the entries that stay survive; a mark on an entry that has just been
  concealed is dropped, so nothing can be operated on unseen. Each panel decides
  on its own, and it keeps the decision through every reload and every directory
  it is pointed at. `..` is never concealed, whatever the parent directory is
  marked with.
- `Tab` switches panels; the focused one is marked with a double border, and
  nothing else moves between them. `↑`/`↓`, `PgUp`/`PgDn` and `Home`/`End`
  stop at the first and the last entry instead of falling out of the panel.
- `→` moves the cursor to the last entry of the page on screen and, pressed
  again, a page forward onto the last entry of the next one; `←` does the same
  backwards, first to the top of the page and then a page back. Both stop at
  the ends of the listing and neither changes panel. The page is the one
  actually on screen — the jump is measured from the list's own scroll offset,
  so it follows a panel that has been scrolled.
- Typing a printable character opens a search in the active panel: the bottom
  row shows `Search: …` and the cursor moves to the first entry whose name
  contains the text, wherever in the name and whatever the case. `Backspace`
  shortens it and closes it on the last character, `Esc` closes it, and any
  other key closes it and then does its own job — so `Enter` opens what was
  found and `F5` copies it. While the search is open every printable key is
  text, `Space` and `+` included; while it is closed those keep their commands.
  Switching panels or reloading a panel closes its search, and the cursor stays
  where the search left it.
- Entries are coloured by what they are: directories and `..` white, ordinary
  files bright blue, archives bright magenta and executables bright green. A
  marked entry stays yellow and the entry under the cursor keeps the cursor
  bar, so neither is repainted.
- Both panels list a real directory: `..` first, then directories, then files,
  each group alphabetically, in aligned name / size / date columns that
  reformat when the terminal is resized. Directories show `<DIR>`. Names collate
  the way a reader expects rather than by byte value — punctuation before
  digits, digits before letters, case ignored — so `_notes.txt` heads its group
  instead of trailing behind `zebra`.
- An underscore in a name is shown, not swallowed. Terminal.Gui reads `_` as its
  hot key marker, so every view that displays something the file system named —
  the panel frames, the panel footers, the status line, the progress window —
  has that marker turned off.
- `Enter` descends into the directory under the cursor. `Enter` on `..` goes
  up and places the cursor on the directory just left.
- `Enter` on an archive the capability table calls readable — zip, tar, tar.gz,
  tar.bz2, tar.xz and 7z proven by the suite, rar through the very same reader —
  opens it in the panel as though it were a directory, and the title says so
  (`C:\work\backup.zip\docs`). Levels inside walk in and out the same way, `..`
  at the top level leaves the archive with the cursor back on the archive file,
  and `Ctrl+R` re-reads it. Sorting, hidden entries, marking, the search and
  the colours work exactly as they do on disk, since what a level of an archive
  answers with is the same list of entries a directory answers with. `F3` views
  a file inside an archive, asking once for the password if the archive carries
  one; `F4` refuses with a message, because an entry cannot be edited in place.
  `F5` copies the targets out into the other panel — a marked directory with
  the tree below it — behind the same progress window, with the same overwrite
  question and the same surviving marks as a copy between two directories, and
  the archive is left exactly as it was.
- A kind that can be created can also be changed from the panel — zip, tar.gz
  and 7z: `F5` from the other panel adds its
  targets at the level shown, `F8` removes the targets of the archive panel
  (always for good, since a zip has no trash), `F7` makes a directory entry, and
  `F6` moves in either direction — out of an archive it extracts first and drops
  the entry only for what arrived, into one it deletes a source only once that
  source is in, so a moved entry is in exactly one of the two places. Every
  change runs behind the progress window and `Esc` leaves the archive as it was.
  A kind that cannot be changed — a tar, a tar.bz2, a rar — refuses by name before a
  prompt opens or a batch starts, and so does an archive locked with a password.
  With an archive under both panels, `F5` and `F6` carry entries from one to the
  other through the session's own working directory, `F6` needing both archives
  changeable where `F5` needs only the one being written.
  `Shift+F1` and `Shift+F4` still do nothing from inside an archive.
- Unreadable or vanished directories raise a message box; the panel stays put.
- `Ctrl+R` reloads the active panel, keeping the cursor on the same entry even
  when its position changed.
- `Alt+F1` / `Alt+F2` open a root chooser for the left / right panel — the ready
  drives on Windows, the file-system root and the home directory on Unix.
- `Insert`, `Space` and `Shift+↓` mark the entry under the cursor and move one
  entry down; `Shift+↑` marks it and moves one entry up. Every one of them
  toggles, so holding `Shift` and running an arrow over a marked stretch clears
  it again. Marked entries are highlighted, `..` can never be marked, and each
  panel keeps its own marks — they survive `Ctrl+R` (minus whatever
  disappeared) and are dropped when the panel changes directory. The cursor
  stops at the ends of the listing while the entry it stopped on still gets
  marked.
- `Shift+PgDn` (or `Shift+→`) does the same to a whole page: everything from
  the cursor up to — but not including — where a page jump would land, with the
  cursor following onto that last, untouched entry. `Shift+PgUp` and `Shift+←`
  go the other way. Leaving the landing entry out is what lets the key be held:
  the cursor always comes to rest on an unmarked entry, so the next press
  carries on marking instead of undoing the page just marked, and pressing on
  and on marks the whole listing. At the very bottom, where the cursor has
  nowhere left to land, the entry it stands on is marked. On a page that is
  marked only in part the entry under the cursor decides for the whole run —
  standing on an unmarked one marks it all, standing on a marked one clears it
  all — so the result never depends on what the run happens to contain. `..` is
  skipped as always, and a run that reaches past the last entry stops at it.
- `+` and `-` ask for a wildcard pattern and mark or unmark everything it
  matches, so `*.fs` is one step. Matching ignores case and never touches `..`.
  There is one selection per panel, so a pattern and the keys above add to and
  take from the same set: `*.txt` and then `Shift+↓` over one of the matches
  leaves the rest marked.
- A rule separates the listing from the bottom row of each panel, and that row
  is never blank: it summarises the marks — how many entries and how many bytes
  they add up to — and falls back to what the directory holds (`3 files, 2
  folders   1,234 bytes`; `..` counts as neither, and only the files add to the
  total) while nothing is marked. An open search takes it over from both.
- A status line above the function-key bar describes the entry under the cursor
  of the active panel — untruncated name, exact size in bytes (`<DIR>` for
  directories) and modification date. It follows both cursor movement and
  panel switching.
- `F7` prompts for a name and creates that directory in the active panel; the
  panel refreshes with the cursor on the new entry and existing marks intact. A
  blank name or a cancelled prompt does nothing; a clash (or any failure) is
  reported in a dialog. The prompt opens on the name the last directory was
  given, selected, so one keystroke replaces it and Enter alone accepts it. That
  name is kept in the settings file, so it is still offered after a restart.
- `F8` (or `Del`) sends the operation targets of the active panel to the trash —
  the marked entries, or the entry under the cursor when nothing is marked,
  never `..`. `Shift+F8` (or `Shift+Del`) deletes them for good instead. Either
  way it first asks for confirmation, in the wording of the mode it will carry
  out, naming a single target or counting several, with an extra warning when a
  target directory is not empty; saying no changes nothing. Directories go with
  everything inside them, the panel refreshes with the marks of the deleted
  entries dropped, and whatever failed is listed entry by entry in one dialog
  while the rest of the batch still goes through. The trash is the recycle bin
  on Windows and `$XDG_DATA_HOME/Trash` (freedesktop, home trash only) on
  Linux.
- `F5` copies the same operation targets into the directory shown by the other
  panel. Nothing is asked while nothing collides; a target that already exists
  raises a Yes / No / Yes to all / No to all / Cancel question naming it, where
  `No` skips that one entry, `Yes to all` and `No to all` settle every later
  collision the same way without asking again, and `Cancel` drops whatever is
  left. Directory trees go over whole. Afterwards both panels are
  re-read, the marks of the copied entries are dropped while the marks of the
  ones that failed stay, and failures are listed entry by entry.
- `F6` opens a prompt filled with the other panel's path. Accept it and the
  operation targets move there — same collision question as `F5`, same refresh
  and per-item reporting afterwards. Type a bare name instead and, when a
  single entry is being acted on, it is renamed where it stands, with the
  cursor following it to its new place in the listing; a name typed while
  several entries are marked is read as a directory beside them. An empty
  answer or a cancelled prompt does nothing.
- While `F5` or `F6` works, a progress window names the file being handled and
  the rest of the app keeps drawing, because the batch runs on a background
  thread. `Esc` (or the Cancel button) closes that window and the batch stops
  at the next file: whatever was already copied stays, an entry caught in the
  middle is listed afterwards as stopped before it finished, and a move that
  was interrupted never deletes its source. Every overwrite question is
  answered before the work starts, so nothing can block waiting for the user.
- `F3` opens the file under the cursor — only that one, never the marked
  entries — in a viewer that covers the whole terminal without a frame. The top
  row names the file, says whether it is showing text or a hex dump, which
  encoding it read, how many bytes the file holds, which line the screen starts
  on and how far into the file that is; the bottom row is the same twelve-slot
  key bar the panels wear, filled with the viewer's own commands. The arrows
  move by a line, `PgUp`/`PgDn` by a page, `Home`/`End` reach the ends, the side
  arrows move across a long line (a screen at a time with `Ctrl` held), `F2`
  wraps and unwraps, `F4` switches to the hex dump and back at the same place in
  the file, and `F3`, `F10` or `Esc` closes. A directory or `..` under the
  cursor opens nothing. Nothing is read but the rows on screen, so a file of any
  size opens at once and there is no length limit; the encoding is settled from
  the byte order mark, the zero-byte pattern of UTF-16, or whether the bytes are
  valid UTF-8, falling back to Latin-1. A file that cannot be read (it vanished,
  or the OS refused) is reported in a dialog.
- `F4` opens the same file — the one under the cursor — in an editor: the one
  its extension is associated with, otherwise the default editor named in the
  settings, otherwise whatever `$EDITOR` names, otherwise `notepad` on Windows
  and `vi` elsewhere. Marbas waits for the editor to close, then re-reads the
  panel with the cursor and the marks where they were, so a file saved (or
  created) meanwhile is on screen straight away. An editor that cannot be
  started is reported in a dialog; `..` under the cursor starts nothing.
- `F4` on a *directory* opens its properties instead: a popup naming the path,
  with a tick box for each of read-only, hidden, system and archive and a field
  for each of the created, modified and accessed stamps, written
  `2024-03-01 09:05:07` whatever the machine's locale. Accepting it writes the
  stamps first and the flags last — a read-only bit set first would refuse the
  stamps — and only the four flags Marbas offers are touched, so nothing else
  the entry carries is lost. The panel is re-read afterwards, which is why a
  directory just marked hidden disappears from a panel that conceals hidden
  entries. A stamp typed as something that is no date leaves the one the entry
  had; cancelling changes nothing; an entry that vanished meanwhile is reported
  in a dialog.
- `Shift+F4` asks for a name and creates that file in the active panel, then
  opens it in the editor its extension is associated with. The panel comes back
  with the cursor on the new file and the existing marks intact. A name a file
  already answers to is opened as it stands rather than emptied; a name a
  directory answers to is refused in a dialog; a blank name or a cancelled
  prompt does nothing.
- `F12` opens the settings popup, which is where the editors are chosen: one
  field for the default editor — the path to an executable, left blank to fall
  back the way the app always did — and, beneath it, an extension and an editor
  with `Add` and `Remove` beside a list of what is already associated. Moving
  the cursor over a row fills both fields from it, so `Add` re-typed over a
  known extension replaces it and `Remove` takes it out; `Enter` in either
  field does what `Add` does rather than closing the popup. Extensions are
  matched without their dot and without regard to case, so `.TXT` and `txt` are
  one entry. What the popup returns is kept with the rest of the session and
  written to the settings file on quit.
- `Alt+U` (the `Under cursor` button) in that popup fills the editor field being
  typed in — the default one, or the one beside the extension — with the path of
  the executable the panel cursor was standing on when `F12` was pressed, so an
  editor can be pointed at by walking to it instead of by typing its path. An
  executable is what the panel already colours as one: `.exe`, `.com`, `.bat`,
  `.cmd` or `.msi` on Windows, the exec bit on Unix. The button is disabled when
  the cursor was not on one.
- The screen wears the classic colours: both panels are blue with their entries and
  frames in bright cyan, the cursor bar of the focused panel is black on cyan,
  marked entries and the summary line beneath them are bright yellow, the two
  rows under the panels are light grey on black, and the function-key bar is
  black on cyan. Every colour is one of the sixteen ANSI ones, so a terminal
  with a limited palette shows them as they are.
- `Ctrl+Q` or `F10` quits. The bottom bar keeps a slot for every function key
  from `F1` to `F12`, the twelve sharing the width of the terminal evenly. Each
  slot draws its key name on a black background and the command beside it on the
  cyan one, so `F5 Copy` reads as a key and a command; a click on a slot runs it.
  The classic commands (`F3 View`, `F4 Edit`, `F5 Copy`, `F6 RenMov`,
  `F7 Mkdir`, `F8 Delete`, `F10 Quit`, `F12 Setup`) all do their job.
- The commands follow the modifiers being held, the key names never move: with
  `Ctrl` down the slots read `F3 Name`, `F4 Ext`, `F5 Date`, `F6 Size`, with
  `Alt` down `F1 Left` and `F2 Right`, with `Shift` down `F4 NewFile` and
  `F8 Erase`, and the classic commands come back when they are let go. A slot no command asks for
  stays empty. Keys that are not function keys — `Alt+H`, `Ctrl+R`, `+`, `-`,
  `Del`, `Shift+Del` and the rest — are bound but off the bar.
  Every entry can also be clicked. On Windows the modifiers are read from the
  keyboard ten times a second; elsewhere the bar stays on the classic row.
- Quitting writes the session to `%APPDATA%\Marbas\settings.conf` (on Unix,
  `~/.config/Marbas/settings.conf`): where each panel stands, which key it sorts
  by and in which direction, whether it conceals the hidden entries, and which
  editors `F4` reaches — `editor.default` and one `editor.extension.<ext>` line
  per association. The next start reads it back, so both panels come up on the directories they were
  left on, sorted the way they were sorted. The file is `key=value` lines meant
  to be readable and editable by hand; anything it does not say, says wrongly or
  spells in a way that means nothing falls back to what a first start would use,
  and a directory that is no longer there falls back to the working directory
  rather than raising an error at startup. A missing file is a first start. When
  the file cannot be written at all, the app quits without complaining.

- `Shift+F1` packs what the active panel points at into an archive in the other
  one. The popup opens on the other panel's path and a name deduced from the
  targets — one entry gives its own name without its old extension, several give
  the name of the directory holding them, a drive root gives `diskCcontents` —
  and offers zip, tar.gz or 7z, three compression levels and a password.
  Switching the format swaps the extension on whatever is in the box; a bare
  name typed there puts the archive beside the entries instead. Zip with a
  password is AES-256 (the names inside stay readable, only the content is
  locked); tar.gz and 7z cannot carry one and grey the field out. Directories
  go in whole with their empty subdirectories and modification times, the run shows its progress and
  `Esc` stops it — leaving no half-written file and every mark in place — an
  existing archive asks before it is replaced, and an archive that would land
  inside what it packs is refused. When it is done the other panel is re-read
  with the cursor on the new archive, and anything that failed keeps its mark
  and its line in one dialog.

## Architecture

Design decisions and their reasoning live in [_documentation/Architecture/](Architecture/OVERVIEW.md),
one topic per file — domain types, state and effects, file operations, archives,
UI and input, settings persistence, testing, packaging and cross-platform
behaviour.

## Next up

**The first eight phases are finished**, and the ninth is two thirds of one:
P9.1 to P9.9 pack on `Shift+F1` and unpack on `Shift+F2`, with every archive
kind named and one read back level by level. What follows is only partly plan
order, and mostly choice. Four things are waiting:

- The rest of phase 9, P9.10 to P9.14: a panel located inside an archive,
  `Enter` walking into one, `F5` copying out of one, and `F5`, `F6`, `F7` and
  `F8` changing one where the kind allows it — a move out of an archive dropping
  the entry only once the extraction is in. Then the formats that are left: 7z
  on the writing side, tar.bz2, tar.xz and rar on the reading one. Rar is
  readable and uncreatable for good: its compression is proprietary.
- The rows still marked *manual* in [Cross-platform](Architecture/CROSS-PLATFORM.md) —
  colours, a resized terminal, the mouse, a real Unix permission bit, and `F4`
  with a full-screen console editor — want a person at a terminal on each
  platform, and now also the published binaries rather than `dotnet run`.
- The one known defect: a full-screen console editor on Unix shares the terminal
  with a driver that never let go of it, which needs a driver shutdown around
  the child process and a switch to keep the detour off Windows.
- Everything under *Known rough edges* below, of which both panels starting on
  the same directory is the one a user meets first.

## Known rough edges

- `SharpZipLib` cleans an entry name as it writes it, turning a backslash into
  a separator. On Linux, where a backslash is a legal character in a file name,
  packing `a\b.txt` therefore stores `a/b.txt` and the archive gains a level the
  disk never had. `ArchiveListing` reads keys the other way — only `/` starts a
  new level — so such an archive lists a directory that was never there. The fix
  is on the writing side, where the entry name would have to bypass
  `ZipEntry`'s cleaning.
- Reading an archive lists it whole for every level: `ArchiveListing.read` walks
  all of the entries and keeps the one level asked for. For the archives a
  panel will show that costs nothing measurable, but an archive of a hundred
  thousand entries pays the walk on every step into it.
- Extracting opens the archive once per target, because the tar reader can only
  go forwards and each target answers for itself. Unpacking ten marked entries
  out of a `tar.gz` therefore decompresses it ten times. A single pass that
  keeps a result per target would fix it, at the cost of holding every target's
  outcome while the entries arrive in the archive's own order.
- A wrong password is reported entry by entry with the library's own wording,
  not as "the password is wrong": `PasswordNeeded` is only answered before an
  extraction starts, when the archive holds an encrypted entry and no password
  was given at all.
- An archive is written whole or not at all only when it is cancelled. A single
  unreadable file inside one source leaves the archive standing with everything
  that did go in, which is deliberate — the failures are listed and the marks
  survive — but it does mean a zip can be complete-looking and short of a file.
- The refusal to pack an archive into itself compares paths case-insensitively
  on both platforms, so on Unix two directories differing only in case can make
  it refuse a target that would have been legal.
- Nothing reads archives yet: the entry under the cursor is a file like any
  other, `Enter` does not open it and there is no unpack key. Until that lands,
  a zip made here has to be opened somewhere else.
- A password only reaches zip. Tar.gz has no notion of one, and 7z — which can
  encrypt its headers as well as its content — is not written yet.
- The entry colours and the rule under the listing are drawn by a driver no test
  has, so what a test can say is which attribute `RowRender` hands back and
  where the `Line` sits — not what the screen looks like. Both belong on the
  manual row of [Cross-platform](Architecture/CROSS-PLATFORM.md), and bright blue on the
  panel's blue background is the one to look at first.
- The search matches the first entry from the top of the listing and there is no
  "next match" key, so a name that shares its text with an earlier one cannot be
  reached by typing alone. Searching on from the cursor, or binding a repeat
  key, is the fix whenever it starts to bother.
- `Home` and `End` are swallowed at the ends of the listing rather than handled,
  which is invisible while nothing else wants them.
- Holding `Shift` on Windows now shows two commands, `F4 NewFile` and
  `F8 Erase`; the other ten slots of that row are still blank, because `Shift`
  has no other labelled bindings on it.
- The bar gives each of `F1` … `F12` an equal slice of the terminal, so below
  about ninety columns the longer commands are clipped (`Quit` reads `Qui` at
  eighty, `NewFile` reads `New`). Shorter labels would fix the narrow case and
  waste the wide one; a width-aware label (long name where it fits, short where
  it does not) is the fix if it ever matters. It is also why `F12` is labelled
  `Setup` while the popup it opens is titled `Settings` — `Settings` beside a
  three-character key name is clipped to `Set` on an eighty-column terminal.
- `F1`, `F2`, `F9` and `F11` have slots and no commands, which is what the
  reserved space is for — `F1` help and `F2` a user menu are the classic
  fillers.
- The settings popup covers the editors and nothing else. Everything else the
  settings file holds — where each panel stands, how it sorts, whether it
  conceals — is still written on quit and editable only by hand. Adding those
  rows is a matter of more fields on the same popup whenever it is wanted.
- Nothing checks that a configured editor exists before `F4` starts it. A path
  that names nothing is reported the way any other failed start is, after the
  key was pressed, rather than when it was typed into the popup.
- The properties popup edits one entry, the one under the cursor, and never the
  marked ones. It also offers no way to reach the properties of `..` or of a
  file — `F4` on a file goes to the editor, which is what the key is for.
- Whether the terminal delivers `Shift+↑` / `Shift+↓` / `Shift+PgUp` /
  `Shift+PgDn` as keys of their own is the driver's business, and no test
  reaches it — the suite injects the key. It is a manual row in
  [Cross-platform](Architecture/CROSS-PLATFORM.md), and `Shift+←` / `Shift+→` are bound
  to the same two commands partly as a way round a terminal that drops the
  shifted page keys.
- Both panels start on the same directory, so `F5` straight after launch copies
  a file onto itself and the OS refuses it with an error dialog. `F6` can be
  pointed elsewhere by hand, `F5` cannot — a target prompt for `F5`, or a check
  that the two directories differ, would be the fix.
- Progress is posted per file through `app.Invoke`. A tree of thousands of tiny
  files therefore queues thousands of callbacks; if that ever stutters, the fix
  is to coalesce (keep the latest path in a field and refresh on a timer)
  rather than to report less.
- The viewer is opened by `Shell` through the `ShowFile` port, which now hands
  over an opened `FileWindow.Source` rather than a list of lines, so the modal
  run itself (`app.Run viewer` in `Prompts.showFile`) is only exercised by running
  the app — the same blind spot the progress dialog has. What the tests do
  cover is every key the viewer takes and every row it would draw.
- The viewer shows one encoding per file and offers no way to override it. Where
  detection has to guess — a byte order mark is missing and the bytes are not
  valid UTF-8 — it says Latin-1 and renders every byte, which is readable but
  wrong for a single-byte code page that is not Latin-1. A key that cycles the
  encoding would be a small addition to `ViewerKeys` and `ContentView`.
- A single line longer than `LineIndex.MaxLineLength` (a megabyte) is cut there.
  Nothing in a viewer can show more of one line than a screen holds, but
  scrolling right past that point stops rather than going on.
- `F4` starts the editor as a child process sharing this terminal and only
  forces a redraw (`ClearScreenNextIteration`) when it returns. A windowed
  editor — `notepad`, VS Code — round-trips cleanly. A full-screen console
  editor on Unix shares the terminal with a Terminal.Gui driver that is still
  reading input, and there is no suspend/resume in the v2 API for a child
  process (`IDriver.Suspend` is `SIGTSTP`, a different thing). If that turns
  out to matter, the fix is to shut the driver down around the process and
  re-initialise afterwards, and it touches `Adapters/EditorProcess.fs` only.
- The cursor is invisible while it stands on a marked entry: `ListView` asks for
  `Focus` and then for `Highlight` on that row, so the marked colour is the one
  that lands. Colouring it the classic way — the marked yellow on the cursor's
  cyan — needs the `RowRender` event, which hands over the row number and takes
  a `RowAttribute` that overrides the role. It is one handler in `FilePanel`
  whenever it starts to bother.
- Dialogs and message boxes keep Terminal.Gui's own theme; the main screen and
  the `F3` viewer wear the classic colours. The remaining four surfaces
  (`PromptDialog`, `PatternDialog`, `ProgressDialog`, `MessageBox`) are separate
  and colouring one without the rest would look worse than colouring none.
- The modifier row only appears on Windows. `WatchModifiers` registers nothing
  elsewhere, because no Unix driver in Terminal.Gui 2.4.17 reports a standalone
  modifier outside the kitty keyboard protocol. If that protocol is ever wired
  through, the fix is one more implementation of the same port.
- The poll reads the physical keyboard, not this window's: holding `Ctrl` in
  another application changes Marbas's bar too, if it happens to be visible.
  `GetConsoleWindow` would gate it, but under Windows Terminal that handle is
  not the window the user sees, so the gate would switch the feature off where
  it matters most.
- The bar follows the keyboard ten times a second, so a `Ctrl` tapped and
  released inside 100 ms may never show its row. Polling faster costs more than
  the miss does.
- `Ctrl+H` toggles hidden entries only where the terminal reports it as a key
  rather than as the byte `0x08`, which no terminal outside the kitty keyboard
  protocol does. Under Windows Terminal it arrives as `Backspace` and does
  nothing, which is why `Alt+H` is the binding on the bar. Nothing is bound to
  `Backspace` itself, so the collision is silent rather than surprising — but it
  is also the reason `Backspace` cannot later be given the "go up one directory"
  meaning some managers have without a `Ctrl+H` pressed elsewhere reaching it.
- Delete goes through an effect now, like copy and move, but it still runs on
  the UI thread rather than behind `RunBatch`, so removing a huge tree blocks.
  Neither `Directory.Delete` nor the recycle bin reports progress, so a
  progress window would have nothing to show; cancelling is what it would buy.
- Only the home trash is written on Unix, never the per-volume
  `$topdir/.Trash-$uid` the freedesktop spec asks for. Deleting from another
  disk therefore copies the entry across rather than renaming it — correct, but
  slow for a large tree.
- Nothing in Marbas browses or empties the trash; that is the desktop's job.
  A `Trash.restore` reading the `.trashinfo` files back would be the obvious
  next step if it is ever wanted.
- The session is written only on quit, so a crash or a closed terminal loses it.
  Writing after every navigation would keep it current at the cost of a file
  write per keystroke; a write on a timer is the middle road if it ever matters.
- `Ctrl+Q` pressed while a dialog holds focus still quits through the
  application binding, which does not go through `Update` and therefore does not
  save. Unbinding `Command.Quit` is not the way out: `Esc` answers to that same
  command on purpose, since that is what closes a popup.
- Which panel is active, where the cursors stand and what is marked are not
  saved — P7.4 asked for paths, sort orders and toggles. The cursor would be the
  obvious next one, and it fits the file as `left.cursor=<name>`, resolved the
  way `FocusTarget.ByName` already resolves.
- The settings file is read and written through the same `IFileSystem` the
  panels browse. That is what makes it testable, but it also means a future
  read-only or sandboxed file system would silently take the config with it.
