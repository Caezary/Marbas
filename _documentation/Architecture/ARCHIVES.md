# Archives

`Shift+F1` packs what the active panel points at into an archive in the other
panel; `ArchiveListing` reads one back level by level and `Archive.extract`
takes entries out of it. The keys that would let a user do the reading —
`Shift+F2` and browsing an archive from a panel — are the rest of phase 9 and
are not built yet.

## The vocabulary

**A kind is what an archive is; a format is what one can be created in.**
`ArchiveKind` is the closed set of archives Marbas has a name for — `Zip`,
`Tar`, `TarGz`, `TarBz2`, `TarXz`, `SevenZip`, `Rar` — and
`ArchiveKind.extension` owns the suffix each one answers to. `ArchiveFormat` is
that set narrowed to the kinds that can be written: its case is private and
`ArchiveFormat.ofKind` answers `None` for a kind whose capabilities say it
cannot be created, so a rar target cannot be built even by mistake.
`ArchiveFormat.all` is the projection itself, in the order the kinds are
declared, and it is what the pack dialog offers; `ArchiveFormat.initial` is its
head, zip. There is no second list to keep in step: turning one flag on in the
table adds a format to the popup.

**One record says what can be done to a kind.** `ArchiveKind.capabilities`
answers an `ArchiveCapabilities` — `CanRead`, `CanCreate`, `CanChange` — and
every place that has to decide asks it rather than naming a format. Zip is all
three: it is read, written and changed entry by entry. Tar.gz is read and
written but never changed, because a gzip stream has no entry to replace
without rewriting the file. 7z and rar are read alone today — 7z joins the
written and the changed when P9.14 arrives, rar never does, since its
compression is proprietary and the licence of the unrar sources forbids
building a compressor from them. Tar, tar.bz2 and tar.xz claim nothing yet and
join the readable set with the same task. The flags are what the readers and
writers of P9.7 onwards are built against: reading is declared here before it
is coded, and `CanRead` false is what keeps a kind out of everything until its
reader lands.

**Recognition reads a compound suffix whole.** `ArchiveKind.recognise` takes an
`EntryName` and matches the longest suffix first, so `release.tar.gz` is a
tar.gz and not a tar, and `release.gz` is no archive at all — gz alone is not a
kind Marbas names. The comparison ignores case, and a name that is nothing but
a suffix is not an archive either: `.zip` is a hidden file, the same rule the
suggested name follows.

**The extension is a string, deliberately.** `FileExtension` normalises to a
single member — `FileExtension.ofName "a.tar.gz"` is `gz` — so a compound
suffix put through it would be a value that lies. `ArchiveKind.extension`
answers a plain string instead, read by recognition and by `ArchiveName`,
rather than bending the existing type around one case.

**A password is a property of the kind.** `ArchiveKind.carriesPassword` is
`true` for zip and `false` for everything else, and `ArchiveFormat` projects it
the way it projects the extension: whether an archive can be locked belongs to
what it is, not to the dialog that offers the field. 7z can carry one in the
format but not through `SharpCompress`, which is why it says `false` and the
field stays shut when P9.14 adds it.

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
What counts as an archive extension is what `ArchiveKind.recognise` answers, so
a kind Marbas cannot create loses its suffix just the same: `book.rar` suggests
`book.zip`.

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
ending in a slash. The choice is a match on `ArchiveFormat.kind`, so the two
writers are picked out of the one kind vocabulary; a kind neither of them knows
cannot reach it, because only a creatable kind is a format at all.

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

## Reading

**One level at a time, as the same entries a directory gives.**
`ArchiveListing.read` takes the archive's path and an `ArchivePath` and answers
the `Entry` list of that level — the very record `DirectoryListing.read` gives
back — so sorting, hidden entries, marks, the search and the panel's rendering
need no new code when a panel comes to stand inside an archive. Sizes and
modification times come from the entries; a name is classified hidden by its
leading dot and executable by its extension through the same two functions the
disk listing uses, with no attributes and no unix mode to add, because an
archive stores neither in a form every kind agrees on.

**A place inside an archive is an `ArchivePath`.** It is `/`-separated, never
rooted and never climbing: `ArchivePath.tryCreate` refuses a leading `/`, an
empty segment and any `.` or `..`, so a path that walks out of the archive
cannot be built in the first place. The root is the empty path, `child` walks
in, `parent` walks out, and `segments` hands over `EntryName`s rather than
strings, which is what makes a level inside an archive the same kind of thing
as a directory on disk.

**Directories are synthesised where the archive stores none.** Every entry key
is flattened to segments and everything below the level being read is grouped
by its next name. A group of one entry that sits exactly one level down and is
not a directory is a file; anything else is a directory — whether the archive
stored an entry for it or merely implied it by holding `docs/deep/leaf.txt`. A
stored directory keeps its own modification time, a synthesised one takes the
newest time below it, which is the only honest answer when the archive says
nothing. A level that holds nothing lists nothing, and so does a level that is
not in the archive at all: walking in is only ever offered for an entry that
was listed. Only `/` separates a level from the next: a backslash is an
ordinary character in a key, so an entry named `a\b.txt` is one entry and not
two levels, which is what the format says whatever platform reads it.

**One reader under both, with two openers chosen by kind.** `ArchiveReader` is
where an archive is opened: it answers a lazy sequence of `ArchiveItem`s — the
names a key is made of, whether the entry is a directory, its size, its time,
whether it is encrypted, and a `WriteTo` that streams its content — and both
the listing and the extraction read that one shape. Zip, 7z and rar go through
`ArchiveFactory.OpenArchive`, which seeks and reads the archive's own table of
contents; the tar family goes through `ReaderFactory.OpenReader`, which
streams, because `ArchiveFactory` sees a `tar.gz` as a gzip holding one file
and stops there while the reader unwraps the compression and hands over the
tar's own entries. Both read the stream `IFileSystem` opens, so all of it is
tested on `MockFileSystem` — the archives the tests read are the ones
`Archive.write` wrote a line earlier. `WriteTo` is only good while the walk
stands on its entry: the streaming reader has no way back.

**A kind that cannot be read is refused before the file is opened.** `read`
asks `ArchiveKind.recognise` and the capability table first, so `release.tar` —
a kind whose reader arrives with P9.14 — and `notes.txt` both come back
`KindCannotBeRead` without a byte being read. The rest of the failures are
values as well: `ArchiveNotFound` for a file that is gone, `PasswordNeeded`
when the library refuses the encryption, and `ArchiveDamaged` carrying the
library's own reason for everything else, which is what a file that only looks
like an archive produces.

**A password hides content, not names.** An AES zip lists its entries without
one — the names, sizes and times are in the clear, as the writing side already
said — so `read` asks for no password. `PasswordNeeded` is there for the kinds
that encrypt their headers; extraction is where a password is asked for, since
that is where content has to be decrypted.

## Extracting

**One request, a list of targets, an `ItemResult` each.** `Archive.extract`
takes an `ExtractRequest` — the archive, the password if there is one, and the
directory to write into — and a list of `ArchivePath`s to take out of it. It
answers `Result<ItemResult list, ArchiveError>`: the `Error` side is the
archive refusing as a whole (a kind that cannot be read, a file that is gone, a
password that is needed, an archive that is damaged), the `Ok` side is one
result per target the way `Archive.write` and `copyAll` answer per source.
`ArchivePath.root` as the only target is how the whole archive is emptied into
the destination, and it answers under the archive's own name.

**A target is written under its last name, with the tree below it.** Extracting
`docs/deep` into `C:\out` writes `C:\out\deep\leaf.txt`: the entry's path is
taken relative to the target's parent, exactly as `Archive.write` names entries
relative to the source's parent, so packing and unpacking are each other's
mirror. Directories stored in the archive are created even when they are empty,
directories only implied by a deeper entry are created on the way, and every
file gets its modification time back — set as UTC, because a
`DateTimeOffset.MinValue` on an entry that carries no time cannot be converted
to a local time west of Greenwich without falling off the calendar.

**An entry that would climb out of the target is refused.** Every entry's
landing place is resolved with `GetFullPath` and checked against the
destination; `docs/../../evil.txt` therefore fails its target with
`EntryOutsideTarget` and nothing is written outside. The check is on the
resolved path rather than on the key, so a key that climbs and comes back
(`docs/../docs/inner.txt`) is allowed, which is what it means: the same file.
`ArchivePath` cannot hold `..` at all, so this rule guards only what other
tools put in an archive.

**The first failure ends that target and no other.** Entries are walked one by
one and the first `Error` stops the walk for that target, leaving the rest of
the targets to run — the rule `Operations.allWatched` follows for copy and
move. A target no entry answers to is `SourceNotFound`, spelled the way a place
inside an archive will be spelled from P9.10 on: `…\backup.zip\docs`.

**Cancelling stops where it stood.** Each file reports its landing place
through `BatchControl` before it is written, `Esc` in the progress window makes
the next entry answer `Cancelled`, and the batch stops after the target it was
in. What was already written stays on disk — unlike packing, where the archive
is deleted, there is nothing half-made to remove: the files that arrived are
whole.

**A password is asked for before anything is unpacked, not after.** When the
kind can carry a password, none was given and any entry to be written is
encrypted, `extract` answers `PasswordNeeded` without writing a byte, so
`Shift+F2` can ask once and run again. A password that is *wrong* is a
different story: the library throws while reading the entry and the target
fails with the reason it gave.

**Every target opens the archive again.** `ArchiveReader.items` is a lazy
sequence that opens the file when it is enumerated, so a batch of five targets
reads the archive five times. For a zip that is five reads of the table of
contents; for a `tar.gz` it is five decompressions, which is the price of
keeping the streaming reader — which can only go forwards — and the per-target
`ItemResult` in the same design.

## Libraries and licences

`SharpZipLib` (MIT) writes zip, `System.Formats.Tar` over a `GZipStream` writes
tar.gz, and `SharpCompress` (MIT) reads every kind the table admits — and will
write 7z when P9.14 arrives. All are fully managed, so the single-file publish
of P8.3 stays intact, and none is copyleft. The wrappers around the native
`7z.dll` were rejected on both counts — they are LGPL, they need a native
library shipped per platform, and an LGPL assembly embedded in a single-file
executable carries a relinking obligation. `SharpCompress` writes 7z in managed
code (non-solid, LZMA/LZMA2), which is what makes it the obvious next step;
what it cannot do is write an encrypted archive in any format, which is why zip
goes through SharpZipLib for writing while SharpCompress reads it back.
