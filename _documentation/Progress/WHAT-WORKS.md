# What works today

What Marbas does today, feature by feature. How to reach each of them is in the
[user guide](../USER-GUIDE.md).

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
  text, `Space`, `+` and `*` included; while it is closed those keep their
  commands, so `*` inverts the marks rather than opening a search.
  Switching panels or reloading a panel closes its search, and the cursor stays
  where the search left it.
- Entries are coloured by what they are: directories and `..` white, ordinary
  files bright blue, archives bright magenta, executables bright green and links
  — to a file or to a directory — bright cyan. A
  marked entry stays yellow and the entry under the cursor keeps the cursor
  bar, so neither is repainted.
- Both panels list a real directory: `..` first, then directories, then files,
  each group alphabetically, in aligned name / size / date columns that
  reformat when the terminal is resized. Directories show `<DIR>`. Sizes are
  compact — `512 B`, `1.5 KB`, `59.6 GB`, `2.0 TB`, the way the drive popup
  spells them — in the panel, in a panel of search results and in the search
  results window alike, so even a size of terabytes fits its column; the footer
  and the status line keep the exact number of bytes. Names collate
  the way a reader expects rather than by byte value — punctuation before
  digits, digits before letters, case ignored — so `_notes.txt` heads its group
  instead of trailing behind `zebra`.
- An underscore in a name is shown, not swallowed. Terminal.Gui reads `_` as its
  hot key marker, so every view that displays something the file system named —
  the panel frames, the panel footers, the status line, the progress window —
  has that marker turned off.
- `Enter` descends into the directory under the cursor. `Enter` on `..` goes
  up and places the cursor on the directory just left.
- `Enter` on a file runs it. A file whose extension has an `Opened with:` row
  in the `F12` popup is started with that program; anything else is handed to
  the system, which covers an executable, a document and a shortcut alike —
  Windows through the shell verb, so a console program gets a console of its
  own, and Linux through `xdg-open` unless the entry carries an execute bit.
  Marbas does not wait for what it started, and the program begins in the
  directory the panel is standing in. Whatever is marked is ignored: the entry
  under the cursor is the one that runs.
- `Alt+W` walks into whatever the cursor stands on, which is what `Enter`
  did before an association could outrank it — the way to browse a `.zip` after
  naming a program for `zip` in the popup. `Ctrl+PgDn` is bound to the same
  command and does arrive under Windows Terminal; the letter is there for the
  terminals that swallow it.
- `Ctrl+PgUp` is the mirror of it and goes up one level, whatever the cursor
  stands on — exactly what `Enter` on `..` does: the parent directory with the
  cursor on the directory just left, the level above inside an archive, and out
  of the archive, onto its file, at its top level. From a panel of search
  results it goes back to where the search was started, on the entry the panel
  stood on then. On a root it does nothing. The landing is remembered in the
  folder history. `Alt+U` is bound beside it, because gnome-terminal keeps
  `Ctrl+PgUp` for its own tabs once it has several.
- `Alt+K` keeps the place the active panel stands in — a directory or a level
  inside an archive — under a digit: a popup of one line, *Add to favourites*,
  names the path, shortened in the middle when the screen is too narrow for it,
  and asks for the digit; the first
  key closes it, and a digit (with `Alt` still held or not) fills that slot,
  overwriting what it held, while any other key keeps nothing. A panel of
  search results keeps nothing. `Alt+0` … `Alt+9` send the active panel to the
  place under the digit; an empty slot does nothing, a place that has gone is
  reported with the panel left where it was, and the landing is remembered in
  the folder history. The ten slots are shared by every tab and both panels and
  live in `favorites.conf` beside the settings, read when a digit is pressed and
  written, on top of what the file holds, the moment a slot is filled — so the
  application starts without the file, and two copies keep each other's
  favourites.
- `Alt+F8`, labelled `Favs` in the `Alt` layer of the bar, and `Alt+B` open
  *Favourites*, read afresh from the file: ten rows, `0` to `9`, each the digit
  and its place shortened in the middle, an empty slot the digit alone, and a
  line naming the keys under them. `Enter` or a digit sends the active panel to
  the slot's place, and a digit of an empty slot moves the cursor onto it; `Ins`
  keeps the active panel's place in the slot under the cursor — nothing over
  search results — and `Del` empties it, each written to the file at once and
  shown in the rows straight away, the cursor staying where it was. A change the
  file refuses is reported and the rows stay as they were.
- `Alt+R` on a file opens a popup listing the programs the system already
  knows for that extension — read from the registry on Windows, from the XDG
  mime database on Linux — with a field for a path of one's own and a box that
  keeps the choice as an `Opened with:` row for good. On a directory it opens
  the system's own file manager there, and on `..` the directory the panel is
  standing in.
  `Shift+Enter` is bound to the same command, but Windows Terminal drops the
  modifier and sends a bare `Enter`, so `Alt+R` is the one to press.
- `Alt+Shift+R` — the open-with key with one more modifier — runs the executable
  under the cursor with administrator rights: on Windows the `runas` verb, so
  the system asks before it starts anything, and a prompt turned down leaves
  nothing on the screen. It is the entry's own rights that are raised, so an
  `Opened with:` row does not change what it starts. A document or a directory
  is not a program and is told so, and on Linux the key explains that a terminal
  application cannot ask this system for administrator rights at all.
  `Ctrl+Alt+Enter` is bound to the same command, but no terminal tried so far
  delivers it.
- A file inside an archive says why it will not run, the way `F4` says why it
  will not be edited: the archive would have to be unpacked first.
- `Enter` on an archive the capability table calls readable — zip, tar, tar.gz,
  tar.bz2, tar.xz and 7z proven by the suite, rar through the very same reader —
  opens it in the panel as though it were a directory, and the title says so
  (`C:\work\backup.zip\docs`). Levels inside walk in and out the same way, `..`
  at the top level leaves the archive with the cursor back on the archive file,
  and `Ctrl+R` re-reads it. Sorting, hidden entries, marking, the search and
  the colours work exactly as they do on disk, since what a level of an archive
  answers with is the same list of entries a directory answers with. `F3` views
  a file inside an archive, asking once for the password if the archive carries
  one, and reads it as it scrolls rather than decompressing it whole first, so a
  huge entry opens as quickly as a small one; `F4` refuses with a message,
  because an entry cannot be edited in place.
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
- Unreadable or vanished directories raise a message box and the panel stays put
  whenever a command found them — `Ctrl+R`, walking in, switching a tab. The
  watch of the directories on screen is the one path that says nothing and moves
  the panel instead, since nobody asked it for anything.
- `Ctrl+R` reloads the active panel, keeping the cursor on the same entry even
  when its position changed.
- Both panels of the tab in front follow the disk on their own: an entry another
  program adds, renames or removes appears or goes within a fraction of a second,
  with no key pressed, each cursor kept on its entry and the marks that survived.
  A burst of changes costs one listing, nothing is re-read while a popup or the
  progress window is in front, and a panel whose directory — or whose whole drive
  — is gone steps onto the nearest place that is still there without a dialog.
  Tabs in the background are left alone, since switching to one re-reads it.
- `Alt+F1` / `Alt+F2` open *Drives* for the left / right panel — one aligned row
  per drive with its root, label, kind, size and free space: the ready drives on
  Windows, the mounts that hold files on Unix and the home directory after them.
  The popup stands over its own panel, two columns in from the panel's outer
  edge, and moves inwards only when it would not otherwise fit on the screen.
  Choosing the drive the other panel stands on lands in that panel's directory,
  any other drive in its root, and `Alt+R` opens the drive in the system file
  manager without moving either panel.
- Sorting by size compares directories by what `F3` measured, files by their own
  length, and a directory nobody measured counts as nothing, so it keeps the
  name order it had. A measurement that arrives while the panel sorts that way
  moves the row into place under the cursor.
- `F3` on a directory measures it — the marks, or the entry under the cursor —
  behind the progress window that `Esc` stops, and writes the total into the
  size column in place of `<DIR>` and into the status line. A total that had to
  skip something unreadable, or that was stopped part way, is marked `≥`. It
  holds until the panel is read again. `..` measures the directory it leads to.
- `Ctrl+\` sends the active panel to the root of the volume it stands on, in one
  key rather than a walk up the tree; a panel standing inside an archive leaves
  it, and one already on a root stays as it is. The key reaches the application
  as `Ctrl+4`, which is what the terminals make of it, and both spellings are
  bound.
- `Insert`, `Space` and `Shift+↓` mark the entry under the cursor and move one
  entry down; `Shift+↑` marks it and moves one entry up. Every one of them
  toggles, so holding `Shift` and running an arrow over a marked stretch clears
  it again. Marked entries are drawn in yellow and stay so wherever the cursor
  goes, `..` can never be marked, and each panel keeps its own marks — they
  survive `Ctrl+R` (minus whatever disappeared) and are dropped when the panel
  changes directory. The cursor stops at the ends of the listing while the entry
  it stopped on still gets marked. A mouse click only moves the cursor; it marks
  nothing.
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
  matches, so `*.fs` is one step. Matching ignores case and never touches `..`;
  a `\` before `*` or `?` matches that character itself.
  There is one selection per panel, so a pattern and the keys above add to and
  take from the same set: `*.txt` and then `Shift+↓` over one of the matches
  leaves the rest marked.
- `Alt+E` and `Alt+Shift+E` mark and unmark every entry whose extension is the
  one under the cursor; `Alt+N` and `Alt+Shift+N` every entry whose name without
  the extension is. The extension is what follows the last dot — `gz` for
  `archive.tar.gz`, whose name is then `archive.tar` — and a name with no dot,
  or with a dot only in front, has an empty one, so `readme`, `Makefile` and a
  directory called `src` go together. Case is ignored, directories take part
  like files, and both add to and take from the same selection as everything
  above. With `..` under the cursor the four keys do nothing, and `..` is never
  marked. `Ctrl+grey+`, `Ctrl+grey-`, `Alt+grey+` and `Alt+grey-` are bound
  beside them for a driver that may one day deliver them.
- `Alt+A` marks every file and `Alt+Shift+A` unmarks every file, both leaving
  the marks of directories as they were; `Alt+I` or a bare `*`, grey or main,
  inverts the marks of every entry, directories included. `..` is skipped by
  all three, and all three work on the same selection, so `*.txt` followed by
  `*` marks everything but the text files. `Shift+grey+`, `Shift+grey-` and
  `Ctrl+grey*` are bound beside them, for the same reason as above.
- `Alt+M` swaps the panel's marks with the set they held before the last
  marking command — `Insert`, `Space`, the shift arrows and page keys, `+`,
  `-`, and the six keys above — so a second press goes forward again. Each
  panel remembers one step of its own. A marking command that changes no mark
  leaves the memory alone; moving the cursor, the quick search, `Tab` and a
  refresh raised by the directory watch keep it; every other key forgets it, a
  change of directory and a file operation included. Names that have gone from
  the listing by the time the marks come back are left out. `Ctrl+M` is bound
  beside it, but every terminal measured sends it as `Enter`.
- A rule separates the listing from the bottom row of each panel, and that row
  is never blank: it summarises the marks — how many entries and how many bytes
  they add up to — and falls back to what the directory holds (`3 files, 2
  folders   1,234 bytes`; `..` counts as neither, and only the files add to the
  total) while nothing is marked. A results panel holding hits nothing could
  verify says how many (`4 files, 0 folders, 2 unverified   8 bytes`). An open search takes it over from both.
- A status line above the function-key bar describes the entry under the cursor
  of the active panel — untruncated name, exact size in bytes (`<DIR>` for
  directories) and modification date; a link adds what it points at after its
  name (`twin -> C:\data\docs`). It follows both cursor movement and
  panel switching.
- `F7` prompts for a name and creates that directory in the active panel; the
  panel refreshes with the cursor on the new entry and existing marks intact. A
  blank name or a cancelled prompt does nothing; a clash (or any failure) is
  reported in a dialog. The prompt opens on the name the last directory was
  given, selected, so one keystroke replaces it and Enter alone accepts it. That
  name is the newest of the names `F7` has created, kept in `history.conf`, so it
  is still offered after a restart.
- `F8` (or `Del`) sends the operation targets of the active panel to the trash —
  the marked entries, or the entry under the cursor when nothing is marked,
  never `..`. `Shift+F8` (or `Shift+Del`) deletes them for good instead. Either
  way it first asks for confirmation, in the wording of the mode it will carry
  out, naming a single target or counting several, with an extra warning when a
  target directory is not empty; saying no changes nothing. Directories go with
  everything inside them. Both run behind the progress window, which names each
  entry as it goes and which `Esc` stops — what was already removed stays gone,
  and what the batch never reached keeps its mark. The panel refreshes with the
  marks of the deleted entries dropped, and whatever failed is listed entry by
  entry in one dialog while the rest of the batch still goes through. The trash
  is the recycle bin on Windows and `$XDG_DATA_HOME/Trash` (freedesktop, home
  trash only) on Linux.
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
  several entries are marked is read as a directory beside them. A rename is
  instant wherever the file system can carry it out itself; where it refuses —
  a directory whose new name lies across a mount point — the copy it falls back
  to runs behind the progress window with `Esc`, like any other batch. An empty
  answer or a cancelled prompt does nothing.
- `Alt+F6` makes a link to the operation targets. The popup offers the other
  panel's directory plus the name of the entry under the cursor — or the
  directory alone when several are marked — and the kinds this system can make:
  symbolic and hard everywhere, a junction on Windows alone. A kind the place
  typed cannot carry is greyed out with the reason beneath it: a hard link only
  to a file on the same volume, a junction only to a directory. The panel the link lands in is read
  again with the cursor on it, drawn in the link colour; a link Windows refuses
  for want of Developer Mode, and every other failure, is reported with a
  sentence.
- `Ctrl+Ins`, and `Alt+C` beside it, put the names of the operation targets on
  the system clipboard as text: one name per line in listing order, joined by
  the system line break with none after the last, unquoted, a directory without
  a trailing separator; a hit in a panel of search results gives its own name,
  an entry inside an archive the name the archive holds it under. `..` under
  the cursor with nothing marked copies nothing, and the marks stay as they
  were. Windows only: on Unix the clipboard refuses, and the refusal is shown
  the first time in a session and never again; a failure of the Windows
  clipboard itself is reported every time, with the reason the system gave.
- `Alt+Shift+Ins`, and `Alt+P` beside it, put the full paths of the same
  targets on the clipboard in the same shape: a path on disk as it stands, an
  entry inside an archive spelled through the archive the way the panel title
  spells a level (`C:\work\backup.zip\docs\notes.txt`), and a hit in a panel of
  search results by its own full path, on disk or inside an archive alike.
- `Ctrl+Alt+Ins`, and `Alt+L` beside it, put on the clipboard where each of
  the same targets leads. A symbolic link or a junction gives its target as the
  status line reads it, a relative one resolved from the directory the link is
  listed in; a `.lnk` shortcut on disk gives the path it names — a local path,
  a path on a network share or, failing both, its relative path taken from the
  shortcut's own directory — read by Marbas's own parser of the format, so it
  works without the Windows shell. A shortcut the parser cannot read, an entry
  inside an archive and anything that is no link give their own full path. Only
  the entry is followed, never the directories above it.
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
  size opens at once and there is no length limit, and `End` on a file of a
  hundred million lines keeps one line start in sixty-four rather than all of
  them. A line feed that ends the file shows as nothing, not as a `.` on the
  last line. The encoding is settled from
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
- `F12` opens the settings popup, which is where the programs are chosen: one
  field for the default editor — the path to an executable, left blank to fall
  back the way the app always did — and, beneath it, an extension and a program
  with `Add` and `Remove` beside the lists of what is already associated.
  `Used for` says which role the pair is for, `Edit` or `Open`, and the popup
  keeps a list per role: `Edited with:` is what `F4` reaches and `Opened with:`
  what phase 10 will run. Moving the cursor over a row fills both fields from it
  and moves the role to that row's, so `Add` re-typed over a known extension
  replaces it and `Remove` takes it out; `Enter` in either field does what `Add`
  does rather than closing the popup. Extensions are matched without their dot
  and without regard to case, so `.TXT` and `txt` are one entry. What the popup
  returns is kept with the rest of the session and written to the settings file
  on quit.
- `Alt+U` (the `Under cursor` button) in that popup fills the program field being
  typed in — the default editor, or the one beside the extension — with the path of
  the executable the panel cursor was standing on when `F12` was pressed, so a
  program can be pointed at by walking to it instead of by typing its path. An
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
  `Alt` down `F1 Left`, `F2 Right` and `F6 Link`, with `Shift` down `F4 NewFile` and
  `F8 Erase`, and the classic commands come back when they are let go. A slot no command asks for
  stays empty. Keys that are not function keys — `Alt+H`, `Ctrl+R`, `+`, `-`,
  `Del`, `Shift+Del` and the rest — are bound but off the bar.
  Every entry can also be clicked. On Windows the modifiers are read from the
  keyboard ten times a second; elsewhere the bar stays on the classic row.
- Quitting writes the session to `%APPDATA%\Marbas\settings.conf` (on Unix,
  `~/.config/Marbas/settings.conf`): where each panel stands, which key it sorts
  by and in which direction, whether it conceals the hidden entries, and which
  programs the extensions are associated with — `editor.default` and one
  `program.<role>.<ext>` line per association, the role being `editing` or
  `opening`. A `editor.extension.<ext>` line written before the role existed is
  still read, as one for editing.
  The next start reads it back, so both panels come up on the directories they were
  left on, sorted the way they were sorted. The file is `key=value` lines meant
  to be readable and editable by hand; anything it does not say, says wrongly or
  spells in a way that means nothing falls back to what a first start would use,
  and a directory that is no longer there falls back to the working directory
  rather than raising an error at startup. A missing file is a first start. When
  the file cannot be written at all, the app quits without complaining.
- Every directory a panel lands on — `Enter`, `..`, `Alt+W`, `Ctrl+PgUp`, a root, a drive,
  a path opened, a search hit, a level inside an archive — is remembered in
  `history.conf` beside the settings, newest first, a hundred at most, a
  directory visited again moving to the top rather than repeating. A re-read, a
  sort, a hidden toggle, a tab switch, a panel walking up because its directory
  vanished and a panel of search results remember nothing. The file is written
  on every change and read before each one, so two copies running at once share
  it; a file that cannot be written raises one dialog per session.
- `Alt+F12`, or `Alt+Shift+H`, opens the history read afresh from the file, in
  a popup of three tabs — *History*, *View history*, *Folder history* — that
  opens on the tab it was last left on, *Folder history* the first time. Each
  row is the moment (the time today, the date before) and the place, a long
  path shortened in the middle. `Ctrl+PgDn`/`Ctrl+PgUp` and `←`/`→` walk the
  tabs, while `↑`/`↓` stop at the first and the last row rather than leave the
  tab; the tab headers are of one width, their titles centred, and the one in
  front is drawn like the others. The columns keep their widths — *When* holds
  a whole date, *How* a word as short as `admin`, and *What* takes the rest. On
  *Folder history* `Enter`, and `[Go to]` or `Alt+G` alike, stands the active
  panel in the chosen directory or archive level and moves it to the top of
  the list, and a
  directory that is gone is reported with the panel left where it was.
  *History* lists every file that started — `Enter`, `Alt+R` with a program
  chosen, `Alt+Shift+R` — with the program's name in brackets after the path
  and `admin` where they apply; a directory or drive handed to the system file manager and a
  start that failed are not listed. `Enter` there starts the file again with
  the same program, directory and rights, and `[Go to]` or `Alt+G` stands the
  active panel beside the file with the cursor on it. *View history* lists
  every file viewed with `F3`, marked `V`, and edited with `F4` or `Shift+F4`,
  marked `E`, remembered as the viewer or the editor is asked for — a file
  inside an archive included, a directory measured with `F3` and one whose
  properties `F4` shows not. `Enter` there repeats what the row says, `F3` views
  and `F4` edits whatever it says, an entry inside an archive refusing `F4` as
  the panel does; `[Go to]` or `Alt+G` stands the active panel beside the file,
  inside its archive where it is one. A file that is gone, or whose archive
  is, is handed to the viewer or the editor all the same and is not moved to
  the top.
  On every tab `Del`, or `[Delete]`, forgets the row under the cursor, and
  `Shift+Del` forgets the whole tab once a question naming it is answered
  *Yes* — `[No]` is the default, and `Esc` closes only the question. The popup
  stays open: both are written to `history.conf` at once, the tabs show what
  the file then holds, and the cursor stays on the row that took the forgotten
  one's place. A cleared tab stays empty after a restart, and a second copy of
  Marbas running at the same time does not bring the entries back. Both keys
  do nothing on an empty tab.
  Typing narrows the tab in front to the rows holding what was typed —
  anywhere in the path or in the *How* column, whatever the case, a space
  included — and shows it as *Filter:* on the line under the table.
  `Backspace` shortens it, `Esc` clears a filter that is not empty and closes
  the popup when it is, and switching tabs clears it. `Enter`, `F3`, `F4`,
  `Alt+G` and `Del` act on the entry the filtered row stands for, and a row
  forgotten leaves the filter in place; `Shift+Del` still clears the whole
  tab. A filter that leaves no row disables `[Go to]` and `[Delete]`.
- The fields that ask one question remember what was typed into them: the `To:`
  of `F6`, the name of `F7`, the name of `Shift+F4`, and the pattern of `+` and
  `-`, which share one list. Each field offers its list, newest first, under
  `Alt+↓` or `F4`, with the newest row already under the cursor; `Enter` puts
  the row into the field with the cursor after it and leaves the popup open, and
  `Esc` closes only the list. `Space` types a space. The list is as wide as the
  field and wears its colours. What was typed is remembered when the popup is
  confirmed, whether or not the operation then succeeds; a cancelled popup and a
  blank text remember nothing. `F7` is the exception: a name is remembered only
  once the directory exists, so a refused name does not become the next
  suggestion.
- The fields of the larger popups remember the same way: the masks and the text
  of `Alt+F7`, the archive of `Shift+F1`, the place of `Alt+F6` and the program
  of `Alt+R`, each with a list of its own under `Alt+↓` or `F4`. There `↑` and
  `↓` still move between the fields, as they always did, rather than walk the
  list. A search is remembered when it is started, not when the last results
  are asked for, and a text left empty remembers nothing; the password of an
  archive is never remembered. A program is remembered as the path the popup
  hands back, without the spaces around it.

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

- `Alt+F11` opens a tab, `Alt+Shift+F11` closes it and the last one stays,
  `F11` (or `Alt+T`, where a terminal keeps `F11` for full screen) lists them
  in a popup, and `Alt+←` / `Alt+→` walk between them and wrap round. A tab is
  the whole pair of panels plus the side that is active, so switching one moves
  both panels at once; a new tab is a clone of the one it was opened from,
  cursors and all, without its marks. Coming back to a tab re-reads both of its
  panels, so a directory that changed while you were elsewhere is shown as it
  is; a directory that has gone is reported and the tab is still the one you
  asked for. The top right corner says which tab is in front and of how many —
  `[2/3]` — sharing the line with the right panel's title, which shortens a path
  that no longer fits to `C:\...\parent\current` rather than losing its sort
  marks. Every one of those commands writes the session, and `Shift+F9` writes
  it whenever asked without quitting.
