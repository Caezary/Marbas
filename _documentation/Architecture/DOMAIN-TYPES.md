# Domain types

**The vocabulary sits below every layer that speaks it.** `Marbas.Domain` is
the bottom of the stack above `Collections/`, and it holds the words rather
than the machinery: `PanelSide`, `PanelCursor` and `TabStep`, `EntryName`,
`SortOrder`, `HiddenEntries`, `Executability`, `SearchText`, `NamePattern`,
`SelectionMode`, `CursorDirection`, `DeleteScope`, `DeleteMode` and
`ProgramSettings` with the `FileExtension`, `ProgramPath` and `AssociationRole`
that key it. `PanelSide` went first because the settings file names the active
panel and `FileSystem` cannot reach `State`; the rest followed for the same
reason read the other way round. A type that describes *how a panel shows a
directory* is not a fact about the directory, and a type that names *what a
command means* is not state — so neither belongs to the layer that happened to
need it first. `FileSystem` and `State` are left with what they are for:
reading and writing the disk, and holding what the application currently is.
The line that decides a case is `IFileSystem`: nothing in `Domain` takes one or
knows a view exists, which is what keeps the dependency arrows pointing one
way, and it is why `FileSystemPath` stays where it is — its type is a bare
string, but `combine`/`name`/`directory`/`parent` are not.

**Paths are a type, not a string**. `FileSystemPath` is a
single-case union with a private constructor and a companion module; the
fs-dependent helpers (`combine`/`name`/`directory`/`parent`) take `IFileSystem`
so path handling stays consistent with `MockFileSystem`. It flows through the
listing, operations, and panel/navigation layers and the error path fields.
Raw OS strings are wrapped once at the outside edge — `Dependencies.create` for
the start path, `Shell` for drive roots and prompt answers.

**Every value a `Message` or `Effect` carries says what it is**.
`EntryName` for an entry's name, `NamePattern` for a wildcard, `FileSystemPath`
for a path, `SelectionMode` (`Select`/`Deselect`) and `DeleteScope`
(`NamedEntriesOnly`/`WithDirectoryContents`) instead of unnamed booleans, and
`PanelCursor` bundling the side with the row it points at. Two things stayed
primitive on purpose: the cursor `Index` inside `PanelCursor`, because an
ordinal position is what an `int` is for, and `TargetEntered`'s text, which is
read before `Update` decides whether it names an entry or a directory.
Constructors follow one rule — `create` is total, `tryCreate` validates and
answers an `option`. `EntryName` has both, because the file system hands over
names that are names by construction while a prompt hands over text that may
be blank; `NamePattern` has only `tryCreate`. Trimming and the blank check live
in `tryCreate`, so `Shell` binds it onto the prompt answer and a blank answer
becomes indistinguishable from a cancelled one before any message exists.

**A search is a domain type, and the panel it belongs to holds it.**
`SearchText` sits beside `NamePattern`: a single-case union over a string with
`ofChar`, `append`, `backspace` (which answers `None` on the last character, so
running out of text *is* closing the search) and `matches`, which is a
case-insensitive substring test. `PanelState.Search` makes it per panel, so the
footer of the panel being searched is the one that changes, and
`loadPanelFocusing` clears it along with the marks — a search cannot outlive the
listing it was run against. `activate` clears both panels' searches, which is
what closes one when `Tab` moves on.

**A program is named by a path, and an extension and a role are what pick it.**
`ProgramPath` and `FileExtension` are single-case unions with `tryCreate` only,
because both come from a text field: a program left blank is *no program*, not
an empty command line, and an extension is normalised as it is created —
trimmed, stripped of leading dots and lowercased — so `.TXT`, `txt` and `  .txt`
are one key. `FileExtension.ofName` reads one off an `EntryName`, which is what
makes the lookup `ProgramSettings.programFor role name` rather than a string
compare at the call site. `AssociationRole` is the third part of the key:
`ForEditing` is the program `F4` starts and `ForOpening` the one `Enter` will,
so one extension can name a different program for each without two tables to
keep in step. `ProgramSettings` is the pair of them: a default editor and an
ordered list of `ProgramAssociation`, kept sorted by role and then extension so
the file it is written to and the lists a popup shows are in the same order
every time.

**Starting a program is a request, not four arguments.** `LaunchRequest` carries
the target, the program to use or `None` for the system's own choice, the
working directory and an `Elevation` of `AsUser` or `AsAdministrator`. Making it
one value is what keeps the port's signature stable while phase 10 fills the
fields in from three different keys, and it makes an illegal call hard to
write: there is no argument order to get wrong and no boolean that has to be
read back to the word it stands for. `LaunchError` is its counterpart on the way
out — `NothingAssociated`, `ProgramNotFound`, `LaunchRefused` and
`ElevationDeclined` — four values rather than one string, because the caller
does different things with them: three reach a dialog and the fourth is
silence.

**A handler is a program with a name a person can read.** `Handler` is a display
name and the `ProgramPath` behind it, and nothing else: what the two systems
answer with — a registry ProgId, a `.desktop` file — is theirs to know and not
worth carrying inland. `Handler.tryCreate` takes the name the system gave and
the program it resolved to, and a blank name falls back to the program's file
name, so a row in the popup always says *something*. `Handler.distinct` keeps
the first name a program was given: the same executable is reached twice on both
platforms (a ProgId and an open-with entry, a default and an added association),
and the list a person reads should say it once.

**Only editing has a default.** `ProgramSettings.programFor` falls back to
`DefaultEditor` in `ForEditing` and to nothing in `ForOpening`, because the
system already is the default opener: a file no `ForOpening` row names is one
the system should be handed, not one Marbas should guess a program for. That
makes "there is no default opener" a value the lookup returns rather than a
rule every caller has to remember.

**A moment in time is a `DateTimeOffset`, never a `DateTime`.** Every timestamp
Marbas holds — `Entry.Modified`, the three on `EntryProperties`, the deletion
date a trashed entry is stamped with — says which offset it was taken in, so
nothing depends on the machine's idea of local time at the moment it is read.
`IFileSystemInfo` speaks `DateTime`, so the conversion happens on the edge and
nowhere else: `DateTimeOffset info.LastWriteTime` on the way in (a local-kind
`DateTime` keeps its instant) and `properties.Created.LocalDateTime` on the way
out.

**Properties are a value the file system is read into and written back from.**
`EntryProperties` carries the path, the three timestamps and a `Set<EntryFlag>`
— `ReadOnlyFlag`, `HiddenFlag`, `SystemFlag`, `ArchiveFlag` — instead of a raw
`FileAttributes`. That keeps the four bits Marbas offers apart from the ones it
must not touch: `EntryProperties.write` folds the set back onto the attributes
the entry already carries, so `Directory`, `ReparsePoint` and everything else
survive a save. The flags are written last, after the timestamps, because a
read-only bit set first would refuse them.

**Where a panel stands is a type with two cases.** `PanelLocation` is `OnDisk`
of a `FileSystemPath` or `InArchive` of the archive's path and an
`ArchivePath`, and it is what `PanelState.Path` holds. A place inside an
archive is not a path the operating system knows, so it cannot be a
`FileSystemPath` without lying to every function that takes one; making it a
case of its own turns "this panel has no directory to write into" from a
runtime surprise into an `Option` the compiler asks about. `ArchivePath` is
what the second case carries — `/`-separated, never rooted, never climbing —
so the two halves of the location are each already a type that cannot hold
nonsense. See [ARCHIVES.md](ARCHIVES.md), *Where a panel stands*.
