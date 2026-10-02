# Phase 18 — The clipboard

Goal: what the panel points at can be handed to other applications through the
system clipboard — as names, as full paths, as the places links lead to, and as
files the system file manager and any other program that accepts them can
paste — and a path or files on the clipboard can be brought back into Marbas.
The keys were measured in P17.2.

Five decisions run through the whole phase.

**The clipboard is a port, and this phase fills it on Windows only.**
`Dependencies` gains a clipboard that sets and reads text and sets and reads a
list of files with the intent to copy or to move. Only `Adapters` touches the
system: the Windows adapter speaks the Win32 clipboard. The Unix one answers
every request with a refusal the user is shown once per session, since Linux
needs `xclip` or `wl-clipboard` for text and a desktop-specific format for
files; P18.1 records that in [Rough edges](../ROUGH-EDGES.md).

**What is copied is the operation targets.** The marked entries, or the entry
under the cursor when nothing is marked, exactly as `F5` would take them; `..`
under the cursor with nothing marked copies nothing.

**Text is one entry per line.** Names and paths are joined with the system line
break, without quotes, without a separator after a directory and without a line
break after the last one, so a single name pastes cleanly into a field. A full
path inside an archive is spelled the way the panel title spells it
(`C:\work\backup.zip\docs\notes.txt`), and in a panel of search results each
hit gives its own path.

**A path read from the clipboard is forgiven what it reasonably can be.**
Spaces and quotes around it are stripped, a `file:///` URI is read as the path
it names, `~` and environment variables are expanded, and a relative path is
taken from the directory of the active panel. Anything that is not then a single
existing place — several lines, nothing, a path that does not exist — is passed
over in silence.

**Files on the clipboard go through the same batches as `F5` and `F6`.** Pasting
copies or moves into the place the active panel shows, with the same progress
window, overwrite question and per-entry report, into a changeable archive as
`F5` adds to one. Entries inside an archive are first extracted into the
session's working directory before they are put on the clipboard, because a
program outside Marbas can only paste a file that exists on the disk; cutting
from inside an archive is refused by name.

- **P18.1 The port and the names** — the clipboard joins `Dependencies` with
  its Windows adapter and its refusing Unix one, and `Ctrl+Ins` puts the names
  of the operation targets on it as text. *Done when:* the text built for a
  single entry, several entries, a directory and a panel of search results is
  proved without a terminal, the key copies through a real `Application` with a
  fake clipboard, the real adapter is tried by hand into another program, and
  the Unix refusal is a row in [Rough edges](../ROUGH-EDGES.md).
- **P18.2 The full paths** — `Alt+Shift+Ins` copies the full path of each target,
  from a directory, from inside an archive and from search results. *Done
  when:* the three are proved.
- **P18.3 Where the links lead** — `Ctrl+Alt+Ins` copies, for each target that
  is a link, the place it points at — a symbolic link and a junction alike, as
  the status line already reads them — and for a `.lnk` shortcut the target the
  shortcut names, read by a parser of the shortcut format in `FileSystem` so it
  can be tested on `MockFileSystem`. A target that is neither copies its own full
  path. Only the entry itself is resolved, not the directories above it. *Done
  when:* each kind is proved, a shortcut file built byte by byte in a test
  included, and a shortcut whose target the parser cannot read falls back to the
  shortcut's own path.
- **P18.4 Going to the path on the clipboard** — `Ctrl+Shift+Ins` reads the text
  on the clipboard, forgives it as the decision above describes, and sends the
  active panel there: into a directory, or beside a file with the cursor on it.
  The landing is remembered in the folder history. Reading the environment goes
  through a port, so the expansion is testable. *Done when:* every form the
  decision lists is proved to land, and several lines, an empty clipboard and a
  missing path are proved to change nothing and say nothing.
- **P18.5 A path inside an archive** — a path whose leading part is an existing
  archive the capability table can read (`C:\work\backup.zip\docs\notes.txt`)
  opens the archive at that level, with the cursor on the entry when the path
  names one. Nothing parses such a path today: the parser walks up the path to
  the nearest file that exists and asks whether it is an archive. *Done when:* a
  path to a level, a path to an entry, a path into an archive that does not hold
  it and a path through a file that is no archive are proved on
  `MockFileSystem`.
- **P18.6 Files on the clipboard** — a spike first proves that what the adapter
  writes — the list of files and the intent to copy or to move, in the formats
  the system file manager reads — is pasted there as a copy or as a move. Then
  `Ctrl+Shift+C` puts the operation targets on the clipboard to be copied and
  `Ctrl+Shift+X` to be moved, extracting from an archive as the decision says.
  *Done when:* the files and the intent handed to the port are proved for both
  keys, in a directory and in an archive, and a copy and a move pasted into the
  system file manager are tried by hand.
- **P18.7 Pasting** — `Ctrl+Shift+V` reads the files on the clipboard, put there
  by Marbas or by any other program, and copies or moves them into the place the
  active panel shows through the batches `F5` and `F6` run, an archive included.
  After a move the clipboard is emptied, as the system file manager does, so the
  same files are not moved twice. A clipboard holding no files does nothing.
  *Done when:* a copy and a move are proved into a directory and into an archive
  with a fake clipboard, including the overwrite question, and pasting files cut
  in the system file manager is tried by hand.

Every task updates [the user guide](../USER-GUIDE.md),
[What works today](../Progress/WHAT-WORKS.md) and
[File operations](../Architecture/FILE-OPERATIONS.md) for what it adds, and the
port is described in [State and effects](../Architecture/STATE-AND-EFFECTS.md)
when P18.1 brings it.

## Left for a later phase

- **The clipboard on Linux** — text through `wl-clipboard` or `xclip`, or the
  terminal's own OSC 52 escape, and files through the desktop's
  `x-special/gnome-copied-files` and `text/uri-list` formats.
