# Phase 23 — The layers above the file system

The tasks are in
[Phase 23 — The layers above the file system](../Plan/PHASE-23-THE-LAYERS-ABOVE-THE-FILE-SYSTEM.md).

**P23.1 the layers are a test.** `Layers/LayerReferences.fs` reads the
compiled `Marbas.dll` with Mono.Cecil, as `SystemCalls.fs` beside it does,
places every type in the layer its namespace names and collects every type of
`Marbas` it refers to; `Layers/LayerReferenceTests.fs` asserts that every type
is in a layer of the table, that every reference is to the type's own layer or
to one its row allows, and that every layer of the table holds a type. The
table is the one of today, `FileSystem` still allowed `Collections` for
`Settings`, and the scan found nothing that contradicts §1 of
[Coding style](../coding-style-conventions.md#1-layers): `Program` names
`Adapters` alone, `Messages` neither `Collections` nor anything above it, and
no layer one beside it. What the test watches is in
[Testing](../Architecture/TESTING.md#the-layers-a-test-holds).

The test was shown to fail twice, and both plants were taken out again. A
function `planted` in `ArchiveErrorMessage` that answered `Zipper.single title`
failed the second assertion with
``Marbas.Messages.ArchiveErrorMessage (Messages) uses Marbas.Collections.Zipper`1 (Collections)``
and the same line for the module `Zipper`. A file `Planted.fs` declaring
`Marbas.Stray.Planted`, compiled before `Program.fs`, failed the first with
``Marbas.Stray.Planted is in Stray, which is no layer of the table``.

§1 of Coding style says that the table is now a test and changes with it, and
its row of `Domain` no longer names `FileSystemPath`, which is declared in
`FileSystem`; that row now says so, and that it may use `Collections` for
`Settings`.

Four things went differently from the plan.

- **The static initialisers are scanned, not left out.** F# compiles the
  module-level values of a file into `<StartupCode$Marbas>.$…`, a type the
  compiler generates and whose name, for a file that opens with `namespace`,
  names no layer — `$HostEnvironment`, `$History`. Twenty-four of those that
  name none hold code, and a value at the top of a module of `Messages` that built a `Zipper`
  would be made there. Each is placed by the folder of the source file its
  first sequence point names, as `SystemCalls.fs` places a method, so the
  scan reads the symbols beside the assembly too.
- **A fourth test keeps the scan from going blind.**
  `the scan sees the layers use the ones below them` expects references
  between two layers its rows allow, as `SystemCallTests` expects calls in
  `Adapters`: a scan that found no reference at all would pass the second
  assertion.
- **The variables of a method's body are collected too**, beside what the
  plan lists; a variable of a type is almost always also named by an
  instruction, and the list costs nothing.
- **A layer is a case of a union**, `Layer`, and the table a map from it to
  the layers it may use. A namespace whose second segment is no case is the
  stray the first assertion names. A layer joins the table as a case and a
  row; the case alone, with no type moved into it, fails the third assertion.

**P23.2 `Find` leaves.** The table of the layer test was given its row first —
`Find`, allowed `FileSystem` and `Domain`, and allowed to `State`, `Ui` and
`Adapters` — and the test failed with `Find holds no type`. Then
`ContentMatch`, `FindGround`, `FileSearch` and `FindAnswer` moved to
`Marbas/Find/` as `Marbas.Find`, in a group of their own compiled after
`ByteSearch`, the last module of `FileSystem`, and their four test files to
`Marbas.Tests/Find/` as `Marbas.Tests.Find`, in a group after the tests of
`FileSystem`. The modules of `FileSystem` compiled without them before
anything else was touched, which is what the graph of the plan promised.

Each moved module opens `Marbas.FileSystem`, which its old namespace gave it
for nothing, and `FindAnswer` opens `Marbas.Find.FileSearch` for the old
`Marbas.FileSystem.FileSearch`. No body changed. The readers are the ones the
plan names — `AppState`, `Update`, `Ports`, `Shell`, the four views of
`Ui/Find`, `SearchRunner` and `Dependencies` — and in the tests the four of
`Ui/Find` and `Integration/Find`, seven of `State`, `ClipboardFixture` among them,
`ShellSearchTests` and `PanelPaintingTests`. A reader that names a module of
`Find` by its name, as `FileSearch.run` or `FindGround.resolve`, or a type of
`FindAnswer`, takes `open Marbas.Find`; one that only opened
`Marbas.FileSystem.FileSearch` now opens `Marbas.Find.FileSearch` instead.

The suite passed 4,299 of 4,334 on Windows, 35 skipped, and 4,276 of 4,337
under WSL, 61 skipped — the counts of P23.1 on both. Under WSL, in tmux,
over a zip holding `docs/notes.txt` and `docs/other.md`, *Find* with
*Search archives* found `notes.txt` under `box.zip/docs` by the mask `*.txt`,
and again by the text `needle` under the mask `*`, the second time with one
entry skipped: the zip itself, a binary file the text search passes over.

The type `FindGround` stays in `PanelLocation.fs` under `Marbas.FileSystem`,
and the module `FindGround` is now `Marbas.Find.FindGround`; it keeps its
`ModuleSuffix`, which no longer separates it from a type of its own namespace
but changes no name a reader writes.
