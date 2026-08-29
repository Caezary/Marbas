# Archives

`Shift+F1` packs what the active panel points at into an archive in the other
panel. Reading archives — unpacking one, walking into one — is the rest of
phase 9 and is not built yet; what follows is about creating them.

## The vocabulary

**A format is a closed set, and it says what it can carry.** `ArchiveFormat` is
`Zip` or `TarGz`, and `ArchiveFormat.extension` owns the extension each one
answers to. `carriesPassword` is part of the same type, because whether a
format can be locked is a property of the format and not of the dialog that
offers the field: it is `true` for zip and `false` for tar.gz, and every place
that has to decide — the popup enabling its field, the answer it gives back —
asks the type rather than repeating the rule. `ArchiveFormat.initial` and
`ArchiveCompression.initial` are what a fresh popup opens on, read by both the
popup and the name suggested to it, so the two cannot drift apart.

**The formats that can be written are not the formats that can be read.** Zip
and tar.gz are the two that can be created. Rar can never join them: its
compression is proprietary and the licence of the unrar sources forbids using
them to build a compressor, so no library — whatever its licence — will ever
create one. It belongs on the reading side alone, which is why the type says
what an archive can be *created* in rather than what an archive *is*.

**The extension is a string, deliberately.** `FileExtension` normalises to a
single member — `FileExtension.ofName "a.tar.gz"` is `gz` — so a compound
suffix put through it would be a value that lies. `ArchiveFormat.extension`
answers a plain string instead, used only by `ArchiveName`, rather than bending
the existing type around one case.

**A password is a domain type that trims nothing.** `ArchivePassword.tryCreate`
answers `None` only for the empty string: a password made of spaces is a
password, unlike every other text the app reads out of a field. It lives in
memory for the length of one batch — nothing writes it to the settings file, a
suggested name or an error message.

## Deducing the name

**One target gives its own name, several give the directory's.**
`ArchiveName.suggest` takes the operation targets: `report.txt` alone suggests
`report.zip` — the old extension goes, since the archive is not a text file any
more — a directory `docs` suggests `docs.zip`, and two or more targets are
named after the directory holding them. A compound archive extension is dropped
whole, so `release.tar.gz` suggests `release.zip` and not `release.tar.zip`, and
a name that is all extension keeps it: `.gitignore` suggests `.gitignore.zip`.

**A root has no name, so it is named after its disk.** `C:\` suggests
`diskCcontents.zip`, keeping the letter as the system spells it. A Unix root has
no letter to take, so it falls back to `rootcontents`.

**Choosing a format renames what is already typed.** `ArchiveName.inFormat`
swaps a known archive extension for the chosen one and appends where there is
none, so `backup.2026` becomes `backup.2026.zip` rather than losing the part
that looks like an extension but is not one. `ArchiveName.retarget` does the
same to a whole path, keeping the directory the name was typed into.

## The popup

**The dialog answers, it does not act.** `PackDialog.choice` gives back an
`ArchiveChoice` — the target as typed, the format, the compression and the
password — or `None` when the popup was dismissed, exactly as `PromptDialog`
and `SettingsDialog` do. The target stays a `string` there for the same reason
`TargetEntered` does: whether it names a file or a directory is `Update`'s
question, not the popup's.

**The extension swap is injected, not computed.** `PackDialog.create` takes a
`retarget: ArchiveFormat -> string -> string` and calls it whenever the format
changes; `Adapters/Dependencies.fs` passes `ArchiveName.retarget fs`. Splitting
a path needs the file system's own rules — a backslash separates on Windows and
is a legal name character on Unix — and handing the dialog a function keeps
`IFileSystem` out of the view layer while the tests hand it the same function
over `MockFileSystem`.

**A password the format cannot carry is dropped, not remembered.** The field is
disabled the moment tar.gz is chosen — it loses its cyan input background and
sinks into the dialog — and whatever was typed before that stays on screen but
never reaches the answer.

## Writing

**The archive is written through `IFileSystem`, one entry at a time.**
`Archive.write` opens the target with `fs.File.Create`, wraps it in the writer
its format asks for, and streams every file into it with the source stream open
only while it is being copied. `ZipFile.CreateFromDirectory` would have done
the same walk, but it cannot be cancelled and reports no progress, which are
the two things this needs. There is no performance to be won by calling it
either: the cost is the compression, and memory stays flat whatever the file
size.

**One writer per format.** Zip goes through `SharpZipLib`'s `ZipOutputStream`,
tar.gz through the BCL's `TarWriter` over a `GZipStream`. Both are streaming
APIs over the stream the mock file system hands out, which is what keeps the
whole feature testable without a disk. The split is not arbitrary: only
SharpZipLib can encrypt a zip, and only `System.Formats.Tar` knows a directory
entry and a modification time as first-class things rather than as a name
ending in a slash.

**Entry paths are relative to the source's own parent, and always use `/`.**
Each top-level source contributes the tree below it named from the directory it
sits in, so packing `docs` writes `docs/`, `docs/inner.txt`, `docs/deep/` and so
on. Empty directories keep an entry of their own and every file carries its
modification time. Whether the panel is concealing hidden entries makes no
difference: what is packed is what is on disk.

**The batch answers per top-level source.** `Archive.write` uses the same
`Operations.allWatched` that copy and move use, so an unreadable file aborts the
tree it is in and leaves the other sources to finish, and the marks of what did
not make it survive — the rule that holds for every operation. The archive that
results is partial, which is the honest outcome: the entries that were written
are in it and the failures are listed in one dialog.

**Cancelling leaves nothing behind.** `Esc` in the progress window stops the
walk, and `write` then deletes the half-written file and answers `Cancelled` for
*every* source rather than only for the ones it had not reached. Since the
archive is gone nothing succeeded, and every mark stays where it was.

**A zip with a password is an AES-256 zip.** The password goes on the
`ZipOutputStream` and every entry is given `AESKeySize = 256`; without one, no
entry is encrypted at all. What this hides is the content: like every AES zip,
the entry names and sizes stay readable to anyone who opens the archive without
the password. Encrypted headers are a 7z feature and wait for the format.

## The walk from key to archive

`PackRequested` → `PromptForArchive` → the popup → `ArchiveEntered` →
`ArchiveTargets` → the batch → `PackFinished`, the same shape `F5` and `F6`
follow.

**The target is read the way `F6` reads its own.** A bare or relative name is
combined with the *active* panel's path, an absolute one is taken as it stands.
The suggestion offered is absolute — the other panel's directory plus the
deduced name — so leaving it alone puts the archive where copying would put it,
and typing a bare name puts it beside the entries being packed.

**An archive that would pack itself is refused before anything is written.**
`Archive.wouldPackItself` answers true when the target *is* one of the sources
or lies inside one of them, and `Update` turns that into `TargetWithinSource`,
an `OperationError` like any other, worded by `OperationErrorMessage`. An
archive beside what it packs is fine — the sources are a fixed list of names,
and a file created afterwards is not one of them. The comparison ignores case,
which on Unix can refuse a target that two directories differing only in case
would have made legal; refusing too much is the safe side of a rule that
otherwise packs an archive into itself.

**An archive that already exists asks once.** `Shell` puts the
`OverwriteQuestion` wording through the plain `Confirm` port rather than the
five-answer `AskOverwrite` one, because there is a single file to decide about
and no series of collisions to settle. A refusal never starts the batch.

**The other panel gets the cursor on the new archive.** `PackFinished` carries
the archive's name, so `finishPack` re-reads the active panel keeping the marks
of what failed and the other one focused on the archive that just appeared. A
target typed somewhere else simply finds no such entry there and the cursor
falls to the top.

## Libraries and licences

`SharpZipLib` (MIT) today and, when 7z arrives, `SharpCompress` (MIT): both
fully managed, so the single-file publish of P8.3 stays intact, and neither is
copyleft. The wrappers around the native `7z.dll` were rejected on both counts —
they are LGPL, they need a native library shipped per platform, and an LGPL
assembly embedded in a single-file executable carries a relinking obligation.
`SharpCompress` writes 7z in managed code (non-solid, LZMA/LZMA2), which is what
makes it the obvious next step; what it cannot do is write an encrypted archive
in any format, which is why zip goes through SharpZipLib even today.
