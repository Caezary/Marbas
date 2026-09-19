# Phase 9 — Archives

Goal: pack and later unpack archives without leaving the manager. Creating an
archive comes first; the format vocabulary is chosen so that reading can be
added without reshaping it.

- **P9.1 Archive format and suggested name** — `ArchiveFormat` (`Zip`,
  `TarGz`) with the extension each one owns, and `ArchiveName.suggest`: one
  target gives its own name without the old extension, several targets give
  the active directory's name, a directory root gives `disk<letter>contents`.
  Changing the format swaps the extension on a name already typed.
  *Done when:* suggestion and extension rules are covered by tests.
- **P9.2 Writing an archive** — `Archive.write` walks the sources through
  `IFileSystem` and streams every file into the archive, entry paths relative
  to the source directory and separated by `/`, empty directories kept,
  modification times preserved. It answers an `ItemResult` per top-level
  source the way `copyAll` does, reports each entry through `BatchControl` and
  stops when it is cancelled, deleting the half-written file on the way out.
  *Done when:* archives written on `MockFileSystem` read back with the
  expected entries, and a cancelled run leaves nothing behind.
- **P9.3 The pack dialog** — `Shift+F1` opens a popup with the target (an edit
  box holding the other panel's path and the suggested name, read like `F6`'s),
  the format, the compression level and a password field.
  *Done when:* the dialog answers with everything the effect needs, or with
  nothing when it is cancelled.
- **P9.4 Packing end to end** — `PackRequested` → the dialog →
  `ArchiveTargets` → the batch → `PackFinished`: the operation targets of the
  active panel are packed into the other panel's directory, an existing
  archive asks the overwrite question, a target that would land inside what is
  being packed is refused, the other panel is re-read with the cursor on the
  new archive, and the marks of what did not make it survive.
  *Done when:* `Shift+F1` produces an archive from marks or from the cursor
  entry.
- **P9.5 Password-protected zip** — a password turns the zip into an AES-256
  one; the field is disabled for the formats that cannot carry one.
  *Done when:* a protected archive refuses to open without the password.

Reading an archive comes next, and it reads formats Marbas cannot write — `rar`
above all, whose compression is proprietary and which therefore never appears
among the formats an archive can be created in.

- **P9.6 Archive recognition** — `ArchiveKind`
  recognised from a name (`zip`, `tar`, `tar.gz`, `tar.bz2`, `tar.xz`, `7z`,
  `rar`), compound suffixes read whole so `release.tar.gz` is a tar.gz and not a
  gz, and one capability record per kind: whether Marbas can read it, create it,
  and change what is in it — adding an entry, removing one, making a directory —
  which is zip and, from P9.14, 7z. `ArchiveFormat` — the set an archive can be
  created in — becomes the creatable projection of that set rather than a second
  vocabulary kept in step by hand. *Done when:* recognition and capabilities are
  covered by tests, and the pack dialog offers exactly the kinds that claim they
  can be created.
- **P9.7 Reading an archive** — `ArchiveListing.read` opens the file through
  `IFileSystem` with `SharpCompress` and answers the entries of one level inside
  it as the same `Entry` list a directory gives — sizes, modification times, and
  directories synthesised where the archive stores none — so sorting, hidden
  entries, marks and the search need no new code. A path inside an archive gets
  a domain type of its own (`ArchivePath`: `/`-separated, never rooted, no `..`),
  and failures come back as values (`ArchiveError`: not a kind that can be read,
  damaged, password needed). *Done when:* archives written on `MockFileSystem`
  list back level by level, including empty and merely implied directories.
- **P9.8 Extracting** — `Archive.extract` streams named entries out of an
  archive into a target directory through `IFileSystem`, a directory taking the
  tree below it, restoring modification times and refusing an entry whose path
  would climb out of the target. It answers an `ItemResult` per top-level target
  the way `Archive.write` does, reports each entry through `BatchControl` and
  stops when cancelled, leaving behind what it had already written. *Done when:*
  a round trip through `write` and `extract` on `MockFileSystem` returns the
  tree it started from, and a cancelled run stops where it stood.
- **P9.9 `Shift+F2` unpack** — `UnpackRequested` → `ExtractTargets` → the batch →
  `UnpackFinished`: the archive under the cursor, or every archive marked, is
  unpacked into the other panel's directory, a file already there asks the
  ordinary five-answer overwrite question, a password-protected archive asks for
  the password once, a kind that cannot be read reports itself as an
  `OperationError`, and the other panel is re-read afterwards. The bar gets an
  `Unpack` slot on `Shift+F2`. *Done when:* `Shift+F2` empties an archive into
  the other panel.
- **P9.10 A panel that is not on disk** — `PanelLocation` is either a directory
  on disk or a place inside an archive (the archive's own `FileSystemPath` plus
  an `ArchivePath`); `PanelState.Path` becomes one, the panel title spells it
  `…\backup.zip\docs`, and every operation asks the location what it allows
  instead of assuming a disk path. A panel standing inside an archive saves the
  directory *holding* the archive to the settings file, because a session should
  restore to a place that is certainly still there. *Done when:* the type is in
  place, both panels browse the disk exactly as before, and the suite says so.
- **P9.11 Walking into an archive** — `Enter` on a readable archive opens it as
  a location instead of doing nothing, `..` at its top level leaves it with the
  cursor back on the archive file, deeper levels walk in and out, `Ctrl+R`
  re-reads it, and `F3` views a file inside one by streaming that entry into the
  viewer. `F4` is the one key that refuses with a message: editing an entry in
  place would mean rewriting the archive when the editor returns, which is a
  promise this phase does not make. *Done when:* a zip, a tar.gz, a 7z and a rar
  all browse like directories.
- **P9.12 Copying out of an archive** — `F5` from a panel inside an archive
  extracts its operation targets into the other panel's directory, marked
  directories taking their trees, running behind the same progress window with
  the same overwrite question and the same surviving marks as a copy between two
  disk directories. *Done when:* `F5` inside an archive is indistinguishable
  from `F5` between directories, and the archive is left untouched.
- **P9.13 Changing an archive** — the operations that write into a panel
  standing inside an archive, each gated on the kind's capability: `F5` adds the
  other panel's targets at that level, `F8` removes the targets of an archive
  panel, `F7` makes a directory entry, and `F6` moves in either direction —
  out of an archive it extracts first and drops the entry only for what arrived,
  into an archive it deletes a source only once that source is in, so a failure
  loses nothing. A kind whose entries cannot be changed refuses by name before
  anything runs, which for `F6` means moving out of a rar is refused where
  copying out of it works. Zip is the changeable kind here; 7z joins it in P9.14
  by turning one flag in the table, since these operations ask the table rather
  than name a format. *Done when:* a file added in the panel is in the archive
  on disk after the panel re-reads, a removed one is gone from it, a moved one
  is in exactly one of the two places, and a tar.gz says why it will do none
  of it.
- **P9.14 The remaining formats** — 7z joins the set an archive can be created
  in through `SharpCompress` (non-solid LZMA2, and no password, which the
  dialog's field already knows how to disable) and with it the set whose entries
  P9.13 can change, an existing 7z being rewritten whole where a zip is updated
  in place; tar, tar.bz2 and tar.xz join the set that can be read; rar stays
  readable and uncreatable for good. The pack dialog's format list and its
  password field follow the capability table of P9.6 rather than a list kept by
  hand, and a small checked-in rar fixture is what proves the read side. Should
  `SharpCompress` turn out not to write the 7z Marbas needs, 7z stays on the
  reading side and the table says so. *Done when:* every kind does exactly what
  its capabilities claim — a round trip per creatable kind, an add and a remove
  per changeable one, and a listing and an extraction per readable one.

## Libraries

`SharpZipLib` (MIT) writes zip, because it is the only maintained managed
library that can encrypt one, and it is what rewrites a zip when P9.13 adds an
entry or removes one; `System.Formats.Tar` over a `GZipStream` writes tar.gz;
`SharpCompress` (MIT) reads everything — zip, the tar family, 7z and rar — and
from P9.14 writes 7z as well. All three are fully managed, which keeps the
single-file publish of P8.3 intact, and none is copyleft. Wrappers around the
native `7z.dll` are LGPL and were rejected on both counts.
