# Phase 23 — The layers above the file system

Goal: `FileSystem` holds what the application knows about entries and nothing
else — paths, listing, operations, the session's own directories and archives —
and the five subjects that only use the disk become layers of their own above
it: the programs that run a file, what the user keeps between sessions, where a
panel stands, a file's content as the viewer reads it, and the search. Which
layer may use which is held by a test, so a new feature cannot land in
`FileSystem` because that is where everything else is; it has to answer which
layer's subject it extends, and a layer that fits none is a new row in the
table, not a file in the nearest folder. The phase adds no feature and changes
no behaviour.

`FileSystem` has grown to sixty-one modules. Twenty-nine of them never touch
`IFileSystem`, and `Settings` touches it on eleven lines without being about
the file system: it reads and writes its own file. What decides whether a
module belongs there is its subject — an entry, a path, an archive — not
whether it reaches the disk. The modules that leave already use `FileSystem`
rather than belong to it. `Profile` and `Programs` read and write files of
their own — the configuration, the `.desktop` files, a program's header — and
no others. `Locations`, `Viewing` and `Find` between them call `fs.*` directly
five times, each to open a file or ask whether one exists, and walk
directories and archives only through `DirectoryListing`, `ArchiveListing`,
`ArchiveIndex` and `ArchiveReader`; `FileSearch` calls `fs.*` not once.

## The graph

The dependencies were read on 2026-10-18 by matching the public types and
modules of each file against the text of every other, keeping only what points
back in the order of compilation, and reading every match that looked odd; five
were names that happened to be equal — a local `Remembered`, a field `Started`,
a field `Content`, the type `Place` of `WindowSearch`, the attribute `Archive`
in `EntryProperties` — and were dropped. A reference made only by a union case
written without its type, or by a record the compiler infers, would not show,
so the compiler is the last word: a move that does not build because of an edge
missing here stops, and this graph is corrected before it goes on.

Nothing that stays in `FileSystem` uses anything that leaves it, which is what
lets each of the five go. `Session` cannot: `Archive` packs and changes an
archive in a scratch directory of `Workspace`.

```
        State, Ui, Adapters: every layer below them
                         │
                     Messages ───────────────────┐
                                                 ▼
          Find                                Profile
         /    \                                  │
  Locations    Viewing                        Programs
         \    /                                  │
        FileSystem ◄─────────────────────────────┘
             │
           Domain
             │
        Collections
```

The picture draws the nearest edge of each layer. The table is the contract —
the one the test of P23.1 holds and §1 of
[Coding style](../coding-style-conventions.md) prints:

| Layer | Holds | May use |
|-------|-------|---------|
| `Collections` | `Zipper` and the like | nothing |
| `Domain` | the vocabulary | `Collections` |
| `FileSystem` | paths, listing, operations, the session's directories, archives | `Domain` |
| `Programs` | what runs a file, and how it is started | `FileSystem`, `Domain` |
| `Profile` | what the user keeps between sessions | `Programs`, `FileSystem`, `Domain`, `Collections` |
| `Locations` | where a panel stands, where it lands, what it lists | `FileSystem`, `Domain` |
| `Viewing` | a file's content as a window of bytes and lines | `FileSystem`, `Domain` |
| `Find` | searching by name and by text | `Locations`, `Viewing`, `FileSystem`, `Domain` |
| `Messages` | the words the application says | `Profile`, `Programs`, `FileSystem`, `Domain` |
| `State` | the state, its transitions, the ports, the interpreter | every layer above it here |
| `Ui` | views and their wiring | `State` and every layer it may use |
| `Adapters` | the real ports | every layer above it here |
| `Program` | starts the application | `Adapters` and what it is handed |

The order of compilation, from the bottom:

```
Collections → Domain → FileSystem → Programs → Profile → Locations → Viewing
            → Find → Messages → State → Ui → Adapters → Program
```

Where every module goes:

| Layer | Modules | Their tests |
|-------|---------|-------------|
| `Programs` | LaunchRequest, ProgramImage, CommandRun, ExpansionRefusal, Handler, ProgramCommand, EditorCommand, DesktopEntry, MimeDatabase, DesktopHandlers | Handler, ProgramCommand, EditorCommand, DesktopEntry, MimeDatabase, XdgDirectories, DesktopHandlers, ProgramImage |
| `Profile` | SettingsError, Settings, HistoryError, History, FavoritesError, Favorites, UserMenuError, UserMenu | Settings, History, Favorites, UserMenu |
| `Locations` | PanelLocation, Landing, SearchListing, DirectoryMeasure | PanelLocation, Landing, SearchListing, DirectoryMeasure |
| `Viewing` | TextEncoding, RewindingStream, FileWindow, LineIndex, WindowSearch, ByteSearch | the six of the same names |
| `Find` | ContentMatch, FindGround, FileSearch, FindAnswer | ContentMatch, FindGround, FileSearch, FileSearchArchive |
| `FileSystem` | FileSystemPath, ArchivePath, Place, PastedPath, ClipboardFiles; EntryAttributes, ArchiveError, ContentError, DirectoryListing, Drives; Operations, Links, Shortcut, Trash, EntryProperties; SessionOwner, Workspace, LogFileError, LogFile | FileSystemPath, ClipboardFiles, EntryAttributes, ContentError, Drives, DirectoryListing, DirectoryOrdering, EntryProperties, ArchivePath, LogFile, OperationsFixture and the seven `Operations*`, Wipe, Links, Shortcut, PastedPath, Trash, SessionOwner, Workspace |
| `FileSystem/Archives` | ArchiveKind, ArchiveFormat, ArchiveNames, ZipMoments, ZipEntries, ArchiveReader, ArchiveIndex, ArchiveListing, ArchiveName, Archive | ArchiveKind, ArchiveName, Archive, ArchiveNames, ArchiveReader, ArchiveIndex, ForeignArchive, ArchiveExtract, ArchiveDirectoryTime, ZipMoments, ZipEntries, BackslashName, ArchiveListing, ArchiveChange, ArchiveAcross, EveryKind |

Each `Tests` name above is a file `<name>Tests.fs`; every one of the sixty-six
files of `Marbas.Tests/FileSystem/` is in exactly one row.

Three placements are not obvious. `DirectoryMeasure` is an operation, but its
`MeasureTarget` carries a `PanelLocation`, so it goes where that type goes.
`ArchiveError` and `ArchivePath` stay in the root of `FileSystem` and not in
`Archives`: `ListingError` carries the one and `Operations`, `Place` and
`PanelLocation` the other. `Shortcut` stays beside `Links`, since both say
where an entry points; it reads `ContentError`, which stays below everything
that reads a file's content.

Three decisions run through the moves.

**Every move is one layer and moves from the top.** A layer leaves only when
nothing left in `FileSystem` uses it, and taking the topmost first keeps that
true at every step: `Find` first, then `Viewing` and `Locations`, which `Find`
then reads from below, then `Profile`, then `Programs`, which `Profile` reads.
After each task the application builds and starts.

**A move changes namespaces and nothing else.** A module keeps its name and
its file name; `module Marbas.FileSystem.FileSearch` becomes
`module Marbas.Find.FileSearch`, a type declared under
`namespace Marbas.FileSystem` is declared under the namespace of its layer, and
every `open` in the layers above and in the tests follows. No function, type or
case is renamed, no body changes, and no test changes what it asserts. A move
changes no method body, so nothing is timed — but searching and the viewer are
hot paths of §10 of [Coding style](../coding-style-conventions.md), and a move
there that turns out to need more than a namespace stops and is timed before
and after.

**The tests move with their modules.** The layout of
[Testing](../Architecture/TESTING.md#layout) already holds: a test folder
mirrors the source folder, one test module per module under test, the
namespace following. So `Marbas.Tests/FileSystem/FileSearchTests.fs` becomes
`Marbas.Tests/Find/FileSearchTests.fs` as `Marbas.Tests.Find.FileSearchTests`,
and the test project compiles the folders in the order the source does. The
fixtures at the root of `Marbas.Tests` — `EmbeddedFixture`, `Fakes`,
`ProgramImageBytes`, `ShortcutBytes` — stay where they are; no moved test reads
a fixture of another folder, and `OperationsFixture` serves only the
`Operations*` tests, which stay. The tests under `Integration/` stay where
they are too: their subject is a view or the disk, not the module, so only
their `open` lines follow. The suite passes the same number of tests on each
system after every task as before it.

## Stage 1 — The rule as a test

- **P23.1 The layers are a test** — §1 of Coding style says which layer may use
  which, and the order of compilation keeps only half of it: it stops a layer
  from naming one above it, never one beside it, so `Viewing` could start
  using `Locations` and nothing would notice. A test in `Layers/`, beside
  `SystemCallTests`, reads the compiled `Marbas.dll` with the Mono.Cecil
  reading `SystemCalls.fs` already does, and takes the layer of every type as
  the second segment of the namespace of its outermost declaring type —
  `Marbas.State.Shell` is `State`, `Marbas.Ui.Panels.FilePanel` is `Ui`,
  `Marbas.Program` is `Program` —, leaving out what the compiler and Coverlet
  generate. It collects what each type refers to — its base type and
  interfaces, its fields, the parameters and results of its methods, their
  generic arguments, and every type, method and field an instruction names —
  and asserts three things: that every type is in a layer of the table, so a
  stray namespace fails by name; that every reference is to the type's own
  layer or to one its row allows, failing with both types and both layers
  named; and that every layer of the table holds a type. The table is the one
  of today — `FileSystem` may still use `Collections`, for `Settings`, until
  P23.5 — and is settled by what the scan finds: a reference that contradicts
  §1 stops the task and is reported, not written into the table. The test is
  shown to fail once by a reference planted in `Messages` to `Zipper` and once
  by a type planted under `Marbas.Stray`, both taken out again. The scan reads
  one assembly once and stays out of every hot path. *Done when:* the test
  passes, both planted failures failed it with what was planted named in the
  message, §1 of Coding style says that the table is now a test and that
  `FileSystemPath` is declared in `FileSystem` — the row of `Domain` still
  names it — and [Testing](../Architecture/TESTING.md#the-layers-a-test-holds)
  says what the test watches.

## Stage 2 — Out of `FileSystem`, from the top

Each task begins by adding its layer's row to the table of P23.1, which then
fails because the layer holds no type; the move makes it pass. Each ends with
the layer's section in the code map of [Overview](../Architecture/OVERVIEW.md)
and its row in §1 of Coding style.

- **P23.2 `Find` leaves** — ContentMatch, FindGround, FileSearch and FindAnswer
  move to `Marbas/Find/` as `Marbas.Find`, compiled after the last module of
  `FileSystem`; their four test files move to `Marbas.Tests/Find/`. `Find` may
  use `FileSystem` and `Domain` until P23.3 and P23.4 give it the two layers
  it reads from. Its readers are `AppState`, `Update`, `Ports` and `Shell`,
  the four views of `Ui/Find`, and `SearchRunner` and `Dependencies` in
  `Adapters`. The type `FindGround` stays in `PanelLocation.fs`
  and the module `FindGround` goes to `Find`, so for the first time they live
  in two namespaces; neither is renamed here. *Done when:* `Marbas.Find` holds
  the four modules, the layer test passes with its row, the suite passes with
  the same count on both systems, and a search by name and by text inside an
  archive is run once in the application.
- **P23.3 `Viewing` leaves** — TextEncoding, RewindingStream, FileWindow,
  LineIndex, WindowSearch and ByteSearch move to `Marbas/Viewing/` as
  `Marbas.Viewing`, compiled before `Find`, whose `ContentMatch` reads
  `TextEncoding`; their six test files move to `Marbas.Tests/Viewing/`.
  `Viewing` is not named `Content`, which `TextEncoding` declares as a type.
  Its readers are `Ports` and `Shell`, seven views of `Ui/Viewer` and
  `Ui/Dialogs/ViewerSearchDialog`, and `Prompts` and `WindowSearchRunner` in
  `Adapters`.
  *Done when:* `Marbas.Viewing` holds the six modules, `Find`'s row allows
  `Viewing`, the layer test and the suite pass, and a file is opened in the
  viewer, searched as text and as bytes, and read in another code page.
- **P23.4 `Locations` leaves** — PanelLocation, Landing, SearchListing and
  DirectoryMeasure move to `Marbas/Locations/` as `Marbas.Locations`, compiled
  before `Viewing`; their four test files move to `Marbas.Tests/Locations/`.
  `PanelLocation.fs` takes `FindHit`, `FindGround` and `FoundEntries` with it:
  they are declared with `and` beside `PanelLocation`, whose `SearchResults`
  case carries them, and cannot be parted from it. *Done when:*
  `Marbas.Locations` holds the four modules, `Find`'s row allows `Locations`,
  the layer test and the suite pass, and a panel is sent into an archive, onto
  a drive and onto search results, and a directory is measured.
- **P23.5 `Profile` leaves** — Settings, History, Favorites and UserMenu move
  with their four `*Error` modules to `Marbas/Profile/` as `Marbas.Profile`,
  compiled before `Locations`; their four test files move to
  `Marbas.Tests/Profile/`, beside which `State/SettingsTests.fs` keeps its name
  in its own namespace, as it does today. `Settings` takes the use of
  `Collections` with it, and the row of `FileSystem` drops `Collections`. The
  four modules read and write their own files through `IFileSystem` and keep
  doing so: the port is not a privilege of `FileSystem`, every layer below
  `Adapters` takes it, and the tests stay on `MockFileSystem`. `Messages`
  words their errors and gets `Profile` in its row. *Done when:*
  `Marbas.Profile` holds the eight modules, the layer test passes with
  `FileSystem` no longer allowed `Collections`, the suite passes, and the
  application started over an existing configuration restores its tabs, its
  history, its favourites and its user menu.
- **P23.6 `Programs` leaves** — LaunchRequest, ProgramImage, CommandRun,
  ExpansionRefusal, Handler, ProgramCommand, EditorCommand, DesktopEntry,
  MimeDatabase and DesktopHandlers move to `Marbas/Programs/` as
  `Marbas.Programs`, compiled right after `FileSystem` and before `Profile`,
  whose `History` remembers a `LaunchRequest` and a `CommandRun` and whose
  `UserMenu` an `Elevation`; their eight test files move to
  `Marbas.Tests/Programs/`. Three modules have no test file of their own and
  are tested through their readers, as today. `ProgramImageBytes.fs` stays at
  the root of the tests, since `Adapters/ProgramResolutionTests` reads it too.
  *Done when:* `Marbas.Programs` holds the ten modules, `Profile`'s and
  `Messages`' rows allow `Programs`, the layer test and the suite pass, and a
  program, a document, an *Open with* handler, the editor and a user menu
  entry are started once in the application — on Windows, and on Linux under
  WSL.

## Stage 3 — What `FileSystem` keeps

- **P23.7 The archives get a folder** — what is left of `FileSystem` is
  twenty-nine modules, ten of them about archives. Those ten move to
  `Marbas/FileSystem/Archives/` as `Marbas.FileSystem.Archives`, compiled after
  every module of the root of `FileSystem`, none of which uses them; the
  sixteen test files of the archives move to
  `Marbas.Tests/FileSystem/Archives/`. The root is compiled in the order of
  the four subjects it keeps — the paths, the entries, the operations, the
  session — so that its `.fsproj` reads as the table above, and `ArchiveError`
  and `ArchivePath` stay in it. The rest of the root keeps no folder: nineteen
  modules in four groups read well enough flat, and a group gets a folder when
  it outgrows the others. *Done when:* `Marbas.FileSystem.Archives` holds the
  ten modules, the layer test still counts it as `FileSystem`, the suite
  passes, and an archive of each kind Marbas writes is packed, changed and
  unpacked once in the application.
- **P23.8 Where a new module goes** — the rule is written where it is read
  first. [Overview](../Architecture/OVERVIEW.md) gets a paragraph on the five
  layers — why they are not `FileSystem`, what each holds, and that
  `IFileSystem` is taken by every layer below `Adapters` — and its first
  sentence and that of [State and effects](../Architecture/STATE-AND-EFFECTS.md)
  name the whole chain. §1 of Coding style says where a new module goes: into
  the layer whose subject it extends; into `FileSystem` only when it is about
  entries, paths, operations or archives and more than one layer needs it; and
  into a new layer, with its row in the table and in the test, when it fits
  none — never into the nearest folder. `.claude/CLAUDE.md` names the chain in
  its rule on dependencies. Every other document that names a moved module by
  its path — [Archives](../Architecture/ARCHIVES.md),
  [Finding files](../Architecture/FINDING-FILES.md) and the rest found by a
  search for `FileSystem/` — follows. *Done when:* no document names a module
  under a folder it has left, the code map lists every module of `Marbas`
  in the order it is compiled — the modules it misses today, `Favorites`,
  `ArchiveIndex` and `Shortcut` among them, included — and §1 of Coding style
  and the layer test hold the same table.

## Left for a later phase

- **`Operations.fs` is two modules** — the batch's own vocabulary
  (`OperationError`, `BatchControl`, `ItemResult`, `attempting`, `guarded`,
  `failures`) and copying, moving, deleting and wiping share a file.
  `ContentMatch`, `FileSearch` and `DirectoryMeasure` take only the
  vocabulary; `Archive`, `Workspace` and `Trash` take a function of the second
  half as well. And `OperationError` holds eighteen cases, gathered from
  features beside the operations: `NoProgramAssociated` and `StartDeclined`
  from the programs, `NotInSearchResults` from the search results,
  `ArchiveEntryNot*` from the archives, `LinkRefused` from the links. Parting
  the file renames every `Operations.` that names the vocabulary, across every
  layer and the tests, and giving those cases back to their layers changes what
  `Problem` carries; both wait for a phase that changes the operations anyway.
- **`ClipboardFiles` belongs to `State`** — no layer below `State` names it,
  and §1 puts a type in the lowest layer that speaks it. It stays in
  `FileSystem` for now: its test file would meet `State/ClipboardFilesTests.fs`,
  which tests the transitions of the clipboard, and the name of one of them has
  to be chosen when the clipboard is next touched.
- **`DirectoryMeasure` could return to `FileSystem`** — if `MeasureTarget`
  carried a `Place` instead of a `PanelLocation`, it would need nothing of
  `Locations`. Whether a measure of search results can be said in a `Place` is
  a question of the measure, not of the layers.
- **`Archive.fs` runs to 827 lines** — writing, changing and taking an archive
  apart in one module. It is parted when it next changes, along those three.
- **Two names of one word** — the type `FindGround` of `Locations` and the
  module `FindGround` of `Find`, and the record `Place` of `WindowSearch` beside
  the union `Place` of `FileSystem`. Each pair compiles and reads apart by its
  namespace; a rename is a choice of words for a phase that touches them.
