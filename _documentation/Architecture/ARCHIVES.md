# Archives

`Shift+F1` packs what the active panel points at into an archive in the other
panel, `Shift+F2` empties an archive into it, `Enter` walks into one as though
it were a directory, and from a panel standing inside a kind that can be written
the ordinary keys copy out of it, add to it, remove from it and move in either
direction. Zip, tar.gz and 7z can be created and changed; tar, tar.bz2, tar.xz
and rar are read.

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
every place that has to decide asks it rather than naming a format. **A kind
that can be created can be changed**: zip, tar.gz and 7z are all three, and what
differs is only how — zip is edited where it lies, the other two are written
again from what they held. Tar, tar.bz2 and tar.xz are read and nothing more,
because nothing here writes them and a changer has nothing to write into; rar
never gains one either, since its compression is proprietary and the licence of
the unrar sources forbids building a compressor from them. The flags are
what every layer above is built against — the pack dialog offers the creatable
half, `Update` refuses a key that would change an unchangeable kind, and
`ArchiveReader` opens nothing whose `CanRead` is false — so a kind gains a
feature by turning one flag on, once the code behind it exists.

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
format but not through `SharpCompress`, whose writer takes no password at all,
which is why 7z says `false` and the field sinks the moment it is chosen.

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
tar.gz through the BCL's `TarWriter` over a `GZipStream`, and 7z through
`SharpCompress`'s `SevenZipWriter`. All three are streaming APIs over the stream
the mock file system hands out, which is what keeps the whole feature testable
without a disk. The split is not arbitrary: only SharpZipLib can encrypt a zip,
only `System.Formats.Tar` knows a directory entry and a modification time as
first-class things rather than as a name ending in a slash, and only
SharpCompress writes 7z in managed code at all. The choice is a match on
`ArchiveFormat.kind`, so the three writers are picked out of the one kind
vocabulary; a kind none of them knows cannot reach it, because only a creatable
kind is a format at all.

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

**A write that cannot be finished leaves nothing behind either.** Closing a
writer is where a zip's central directory is written, so a disk that fills up
fails there rather than on any one entry — and the file left behind would not
be a partial archive but an unreadable one. `write` therefore runs the whole
packing through `Operations.attempting`, and a failure deletes the file and
answers with that error for every source, exactly as cancelling does. A
*source* that fails is the other story and still leaves a real archive holding
what did get in.

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
and a file created afterwards is not one of them. The comparison is
`FileSystemPath.atOrUnder`, so it ignores case on Windows and minds it on Unix:
two directories differing only in case are two directories where the file
system says they are.

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

**"Below this level" is one rule, and the name says whether it is strict.**
`ArchivePath.strictlyUnder` and `atOrUnder` take a level and the names an entry
is keyed by, and read the same way `FileSystemPath.strictlyUnder` and
`atOrUnder` do for a path on disk. The listing, the measurement and the search
ask the strict one for the entries *below* a level; the extraction and the
rewrite ask the other for the entries *at or below* a target, since a target
names an entry as well as a level. There used to be three copies of the first
and one of the second, all spelled out by hand.

**One reader under both, with two openers chosen by kind.** `ArchiveReader` is
where an archive is opened, and it opens one only for the length of a walk:
`withItems` takes an `ArchiveStep` and calls it once per entry with an
`ArchiveItem` — the entry's `ArchiveEntryInfo` and an `OpenContent` that opens
its content as a stream — until the step answers `StopReading` or the entries
run out. Zip, 7z
and rar go through `ArchiveFactory.OpenArchive`, which seeks and reads the
archive's own table of contents; the tar family goes through
`ReaderFactory.OpenReader`, which streams, because `ArchiveFactory` sees a
`tar.gz` as a gzip holding one file and stops there while the reader unwraps
the compression and hands over the tar's own entries. Both read the stream
`IFileSystem` opens, so all of it is tested on `MockFileSystem` — the archives
the tests read are the ones `Archive.write` wrote a line earlier.

**A solid 7z is read sequentially by the opener it already has.** 7z archives
share one LZMA2 stream between their entries, so reaching an entry by index
could in principle mean decompressing every entry before it again, and
SharpCompress documents `IReader` as the sequential path for that. P15.17 timed
it rather than assuming: within one walk, extracting a solid 7z is linear in the
number of entries and costs the same as extracting a zip, which is not solid at
all — the library keeps the stream open across sequential `OpenEntryStream`
calls. So 7z stays with zip and rar on `ArchiveFactory`, and the split between
the two openers remains the one thing that forced it: `ArchiveFactory` cannot
see inside a `tar.gz`. The measurement is in
[Measurements](../Progress/MEASUREMENTS.md#unpacking-a-solid-7z-p1517-2026-09-24).

**What may be kept is separated from what may not, and the compiler holds the
line.** `ArchiveEntryInfo` — the names a key is made of, the entry's
`ArchiveEntryKind`, its size and its time — is an ordinary record: a walk may
collect every one of them and hand the list on. `ArchiveItem`, which carries
the `OpenContent` beside it, is a `[<Struct; IsByRefLike>]` record, so it cannot
be put in a list, stored in a field or captured by a closure that outlives the
step; the reader for a tar has no way back to a previous entry, and a content
function kept past its step used to compile and throw
`ObjectDisposedException`. Where a step needs the content elsewhere it passes
the `OpenContent` itself, which is an ordinary function, and where it needs a
lambda over the entry it binds `item.Info` first.

**Content is handed over as a stream, not as a block of memory.** `OpenContent`
answers the reader's own entry stream, and every caller reads it a buffer at a
time inside the step: unpacking copies it into the file it is writing, the
content search hands it to `ContentMatch`, which stops at the first match. Only
the rewrite of a 7z or a tar.gz still buffers, and for a reason of its own that
[Changing an archive](#changing-an-archive) records. What it replaced was a
`WriteTo` that every
caller answered with a `MemoryStream`, so a text search over an archive
allocated every entry it visited and `F3` on a 4 GB entry allocated 4 GB.

**One entry can also be opened on its own, outside a walk.** `entryInfo` walks
to an entry and answers its `ArchiveEntryInfo`; `openContent` opens the archive
and answers a stream over that one entry which *owns* the archive — disposing
the stream closes the entry, the archive and the file under it. That is the one
place content outlives a walk, and the type says so: an ordinary `Stream`, not
an `ArchiveItem`. It exists for the viewer, which has to answer reads for as
long as the user keeps the entry on screen.

**A kind says whether an entry is a directory, a file or an encrypted file.**
`ArchiveEntryKind` is one value where there used to be two booleans that could
disagree — `IsDirectory` and `IsEncrypted` — and `ArchiveEntryInfo.isDirectory`
and `isEncrypted` are the two questions anything asks of it.

**The index of an archive is read once and kept while the file is unchanged.**
`ArchiveIndex.read` answers every entry's `ArchiveEntryInfo`, and remembers the
last four archives it was asked about, keyed by path, length and last write
time. A panel standing in an archive lists a level by filtering that list, so
walking from level to level reads the file once instead of once per `Enter`;
`Archive.needsPassword` and `SearchListing.heldIn` are answered from the same
list, and so are the levels `DirectoryMeasure` reports. The cache hangs off the
`IFileSystem` it was read through — a `ConditionalWeakTable` — so the
application has one and every test has its own, and a file written again under
the same name is read again because its stamp no longer matches. Measured on a
zip of 20,000 entries over a mock file system: reading the index costs 80–100
ms, and a level change after it costs 4–8 ms, `needsPassword` about 1 ms and
`heldIn` about 0.2 ms.

**A name no readable kind owns is refused before the file is opened.** `read`
asks `ArchiveKind.recognise` and the capability table first, so `notes.txt` and
`data.bin` come back `KindCannotBeRead` without a byte being read — and so
would a kind whose reader had not landed yet, which is what the flag is for. The rest of the failures are
values as well: `ArchiveNotFound` for a file that is gone, `ArchiveAccessDenied`
for one the operating system will not hand over, `PasswordNeeded`
when the library refuses the encryption, and `ArchiveDamaged` carrying the
library's own reason for everything else, which is what a file that only looks
like an archive produces. Access is its own case rather than damage because
every layer above reads the wording: a read-only zip refusing `F5` into it, and
a directory that will not take the rewritten 7z, both used to explain
themselves as a broken file and sent the user looking for the wrong thing.

**An entry name is read as UTF-8 unless the bytes say otherwise.** A zip says
which of the two it used — general purpose bit 11 — and every other kind says
nothing at all, so there is no one encoding to decode a name with.
`ArchiveNames.decode` is the rule, installed on `ReaderOptions` as
SharpCompress's `CustomDecoder`: an entry the archive *calls* UTF-8 is UTF-8,
and for everything else the bytes decide — valid UTF-8 is UTF-8, anything else
is the legacy code page. `SharpZipLib`'s `ZipFile` gets the same legacy encoding
through `StringCodec.FromEncoding` when a zip is changed, so the name the panel
shows is the name the update matches on; that codec is read-only in effect,
since an entry added under a non-ASCII name still goes out flagged as UTF-8.

**The legacy code page is the culture's, and validity is judged strictly.**
`ArchiveNames.legacyCodePage` answers `TextInfo.OEMCodePage` — 852 for a Polish
Windows, which is what the tools that wrote those archives used — and
`legacyEncoding` falls back to Latin-1 when the culture names no code page, when
it names UTF-8, or when nothing answers to it; Latin-1 because it maps every
byte to a character and so loses nothing it cannot explain. The UTF-8 test is a
strict `UTF8Encoding` rather than `TextEncoding.detect`, whose tolerance of a
sequence cut short by the end of the probe is right for a file's first 8 KB and
wrong for a name, where a cut sequence means the bytes are not UTF-8. The code
page and the decoder it is wrapped in are resolved once per process —
`ArchiveNames.legacy` and `reading` are `lazy` — so registering the code-page
provider and looking the encoding up no longer happen on every archive open.

Writing was never the problem: a zip Marbas writes carries bit 11 and UTF-8
bytes, so other tools read it back intact. What this fixes is the other
direction — a zip or a tar somebody else wrote with names in an OEM code page,
which used to arrive as replacement characters, and a tar written in UTF-8 with
no way to say so, which a plain "assume the code page" rule would have broken.

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
`Shift+F2` can ask once and run again. That question is answered from the
index, and an archive whose index cannot be read at all answers with the
reason — `needsPassword` is a `Result`, so a damaged file is no longer read as
"no password needed" and reported later under a different message. A password
that is *wrong* is a different story: the library throws while reading the
entry and the target fails with the reason it gave, and an archive that hides
its own headers is opened with the password it was given rather than asked
about first.

**Every target opens the archive again.** `unpackTarget` opens its own walk
through `ArchiveReader.withItems`, so a batch of five targets reads the archive
five times. For a zip that is five reads of the table of contents; for a
`tar.gz` it is five decompressions, which is the price of keeping the streaming
reader — which can only go forwards — and the per-target `ItemResult` in the
same design. The index cache does not help here: extraction needs content, and
content is only ever handed out inside a walk.

## The walk from key to unpacking

`UnpackRequested` → `ExtractTargets` → the dialogs → the batch →
`UnpackFinished`, the shape `F5` and `Shift+F1` already follow. `Shift+F2`
carries it, on the second slot of the shift row of the key bar, and the targets
are the ordinary operation targets: every marked entry, or the one under the
cursor.

**Every target is handed to the archive machinery, whatever it is.** `Update`
does not ask which targets look like archives — a target that is no archive at
all comes back as `KindCannotBeRead` from `Archive.extract` and is reported like
any other failed item, which is the same answer a `release.tar` gets today for a
different reason. Silence would leave `Shift+F2` on a text file looking broken.

**`Shell` asks everything before the batch starts.** A batch runs off the UI
thread, so no dialog may open inside it. For each archive `Shell` therefore
asks, in order: the password, when `Archive.needsPassword` says an entry is
encrypted and none was given — the prompt answers a `PasswordOutcome`, one of
`NotNeeded`, `NotGiven` or `Given`, and the middle one drops the archive from
the batch, which covers a dismissed prompt and an empty field alike, since
neither is a password to unpack with — and then the five-answer overwrite
question, for each top-level entry that already exists in the target directory.
What the user approves becomes the archive's list of `ArchivePath`s; approving
nothing drops the archive too. An archive that cannot even be listed keeps
`ArchivePath.root` as its target, so the batch itself produces the honest error
instead of `Shell` inventing one.

**The overwrite question is asked about the top level only.** `Operations.approve`
is the same function copying and moving use, fed with the archive's top-level
entries spelled `…\backup.zip\notes.txt`, so it computes the same landing places
and asks the same five answers. A file deeper inside a directory being unpacked
is overwritten without a question — the level the user can see is the level they
answer for.

**An archive that fails answers under its own name.** `Archive.extractAll` runs
the plans one after another and turns an `Error` from `extract` into a single
`ItemResult` whose source is the archive file itself, carrying
`Operations.ArchiveFailed`. That case wraps the `ArchiveError` whole rather
than flattening it to a string, so
the failures dialog words it through `ArchiveErrorMessage` and nothing in
`FileSystem` has to know how to phrase anything.

**Both panels are re-read afterwards.** `UnpackFinished` goes through the same
`finishTransfer` that `F5` and `F6` use: the active panel keeps the marks of
what did not make it, and the other panel — where the entries landed — is read
again from disk.

## Where a panel stands

**A panel stands at a location, and a location is one of two things.**
`PanelLocation` is either `OnDisk` — a `FileSystemPath` naming a directory —
or `InArchive`, the archive's own `FileSystemPath` together with the
`ArchivePath` of the level being shown. `PanelState.Path` is one of those
rather than a bare path, so a panel inside an archive is a state the type
system admits instead of a disk path that happens to point at a file. Nothing
builds an `InArchive` yet: `Enter` on an archive is P9.11. What this task
bought is that every operation had to say out loud what it does with a place
that is not a directory.

**An operation asks the location for a disk path instead of assuming one.**
`PanelLocation.directory` answers `Some` on disk and `None` inside an archive,
and it is the only way to a `FileSystemPath` from a panel.
`PanelLocation.combine` puts a name in the location the same way, so
`operationPaths` answers the empty list inside an archive and every effect that
needs paths — copy, move, delete, pack, unpack — falls to its own "nothing to
do" case. Making a directory, making a file and typing a target are gated in
`Update`, which asks for the directory before it opens a dialog rather than
after it, so no popup can be answered into a place that cannot take the answer.
Re-reading a panel — a refresh, a hidden toggle, the reload after a batch —
leaves an archive panel as it stands, because the listing it would need is the
one P9.11 brings.

**The location is what the title spells.** `PanelLocation.value` gives a
directory its own path, and a place inside an archive the archive's path with
the level's names below it, separated the way the platform separates them:
`C:\work\backup.zip\docs`. That is the same spelling `Archive.extract` already
uses when a target inside an archive fails, so a place in an archive is written
one way everywhere in the app. Spelling needs `IFileSystem` — the separator is
the platform's, and only `IFileSystem` may be asked what it is — which is why
`PanelTitle.describe`, `FilePanel.render` and `AppView.render` now take a file
system: rendering a panel means rendering where it stands.

**A session is restored to a place that is certainly there.**
`PanelLocation.holding` answers the directory on disk that holds the location —
itself when it is one, the directory holding the archive when it is not — and
that is what goes into the settings file when the app quits. An archive can be
deleted, renamed or moved between two runs, so a panel that was inside
`backup.zip` comes back in the directory that held it rather than failing to
open something that is gone.

**One place spells a place inside an archive.** `ArchivePath.spelled` folds the
level's names onto the archive's own path with the platform's separator, so
`C:\work\backup.zip\docs\deep` is written the same way by the panel title, by
the viewer's header, by the failure of an extraction target and by the message
that refuses `F4`. Before P9.11 the extraction spelled its own with the
archive's `/`-separated value; now there is one function and one answer.

## Walking into an archive

**A panel is listed by asking its location, and both answers are the same
list.** `AppState.loadLocationFocusing` takes a `PanelLocation` and reads it
through `DirectoryListing.listForDisplay` or `ArchiveListing.listForDisplay`;
everything after that — the cursor to focus, the marks cleared, the sort and
the hidden setting carried over from what the panel was — is one code path for
both. `ArchiveListing.listForDisplay` is the mirror of the directory one: it
conceals what the panel conceals, orders by the panel's sort, and puts a `..`
in front carrying the archive file's own modification time. That entry is
always there — inside an archive there is always a way out, unlike a drive root
on disk, which has none.

**A listing that fails is one error type, whatever failed.**
`ListingError` gained `ArchiveUnreadable`, which carries the `ArchiveError`
whole rather than flattening it to a string, the way `Operations.ArchiveFailed`
already did. `ListingErrorMessage` words it by delegating to
`ArchiveErrorMessage`, and `ListingErrorMessage.title` picks the caption —
"Cannot open directory" for a directory, "Archive" for an archive — so a
damaged zip explains itself under the right heading and `AppState` still has
one error to map. `ArchiveError` moved out of `ArchiveReader` into a file of
its own for this: the type is a value like `ContentError`, and it has to be in
scope before `DirectoryListing`.

**`Enter` decides from where the panel stands and what the cursor is on.** On
disk a directory is descended into and `..` climbs, as before; a *file* is
offered to `ArchiveReader.kindOf`, and a kind that can be read is opened as
`InArchive(file, ArchivePath.root)`. Inside an archive, a directory walks a
level in, `..` walks a level out with the cursor on the level just left, and
`..` at the top leaves the archive for the directory holding it with the cursor
on the archive file — the same courtesy climbing out of a directory has always
paid. A file inside an archive opens nothing: an archive inside an archive would
have to be unpacked to be read, and that is not a promise this phase makes.

**Since P10.3 a `ForOpening` row wins over the walk, and `Alt+W` is how to
walk anyway.** An archive whose extension names a program in the association
table is *run*, not opened as a location — the point of naming a program for
`zip` is to reach it. That would otherwise have made the archive browser
unreachable for whoever does so, which is why `Alt+W` — and `Ctrl+PgDn` beside
it, since P10.7 — carries the walk on its own: `WalkIntoRequested` is the old
meaning of `Enter`, unchanged and unconditional. A file inside an archive no longer merely does nothing when
`Enter` is pressed on it; it refuses with `ArchiveEntryNotRunnable` under its
spelled name, the way `F4` refuses with `ArchiveEntryNotEditable`, because
running a copy out of the workspace would promise a round trip back into the
archive that nothing here makes.

**Everything the panel already did keeps working, because nothing else
changed.** The entries are `Entry` values, so sorting, the hidden toggle,
marking, the search, the colours, the footer and the status line need no
archive-specific code. `Ctrl+R` re-reads the archive through the same reload as
a directory, and `Alt+F1`/`Alt+F2` are the way out to a drive when the panel is
inside one.

**`F3` streams an entry into the viewer.** `ViewRequested` raises `ViewFile`
for a file on disk and `ViewArchiveEntry` for one inside an archive;
`FileWindow.openEntry` asks `ArchiveReader.entryInfo` for the entry's size and
hands the viewer a `Source` over a `RewindingStream`, labelled with the entry's
spelled path, so the header names `C:\work\backup.zip\docs\notes.txt` and every
viewer command works as it does on disk. `Shell` asks for a password first when
`Archive.needsPassword` says the archive carries one, exactly as `Shift+F2`
does, and an entry the archive no longer holds comes back as `ArchiveNotFound`
under its own spelled name.

**A forward-only entry is made to look like a file, by opening it again.** The
viewer reads byte ranges in whatever order the user scrolls in, and a
decompressing entry stream only goes forward. `RewindingStream` takes the
entry's length and a way to open it from the start: a read ahead of where it
stands skips forward through a 64 KiB scratch buffer, a read behind it disposes
the stream and opens a fresh one. Scrolling down a large entry therefore costs
one pass, and jumping back to the top costs one more — where the old answer was
to decompress the whole entry into memory before the first line appeared. The
stream is opened lazily, so an entry the viewer is closed on without reading is
never decompressed at all, and the `Source` owns it: closing the viewer closes
the entry, the archive and the file. The one thing this gives up is what an open
file handle gave: the archive is re-opened during viewing, so an archive deleted
under the viewer fails a later scroll rather than reading on from a handle
nobody can see.

**`F4` is the one key that refuses.** Editing an entry in place would mean
rewriting the archive when the editor returns — and knowing when it returned,
which a detached editor does not promise. `EditRequested` therefore answers
`ArchiveEntryNotEditable` for any entry inside an archive, an `OperationError`
like every other, worded "cannot be edited inside the archive. Unpack it
first." `..` refuses nothing, because it is not an entry to edit.

**Which kinds are proven to browse, and which are taken on the reader's
word.** The tests write a zip and a tar.gz on `MockFileSystem` and walk into
both, which exercises each of the two openers `ArchiveReader` picks between —
the seeking one and the streaming one. Every readable kind is listed and
extracted by name in `EveryKindTests`, rar excepted: nothing on a developer's
machine can create a rar, so that one is taken on the reader's word until a
fixture is checked in. Nothing above `ArchiveReader` asks what kind it is
holding, so browsing follows reading wherever reading goes.

**`Shift+F1` and `Shift+F4` still do nothing from inside an archive.** Packing
an entry that is already in an archive would have to unpack it first, and
creating a file means handing a path to an editor; neither is a promise this
phase makes. Both ask the location for a disk directory, find none, and stop —
the gates P9.10 put in.

## Copying out of an archive

**`F5` asks where the panel stands before it asks what to do.**
`AppState.transferSource` answers what the operation targets *are* from where
the panel is: `FromDisk` of paths when it stands on disk, `FromArchive` of the
archive and one `ArchivePath` per target when it stands inside one, and `None`
when there is nothing under the cursor to take (`..` alone). `Update` turns the
first into `CopyPlaces([ FromDisk … ], …)` and the second into
`CopyPlaces([ FromArchive … ], …)` — one effect either way since P15.7 — so the
key has one message, the state has one decision and neither has to ask what kind
of place a panel is twice. Both need a *disk* directory to write into, which is
the other panel's; a target panel standing inside an archive is P9.13 and does
nothing today.

**The targets are the level's own names, and the extractor does the rest.**
An entry is named `ArchivePath.child level name`, so copying `inner.txt` out of
`backup.zip\docs` hands over `docs/inner.txt` and it lands as
`C:\out\inner.txt` — the rule `Archive.extract` already followed, where a
target arrives under its last name with the tree below it. A marked directory
is one target and brings everything under it; nothing walks the archive twice
to expand it.

**`Shift+F2` and `F5` are the same machinery with different targets.**
`Shell.planFor` asks the password once and then the overwrite question, and
answers an `ExtractPlan` or `None`; `Shift+F2` feeds it the archive's top-level
entries, `F5` feeds it the panel's targets. `approvedEntries` is where the
five-answer question is asked for both: each `ArchivePath` is spelled
`…\backup.zip\docs\inner.txt`, the spelled paths go through the same
`Operations.approve` that copying and moving use — which computes the landing
place and asks per collision — and the approved ones are mapped back to the
`ArchivePath`s they came from. The pairing is what lets a target at any depth
survive the round trip; the old code mapped back by name and only worked at the
top level.

**It is a copy, so it says "Copying" and answers `CopyFinished`.** The batch
runs under `CopyTitle`, reports each entry through the same `BatchControl` the
progress window drives, stops on `Esc` where it stood, and finishes through
`finishTransfer` — the active panel re-read (through the archive, since that is
where it stands) keeping the marks of whatever did not make it, the other panel
re-read from disk. Nothing distinguishes it from `F5` between two directories
except that the source is read out of a file.

**The archive is never written.** Extraction only reads: the file's bytes are
the same after the batch as before it, which is asserted rather than assumed.
An archive that cannot be read fails the whole plan as one `ItemResult` whose
source is the archive file, the way `Shift+F2` reports it.

## Changing an archive

**A zip is updated in place, not rewritten.** `SharpZipLib`'s `ZipFile` update
API — `BeginUpdate`, `Add`, `Delete`, `AddDirectory`, `CommitUpdate` — runs over
the very stream `IFileSystem` hands out, with `MemoryArchiveStorage` in its
default direct mode, so the archive on disk is edited where it lies and the
whole feature is tested on `MockFileSystem` like everything else. Adding an
entry to a large archive therefore costs the entry, not the archive. `Archive`
wraps it in one private `changing` helper that opens the file, hands the
`ZipFile` to the work, and then either commits or aborts; nothing is written
until the commit, which is what makes cancelling free.

**Four operations, one shape.** `Archive.addEntries` puts disk sources at a
level — reusing the very `Sink` and tree walk `Archive.write` packs with, so a
directory brings everything below it and the entry names are relative to the
source's own parent. `Archive.removeEntries` drops every entry under each
target, so removing a directory takes its tree. `Archive.makeEntryDirectory`
adds one directory entry. `Archive.moveIn` and `Archive.moveOut` are the two
compositions: in is add-then-delete-the-source, out is extract-then-remove-the-
entry. All of them answer one `ItemResult` per target the way copying does, and
`Esc` stops them.

**A move drops one side only once the other side has it.** `moveIn` deletes a
source only for the results that came back `Ok`, and `moveOut` removes an entry
only for the targets that actually landed on disk; a failure therefore loses
nothing — the entry is in exactly one of the two places, never neither.
Matching a removal failure back to the entry it belongs to goes by name, which
is sound because a panel's targets all stand at one level and a level cannot
hold two entries with the same name.

**Cancelling leaves the archive exactly as it was.** `Esc` in the progress
window makes the batch abort its update instead of committing it, and every
target then answers `Cancelled` — the same answer `Archive.write` gives when the
half-written file is deleted. There is no half-changed archive to explain.

**Two refusals, in two layers, for two reasons.** Whether a *kind* can be
changed is a question about the name, so `Update` asks `Archive.changeableKind`
and answers `KindCannotBeChanged` before a dialog is opened or a batch is
started: `F7` inside a plain tar never prompts for a name, `F8` never asks for a
confirmation. Whether *this archive* can be changed also depends on what is
inside it — `SharpZipLib` cannot create an AES entry, so adding to a
password-protected zip would either fail or quietly store the new entry in the
clear — and that question needs the file read, so `Shell` asks
`Archive.changeRefusal` before it starts a batch and reports
`ProtectedArchiveCannotBeChanged` through the ordinary message box.
`Archive.addEntries` and its siblings ask again themselves, so the rule holds
whoever calls them.

**The overwrite question is the same five answers, asked about a place inside
an archive.** `Operations.approveInto` is the standing-answer walk lifted out of
`Operations.approve`: it takes a function from a source to its landing place
and a function that says whether that landing place is occupied, so the disk
version passes `FileSystemPath.kind` and the archive version passes membership
of the level's own names. What the user sees is `…\backup.zip\notes.txt`, the
same spelling everything else uses.

**Which key does what, from where.** `AppState.transferTarget` answers
`ToDirectory` or `ToArchive` the way `transferSource` answers `FromDisk` or
`FromArchive`, and `Update` matches the pair: `F5` copies, extracts or adds;
`F6` moves, moves out or moves in; `F8` deletes paths or removes entries; `F7`
makes a directory on disk or an entry in the archive. Every pair now answers,
the two archives included: there is no combination of the two panels that
silently does nothing.

## Crossing between two archives

**An entry crosses through the workspace, because neither library hands one
archive to another.** `Archive.copyAcross` takes a scratch directory out of the
session workspace, extracts the targets into it with the very
`Archive.extract` that `F5` out of an archive uses, adds what arrived to the
other archive with the very `Archive.addEntries` that `F5` into one uses, and
removes the scratch directory whole in a `finally` — so the workspace is empty
afterwards whether the batch finished, was cancelled or threw. Nothing about
the crossing is per-kind: a zip entry lands in a tar.gz and a 7z entry in a zip,
because both halves were already written against the capability table.

**A target that did not arrive is never dropped from the source.**
`Archive.moveAcross` is `copyAcross` followed by the same `droppingArrived`
that `moveOut` uses, so an entry is removed from the archive it came from only
once the other archive holds it — the rule every move in Marbas follows. What
failed on the way out keeps that failure; what failed on the way in replaces
it, which is what `carryingOver` is for: the two halves answer one `ItemResult`
per target, and `carryingOver` joins them through the scratch path each target
landed on, so two targets of one name from different levels keep their own
outcomes.

**Both archives are gated, and each for its own reason.** `F5` needs only the
*target* to be changeable — copying out of a rar into a zip is fine. `F6` needs
the source as well, since it removes from it, which is why `Update.changing`
now takes a list of archives and names the first that refuses. `Shell` asks the
source archive for its password once before the batch and puts the target
level's own names through the same five-answer overwrite question, spelled
`…\into.zip\notes.txt` like every other place inside an archive.

**`F6` skips its prompt when an archive is one of the two sides.** The prompt
exists to let a target be typed or a single entry renamed, and neither is
offered inside an archive, so there is nothing to type: the key goes straight
to the batch with the other panel as the target, exactly as `F5` does. Moving
*out* of an archive is gated on the source archive being changeable, which is
why moving out of a rar is refused where copying out of it works.

**Deleting inside an archive is always "for good".** A zip has no trash, so
`F8` and `Shift+F8` both mean the same thing there, and the confirmation is the
`Permanently` wording rather than the trash one. The scope warning still
appears: a directory among the targets says that everything inside it goes with
it. The removal runs as a batch of its own, under `Deleting`, because dropping
an entry rewrites what follows it in the file.

**`F7` stays one transition, not an effect.** `AppState.makeEntryDirectory`
mirrors `AppState.makeDirectory` exactly — make it, re-read the panel with the
cursor on the new entry, remember the name for the next prompt — because adding
one directory entry is as quick on a zip as it is on a disk. A name the level
already holds is `TargetExists`, spelled the archive way.


## The remaining formats

**7z is written by the same walk every other format is.** `Archive.write`
matches the format onto a third `Sink`, over `SharpCompress`'s
`SevenZipWriter` with LZMA2 and no password — the writer takes none, which is
why `carriesPassword` says `false` and the dialog's field sinks when 7z is
chosen. The compression levels map onto the encoder's own 0/5/9; `Stored` is
LZMA2 at level 0 rather than a stored entry, because the writer accepts only
LZMA and LZMA2 and refuses `CompressionType.None` outright. Cancelling deletes
the half-written file, marks are kept and every source answers, exactly as zip
and tar.gz do — none of that is per-format code.

**Everything but zip is changed by being written again.** A zip has an update
API and is edited where it lies; nothing else here does, so `Archive.rewriting`
opens a second archive beside the first (`<name>.marbas-new`), copies over every
entry that is not being dropped, writes the new ones through the same `Sink` the
packing walk uses, and moves the result over the original. Committing is that
move; aborting — `Esc`, or a failure — deletes the new file and leaves the
original untouched, which is the same promise the zip path makes by aborting its
update. What it costs is a recompression of the whole archive per change, and
the memory of the largest entry, since a survivor is buffered between the reader
that pushes it and the writer that pulls it.

**The working file stays beside the archive, and it never lands on a name that
is taken.** Committing a rewrite is `fs.File.Move` over the original, which is
an atomic rename only while both paths sit on one volume — putting the working
file in the system temporary directory would turn the commit into a copy of a
whole freshly written archive, across a window in which the target is
half-written. So it stays put, and `Archive.workingFile` walks
`<archive>.marbas-new`, `<archive>.marbas-new.2` and on until it finds a name
nothing answers to, the way `Trash.freeName` does. A file of the user's own
carrying that name is therefore read, written and left exactly as it was; it
used to be truncated on the way in and deleted on the way out.

**The working file is removed on every way out, not only the two expected
ones.** Committing moves it, aborting deletes it — and a writer that throws
halfway used to leave it beside the archive for good, since neither branch was
reached. The removal is a `finally` now, so the failing path cleans up too and
the commit's own `delete` simply finds nothing there. What is *not* swept is a
working file an earlier run left behind after a kill: nothing distinguishes it
from a file the user owns, and another Marbas may be writing it this minute, so
it is stepped over rather than deleted.

**One rewriter serves every writable kind, because nothing in it is per-format
but the writer it opens.** `rewriting` takes the kind, asks `ArchiveFormat` for
it and hands the whole job to `intoArchive` — the very function packing goes
through. That is what makes tar.gz changeable: it was never the gzip stream that
stood in the way, only the absence of a rewriter, and 7z had already proved the
shape. A kind that could be changed but not created would have nowhere to write
and is refused where the format is looked up; the capability table is written so
that cannot arise.

**A sink writes an entry, wherever the entry comes from.** `Sink.AddFile` takes
a `Stored` — a size, a modification time that may be unknown, and a function
that opens the content — so the packing walk hands it a file on disk and the
rewriter hands it an entry buffered out of the old archive, and neither writer
knows the difference. `Sink.AddDirectory` takes the time the same way, which is
how a rewritten archive keeps directory times a fresh one has none of. The keys
a sink is given are the archive's own; `Archive.at` derives the level-scoped view
that `F5` and `F7` write through, so one writer serves both the survivors,
copied at their own keys, and the additions, prefixed with the level.

**Both changers answer the same shape, so nothing above them knows which one
ran.** `Archive.changeWith` takes the entries to drop, the level to write at
and a piece of work, and hands that work what the archive holds and a `Sink` to
write into; `addEntries`, `removeEntries` and `makeEntryDirectory` are written
once against it. The kind picks the changer — zip updates, everything else
rewrites — and `changeableKind` is what says a kind picks neither.

**The tar family is read, not written.** Tar, tar.bz2 and tar.xz reach
`ReaderFactory`, which sniffs the compression and hands over the tar's own
entries, so turning `CanRead` on was the whole of the work; there is no writer
for them because tar.gz already covers "a tar with compression" on the writing
side, and a second one would be a format list, not a feature. The tests write a
plain tar with `System.Formats.Tar` and a tar.bz2 with `SharpZipLib`'s
`BZip2OutputStream`; xz has no managed writer anywhere in the dependencies, so
a 224-byte `sample.tar.xz` is checked in as an embedded resource and read into
the mock file system like any other archive.

**Rar is the one kind no test creates.** Nothing in the toolchain writes rar —
that is the point of the format's licence — so the read side rests on
`SharpCompress` and on the capability table saying `CanRead` and nothing else.
A small rar dropped into `Marbas.Tests/Fixtures` would close that gap with one
test shaped like the tar.xz one; until then rar is declared, not demonstrated.

## Libraries and licences

`SharpZipLib` (MIT) writes and updates zip, `System.Formats.Tar` over a
`GZipStream` writes tar.gz, and `SharpCompress` (MIT) reads every kind the
table admits and writes 7z. All are fully managed, so the single-file publish
of P8.3 stays intact, and none is copyleft. The wrappers around the native
`7z.dll` were rejected on both counts — they are LGPL, they need a native
library shipped per platform, and an LGPL assembly embedded in a single-file
executable carries a relinking obligation. What SharpCompress cannot do is
write an encrypted archive in any format, which is why zip goes through
SharpZipLib for writing while SharpCompress reads it back, and why 7z carries
no password.
