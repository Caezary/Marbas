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

**P23.3 `Viewing` leaves.** The row came first again — `Viewing`, allowed
`FileSystem` and `Domain`, given to `Find` and allowed to `State`, `Ui` and
`Adapters` — and the test failed with `Viewing holds no type`. Then
`TextEncoding`, `RewindingStream`, `FileWindow`, `LineIndex`, `WindowSearch`
and `ByteSearch` moved to `Marbas/Viewing/` as `Marbas.Viewing`, in a group
compiled between the last module of `FileSystem` and `Find`, and their six
test files to `Marbas.Tests/Viewing/`, in a group between the tests of
`FileSystem` and those of `Find`. `TextEncoding` was the sixth module of
`FileSystem` and nothing compiled after it there read it.

`FileWindow`, `WindowSearch` and `ByteSearch` open `Marbas.FileSystem` — a
path, an archive, a `ContentError` —, and the other three need nothing of it.
The readers are the ones the plan names: `ContentMatch` in `Find`, `Ports`
and `Shell`, seven views of `Ui/Viewer` — `CodePage`, `HexDump`, `HexMark`,
`HexText`, `ViewerSearch`, `ViewerState` and `FileViewer` —,
`ViewerSearchDialog`, and `Prompts` and `WindowSearchRunner`. Seven of them
had opened `Marbas.FileSystem` for these modules alone — `CodePage`,
`HexDump`, `HexMark`, `HexText`, `ViewerState`, `ViewerSearchDialog` and
`WindowSearchRunner` — and now open `Marbas.Viewing` instead; the others open
both. Every such `open` was found by taking it out and building, not by
reading; the same check on the readers of `Find` found every
`open Marbas.FileSystem` of P23.2 still needed.

Two tests that stay in `FileSystem`, `ArchiveChangeTests` and
`EveryKindTests`, read an entry of an archive back through
`FileWindow.openEntry`, and open `Marbas.Viewing` for it; the rule of the
layers is a rule of `Marbas.dll`, and a test may read through any layer. In
all, twenty-four test files took the new `open`, the six moved among them,
and eleven of them in place of `open Marbas.FileSystem`.

The suite passed 4,299 of 4,334 on Windows and 4,276 of 4,337 under WSL, the
counts of P23.1. Under WSL, in tmux, a 60-line `sample.txt` whose line 41
holds `needle` and `zażółć` in Windows-1250 was opened with `F3`: the viewer
settled on Latin-1, `F7` with `needle` brought line 41 to the top showing
`za¿ó³æ`, `F8` with *Windows-1250* read it again as `zażółć` on the same line,
and `F4` then `F7` with `BF F3 B3` found the run on row `0000000140`.

**P23.4 `Locations` leaves.** The row came first — `Locations`, allowed
`FileSystem` and `Domain`, given to `Find` and allowed to `State`, `Ui` and
`Adapters` — and the test failed with `Locations holds no type`. Then
`PanelLocation`, `Landing`, `SearchListing` and `DirectoryMeasure` moved to
`Marbas/Locations/` as `Marbas.Locations`, in a group compiled between the
last module of `FileSystem` and `Viewing`, and their four test files to
`Marbas.Tests/Locations/`, in a group before the tests of `Viewing`.

This time `FileSystem` did not compile without them, which the plan foresaw:
an edge made by union cases written without their type does not show in a
graph read from the text. `Place.ofLocation` and `Place.toLocation` in
`FileSystem/Place.fs` turn a `Place` into a `PanelLocation` and back through
`OnDisk`, `InArchive` and `SearchResults`. The type `Place` cannot follow
them out, since `History` and `Favorites` keep a `Place` and go to `Profile`,
below `Locations`. The user chose to move the two functions, unchanged, to a
`module Place` at the end of `Locations/PanelLocation.fs`: F# lets a module
extend another of the same name, so `Place.ofLocation` in `Update` and
`Place.toLocation` in `Landing` read as before and nothing is renamed. The
graph of the plan says so, and its list of what is left names the pair.

The readers took `open Marbas.Locations` right after `open Marbas.FileSystem`
and not after the last `open Marbas.FileSystem.*`. Opened after
`DirectoryListing`, the record `Measurement` of `DirectoryMeasure`, whose
`Name` is a label of `Entry` too, took over the records `PanelState`,
`AppState` and `CommandExpansion` build, which the compiler inferred as
`Entry` while the four modules were part of `Marbas.FileSystem`. Opened where
that namespace was, they shadow what they shadowed before.

Eighteen modules of `Marbas` read `Locations` — three of `Find`, seven of
`State`, six of `Ui`, `BatchRunner` and `SearchRunner` — and in six of them,
`FindAnswer`, `TabRow`, `ResultRow`, `ResultsWindow`, `QueryText` and
`PanelTitle`, it replaced `open Marbas.FileSystem`, which they had needed for
these modules alone. Forty-six test files took the `open` besides the four
moved, and every one of them still needs `Marbas.FileSystem`.

The suite passed 4,299 of 4,334 on Windows and 4,276 of 4,337 under WSL, the
counts of P23.1. Under WSL, in tmux: `F3` on a directory holding 5,000 bytes
wrote `4.9 KB` in place of `<DIR>`; `Enter` on `box.zip` sent the panel into
it, titled `…/fixture/box.zip`; `Alt+F1` with `/mnt/wslg/distro` sent it
onto that drive; and *Find* with *Search archives* and `F2` put the other
panel on `Find: *.txt in /.../fixture`, listing `box.zip/docs/notes.txt`.
`Alt+F1` with `/mnt/c` answered *Access denied to '/mnt/c'*; the build of
P23.3 answers the same, so it is the mount and not the move.

**P23.5 `Profile` leaves.** The table took the row of `Profile` — allowed
`FileSystem`, `Domain` and `Collections`, given to `Messages` and allowed to
`State`, `Ui` and `Adapters` — and the row of `FileSystem` lost `Collections`.
The test failed twice, as it should: `Profile holds no type`, and
``Marbas.FileSystem.Settings (FileSystem) uses Marbas.Collections.Zipper`1
(Collections)`` with the same line for `SettingsModule`. Then `SettingsError`,
`Settings`, `HistoryError`, `History`, `FavoritesError`, `Favorites`,
`UserMenuError` and `UserMenu` moved to `Marbas/Profile/` as `Marbas.Profile`,
in a group compiled between the last module of `FileSystem` and `Locations`,
and their four test files to `Marbas.Tests/Profile/`.
`State/SettingsTests.fs` keeps its name in `Marbas.Tests.State`.

`FileSystem` compiled without them before anything else was touched, and the
eight modules read and write their files through `IFileSystem` as they did.
Each opens `Marbas.FileSystem`; the three errors that opened nothing take it
under `open System`. `Messages` reads `Profile` in five modules — the four
`*ErrorMessage` of the profile and `Problem` — as its new row allows, and no
layer between `Profile` and `Messages` reads it. Twenty-one modules of
`Marbas` read `Profile`, the `open` going right after
`open Marbas.FileSystem` as in P23.4; in five of them — `Startup`,
`MenuRemoval`, `UserMenuDialog`, `UserMenuRow` and `UserMenuState` — it
replaced that `open`. Forty-seven test files took it besides the four moved,
and all of them still need `Marbas.FileSystem`.

The suite passed 4,299 of 4,334 on Windows and 4,276 of 4,337 under WSL, the
counts of P23.1. Under WSL, in tmux, the build of P23.4 wrote a configuration
of its own: a directory entered, which the folder history kept; that directory
kept in slot `0` of *Favourites* with `Ins`; a second tab opened and sent back
to the start; an entry `E  Echo` running `echo hi` added to the user menu; and
`F10`. The new build, started over those four files, opened on tab `[2/2]`,
listed both tabs under `F11` with the places they had, showed both folders in
the history of `Alt+F12`, the directory in slot `0` of `Alt+B`
and `E  Echo` under `F2`; after `F10` the four files were byte for byte the
ones it had started over.

**P23.6 `Programs` leaves.** The table took the row of `Programs` — allowed
`FileSystem` and `Domain`, given to `Profile` and `Messages` and allowed to
`State`, `Ui` and `Adapters` — and the test failed with
`Programs holds no type`. Then `LaunchRequest`, `ProgramImage`, `CommandRun`,
`ExpansionRefusal`, `Handler`, `ProgramCommand`, `EditorCommand`,
`DesktopEntry`, `MimeDatabase` and `DesktopHandlers` moved to
`Marbas/Programs/` as `Marbas.Programs`, in a group compiled right after the
last module of `FileSystem` and before `Profile`, and their eight test files
to `Marbas.Tests/Programs/`. `ProgramImageBytes.fs` stays at the root of the
tests.

`FileSystem` compiled without them before anything else was touched. Four of
the ten open `Marbas.FileSystem` — `LaunchRequest`, `ProgramImage`,
`CommandRun` and `ExpansionRefusal` — and the other six need nothing of it.
`Profile` reads `Programs` in `History` and `UserMenu`, `Messages` in
`ExpansionRefusalMessage`, `LaunchErrorMessage` and `Problem`, as their rows
now allow. Twenty-one modules of `Marbas` read `Programs`, the `open` going
right after `open Marbas.FileSystem`; in five — `RegistryHandlers`,
`HandlerRow`, `MenuEntryDialog`, `MenuEntryForm` and `OpenWithDialog` — it
replaced that `open`. Thirty-three test files took it besides the eight moved,
and in fifteen test files in all, seven of the moved among them,
`open Marbas.FileSystem` was needed for these modules alone and is gone.

The suite passed 4,299 of 4,334 on Windows and 4,276 of 4,337 under WSL, the
counts of P23.1. Under WSL, in tmux, with scripts that write a line to a log
standing in for the programs — `xdg-open` first on the `PATH`, `$EDITOR`, an
executable `run.sh`, a handler and a user menu entry `M` running the handler
with `%n` — `Enter` on `doc.txt` ran `xdg-open` on it, `Enter` on `run.sh`
ran it, `F4` on `doc.txt` ran the editor on it, `Alt+R` on `doc.txt` with the
handler typed under *Program* ran the handler on it, and `F2` then `M` ran
`handler menu doc.txt`. A `.desktop` entry `Marker` for `text/plain` under
`XDG_DATA_HOME`, named in `mimeapps.list`, was listed under *Known programs*
and `Enter` on it ran its program on the file — without the `desktop`
argument its `Exec` line carries, which the build of P23.5 drops the same
way.

The user walked the Windows leg by hand: a program, a document, a program of
*Open with*, the editor and a user menu entry all started. The entry,
`code %p` run directly, started VS Code, which wrote its own log over the
panels. `code` is `bin\code.cmd` on the `PATH`, a batch file rather than a
window program's image, so the drained start of P21.6 does not take it and
the shell verb does. `CommandProcess.onWindows`, which decides it, changed
only its `open` lines in the move, so it is not the move.

**A batch script named as a program is drained, at the user's word, before
P23.7.** `ProgramResolution.windowProgram` became `drainedProgram`, which
answers a `DrainedProgram` of `LaunchProcess` — a `WindowImage` for a window
program's own image, as before, and a `BatchScript` for a resolved `.cmd` or
`.bat`. `LaunchProcess.detachedFrom` starts the first from its file and the
second as `cmd.exe /d /s /c ""<script>" "<word>" …"` with no window, its
output and error drained and its input closed; `CommandProcess`,
`EditorProcess` and `LaunchProcess` take it in place of the window program
alone, and `Dependencies` binds it once as before. Every word handed to
`cmd` is quoted and a trailing backslash doubled, since .NET would quote only
a word with a space and `C:\R&D\notes.txt` would end the line at its `&`.
`Enter` on a `.cmd` or a `.bat` itself keeps the verb: a script opened for
itself is meant to be seen, and only a script named as the program of
something else — a user menu entry run directly, a program chosen for the
extension, `F4` — runs hidden. The test fake `WindowPrograms` became
`DrainedPrograms`, with `windowImage` and `batchScript`.

Nine tests were written first and failed: three cases of
`a resolved batch script is drained as a script`, in place of the test that
held a script not to be a window program; a batch script chosen for a file,
run directly from the user menu and started as the editor, each through
`cmd` without a window; a script opened itself keeping the verb; a hidden
script's input redirected; and the quoting of `C:\R&D\notes.txt` and `C:\`.
The suite passed 4,307 of 4,342 on Windows and 4,284 of 4,345 under WSL.
Whether VS Code now stays off the panels in the user's terminal is for the
user to see on Windows; [Rough edges](../ROUGH-EDGES.md) records the `%`
that `cmd` still expands, and the arguments of an `Exec` line that a
`.desktop` handler loses.
