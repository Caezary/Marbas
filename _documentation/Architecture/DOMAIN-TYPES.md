# Domain types

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

**An editor is named by a path, and an extension is what picks it.**
`EditorPath` and `FileExtension` are single-case unions with `tryCreate` only,
because both come from a text field: an editor left blank is *no editor*, not
an empty command line, and an extension is normalised as it is created —
trimmed, stripped of leading dots and lowercased — so `.TXT`, `txt` and `  .txt`
are one key. `FileExtension.ofName` reads one off an `EntryName`, which is what
makes the lookup `EditorSettings.editorFor name` rather than a string compare
at the call site. `EditorSettings` is the pair of them: a default editor and an
ordered list of `EditorAssociation`, kept sorted by extension so the file it is
written to and the list a popup shows are in the same order every time.

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
