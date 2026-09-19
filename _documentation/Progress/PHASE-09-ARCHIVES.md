# Phase 9 — Archives

The tasks are in [Phase 9 — Archives](../Plan/PHASE-09-ARCHIVES.md).

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

**`F5` and `F6` now cross between two archives.** Standing inside an
archive on both sides used to be the one pairing of the two panels that
silently did nothing. An entry now goes through the session working directory:
extracted into a scratch directory with the same `Archive.extract` that
copies out of an archive, added to the other one with the same
`Archive.addEntries` that copies into one, and the scratch directory removed
whole afterwards — whether the batch finished, was cancelled or threw. Nothing
about it is per-kind, so a zip entry lands in a tar.gz as readily as in a zip.
`F6` drops the entry from the archive it came from only once the other one holds
it, and needs *both* archives changeable where `F5` needs only the target, which
is why the gate in `Update` now takes a list of archives and names the first
that refuses.
