# Domain types

**Paths are a type, not a string**. `FileSystemPath` is a
single-case union with a private constructor and a companion module; the
fs-dependent helpers (`combine`/`name`/`directory`/`parent`) take `IFileSystem`
so path handling stays consistent with `MockFileSystem`. It flows through the
listing, operations, and panel/navigation layers and the error path fields.
Raw OS strings are wrapped once at the outside edge — `Program.fs` for the
start path, `Shell` for drive roots and prompt answers.

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
