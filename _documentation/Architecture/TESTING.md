# Testing notes

Tests live in `Marbas.Tests` (xUnit). Logic modules (`Domain`, `FileSystem`,
`State`) are covered directly; UI behaviour is covered by driving real
Terminal.Gui views with an in-memory file system (`MockFileSystem`). The disk is
reached by one folder only, `Integration/Disk/` — and by the Windows clipboard
tests in `Integration/Clipboard/`, which paste into a scratch directory — and
the rule for what may go there is below.

## Running the suite

```
dotnet test Marbas.slnx
```

That is the whole suite — a few seconds, no terminal, no network, nothing to set
up beyond the SDK, and no disk but for `Integration/Disk/` and
`Integration/Clipboard/`. Nothing is marked flaky, so a red run is a real
failure. A handful skip on Linux: the ones whose subject is drive letters, and
the ones whose subject is the Windows clipboard.

**A test on the real disk is the last resort.** It is slower, it leaves a scratch
directory behind when it crashes, and what it can prove depends on the machine
and the rights of the user running it. So a behaviour is tested on the mock
first, and on the mock taught to behave — a subclass in `Fakes` — second; the
disk is reached for only when the subject *is* what the real system does and no
mock can say it. Such tests are integration tests and live in
`Integration/Disk/`.

**A fake replaces one member of the mock, once, through `once`.** Every
subclass in `Fakes` swaps a port — `File`, `Directory`, `FileInfo`,
`DirectoryInfo` — for a subclass of its own, built on first use because the
replacement needs `this`. `MockFileSystem`'s own constructor reads `Directory`
before the subclass's fields exist, so the field that remembers the replacement
is still `null` at that moment: `Fakes.once` answers a fresh replacement then,
remembers one once the object is built, and every override is one line —
`override this.File = once writing (fun () -> CountingFile(…) :> IFile)`. It
replaced seventeen hand-written copies of the same `if isNull … then … <- …` in
P15.21; a `Replaced` held as an ordinary object was tried first and failed with
a `NullReferenceException` inside the base constructor, which is why the null
case is part of the helper and not of each fake.

Symbolic links were the first case that needed it. Enumerating a directory link
on a real system walks into what it points at, and on the mock it answers
nothing, so a walk that would spin for ever on a disk passes on the mock without
a stop at the link. The logic tests of the rule stay on a mock:
`Fakes.LinkFollowing` enumerates through a link the way a real system does and
throws once a path has grown deeper than any walk in a test could reach, so a
walk that goes round a link fails instead of hanging. What only the disk can
answer — that .NET itself walks into a link, and that `Directory.Delete` with
`recursive` does not — is `Integration/Disk/LinkWalkTests.fs`, together with one
walk of each kind over a real cycle. `[<SymbolicLinkFact>]` tries to create a
link once and skips those tests where the system refuses — Windows without
developer mode or administrator rights; Linux and the CI runners allow it.

The hard link and the junction were the second case: they are made by calling
into `kernel32` and `libc`, which no mock reaches, so
`Integration/Disk/NativeLinkTests.fs` makes each kind once through
`Links.create` over the real adapter. Every refusal that can be answered without
the system is tested on the mock instead, with the native calls replaced by a
recorder that proves they were not reached. The Unix volume check is the one
refusal the mock cannot hold: `MockFileSystem` names every drive `/:\` on Linux,
so a second mount cannot be arranged.

The executable bit was the third. `MockFileSystem` hands back whatever mode a
test sets, through the `FileInfo` it builds as well as through
`File.GetUnixFileMode`; the real wrapper answers `None` through the first, and
since phase 10 every test of a program on Unix was true of the mock alone.
`Integration/Disk/ExecutableBitTests.fs` gives a real file the bit and lists
it, follows a link to it, lists a link that points at nothing, and has
`LaunchProcess.describe` say what it would start. They are `[<UnixFact>]`
with a reason of their own, so they run on the Linux leg and are skipped on
Windows, where a file has no such bit. What decides by platform is tested on
the mock on both: `DirectoryListing.readOn` takes the platform, and
`Fakes.ModeWitness` counts which paths were asked for their mode — every file
once on Unix, none on Windows, none by a walk — and can refuse to answer for
one, which is how a file gone since it was enumerated is arranged.

A directory's time through an archive was the fourth. On a disk the first
file unpacked into a directory moves its time, which is why the directories
are dated last, and the mock does not hold that for every case.
`Integration/Disk/DirectoryTimeTests.fs` packs a dated tree into each kind
Marbas writes, unpacks it and reads the times off the disk, on both legs.
With the code changed to date a directory as it is made, all six failed
there, where five of the twenty-seven tests on the mock did.

Every test there works inside a `Scratch`, a fresh directory under the system
temp folder that is removed with `Operations.delete` when the test ends — not
with `Directory.Delete`, which stumbles on a junction inside the tree on
Windows, as one of the tests there records.

**The Windows clipboard is proved against another program.** The adapter in
`Adapters/SystemClipboard.fs` speaks `user32`, `kernel32` and `shell32`
directly and builds every block from bytes, so a test of it that only read back
what it wrote would pass with both halves wrong in the same way.
`Integration/Clipboard/WindowsClipboardTests.fs` therefore checks each half
against Windows PowerShell 5.1 started per call by `OtherProgram`, which reads
and writes through Windows Forms — the text, the `CF_HDROP` list and
`Preferred DropEffect` — the way an ordinary program does, and asks the system
shell to paste through `Shell.Application`'s `paste` verb, the handler
Explorer's own Paste runs. It also holds the clipboard open from that process
to prove a refusal arrives as `ClipboardFailed` with the system's reason. Who
a held clipboard keeps out is itself under test there, between two such
programs — with a window and with none on either side — and so is the
adapter's own hold: `SystemClipboard.whileOpen` is public for that, and a test
runs the other program's `OpenClipboard` inside it. The tests are
`[<WindowsFact>]` and run on the Windows CI runner too. Each starts with a
`KeptClipboard`, which reads the text on the clipboard and puts it back when
the test ends — the text only: files or a picture someone had copied before a
local run are gone after it. They share one module on purpose: xUnit runs the
tests of a module one after another, and there is one clipboard. A full run of
the folder takes about eleven seconds, most of it starting PowerShell.

**One suite, two platforms.** Every path in an arrange block is built with
`Root.at`, which answers `C:\start\docs` on Windows and `/start/docs` on Linux,
so `MockFileSystem` is handed paths its own platform understands. `Root.relative`
does the same without a root, for the tests that care about a name carrying a
directory — on Linux a backslash is an ordinary character in a file name, so
`docs\notes.txt` there is one file and not a path. `[<WindowsFact>]` sets `Skip`
off Windows and carries the reason: bare, it says drive letters, which is what
`MockFileSystem` emulates Windows-style while answering a duplicated `/:\`
elsewhere; given a reason of its own, it marks the archive-name tests, whose
subject is the OEM code page a culture answers with — a number Windows keeps and
other platforms do not. Nothing else in the suite knows which platform it is on.

**A name only one platform allows is tested where it exists, and the rule
about it everywhere.** `MockFileSystem` follows the system it runs on, so a
file called `a\b.txt` can be arranged on the Linux leg alone:
`BackslashNameTests` packs, adds, makes and unpacks such names there, a theory
over the kinds that keep them, each test a `[<UnixFact>]` or a
`[<UnixTheory>]` carrying the reason. What differs by platform in the code
under them is `ZipEntries`, which takes the platform as a value, so
`ZipEntriesTests` asks it for the entry either platform makes and runs on
both legs — the Windows leg holds what a zip packed on Linux says of itself,
and the Linux leg that a directory typed on Windows has its backslash read as
a separator.

**An archive another tool wrote is checked in, not rebuilt.** `Fixtures/`
holds what nothing in the dependencies can write the way that tool does — a
tar.xz, and since P22.4 a zip 7-Zip made and one `zip` made, each a tree of
two files dated `2001-02-03 03:05:06` UTC, 7-Zip's with half a second more in
an NTFS field that stands in its central directory alone. They are embedded
resources, and `EmbeddedFixture.bytes` hands one to the mock by name. The rest
of `ZipMomentsTests` builds its zips with SharpZipLib, whose own types write
either field and read back the nine bytes Marbas writes by hand, so those
bytes are checked by a reader that is not Marbas's. Every such zip is arranged
so that its local time and its exact moment disagree by an hour, which makes
the test say the same on a machine in any time zone. One test cannot be
arranged so — the local time of an entry after an update, which the defect
moved by the machine's own offset — and it passes for nothing on a machine set
to UTC, as the CI runners are.

Coverage comes from the collector the test project already references:

```
dotnet test Marbas.slnx --settings coverlet.runsettings
```

`coverlet.runsettings` asks for Cobertura and excludes `Program.fs` and
`Adapters/`, the composition root — they are the files that wire the real file
system, the real dialogs and the real process, so almost nothing in them is
reachable from a test (the one exception is under *Layout* below, and it is
still not counted). Everything else is measured, and coverage stays close to
complete.

**Coverage is a reading, not a target.** P8.1 measured the suite rather than
adding to it blindly: the collector named the lines no test reached, and each
one was either given a test that says something (a read-only file refuses to be
deleted, a file in use cannot be viewed, an unreadable config file falls back to
the defaults, every sort key survives being written and read back, a transfer
whose other panel vanished answers with an error) or written down below as
unreachable with a reason. `Program.fs` and `Adapters/` are excluded in
`coverlet.runsettings` instead of being counted and apologised for: they are
the composition root, and a test that could reach them would be starting the
real application.

`.github/workflows/ci.yml` runs exactly the three commands above (restore,
build, test) on `windows-latest` and on `ubuntu-latest` for every push to `main`
and every pull request that touches something other than documentation, and
keeps each leg's `.trx` and coverage file as its own artifact. `fail-fast` is
off, so one platform going red still shows what the other did. A second push to
the same pull request cancels the run still going for the previous one; pushes
to `main` are left to finish, so the badge and the history keep a result for
every commit.

A `format` job runs beside the two legs, on `ubuntu-latest` alone: it restores
the tools pinned in `.config/dotnet-tools.json` and runs
`dotnet fantomas --check Marbas Marbas.Tests`, which fails when any file would be
reformatted. One platform is enough, because the `.editorconfig` fixes the line
endings to `lf` and the formatting does not depend on anything else the
platforms differ in.

What the suite cannot check on either platform — colours, a resized terminal,
the mouse, a real permission bit, an editor sharing the terminal — is the
checklist in [CROSS-PLATFORM.md](CROSS-PLATFORM.md).

Four lines have no test and cannot get one from here: the `with ex ->` in
`DirectoryListing.read` and in `Operations.isNonEmptyDirectory`, and the two
`asOperationError` cases that only a failing listing produces. All three sit
behind a directory enumeration that throws, and `MockFileSystem` has no way to
make one throw — a file can be locked or made read-only, a directory cannot.
What each of them maps is unit-tested directly through `mapException`, so the
gap is the plumbing, not the rule. The `with _ ->` in `FileSystemPath.linkTargetOfInfo`
is the same kind of line: it keeps one reparse point the
system will not read from failing the whole listing, and the mock has no such
entry. `AppState.navigateToParent`'s `None` branch is
unreachable by construction: only `..` leads there and a root directory is never
listed with one.

## Layout

`Domain/`, `FileSystem/`, `Messages/`, `State/` and `Ui/` mirror the source
folders they cover, one test module per module under test, and namespaces
follow: `Marbas.Tests.State.PanelStateTests` covers `Marbas.State.PanelState`.
`Ui/` and `FileSystem/` mirror the source down to the subfolder, so
`Ui/Panels/PanelTitleTests.fs` covers `Ui/Panels/PanelTitle.fs` as
`Marbas.Tests.Ui.Panels.PanelTitleTests`, and
`FileSystem/Archives/ArchiveReaderTests.fs` covers
`FileSystem/Archives/ArchiveReader.fs` as
`Marbas.Tests.FileSystem.Archives.ArchiveReaderTests`. `Messages/ProblemTests.fs` holds
every case of `Problem` to the title and the description of the module that
words it, so a case wired to another module's words fails by name. The
interpreter's tests are the `Shell*Tests` files of `State/Shell/`: they run
`Shell.interpret` over `Fakes.dependencies` and build no view. Everything in
them is a unit test — pure, fast, no Terminal.Gui.

**No test file runs past a thousand lines.** That is a ceiling and not a size
to aim at: most files are a few hundred lines, and a split leaves files of that
size. A module whose tests would not fit is split along the features it covers,
one file per feature, each named for the module and then the feature —
`ShellDeleteTests`, `OperationsBatchTests`, `FileViewerSearchTests`,
`HistoryDialogFilterTests`, `ClipboardPathTests`. Where a split leaves more
files than sit comfortably among the others, they get a folder under the folder
of their layer, named for the module, and the namespace follows it:
`State/Shell/` holds the interpreter's tests as
`Marbas.Tests.State.Shell.ShellDeleteTests`, and `State/Update/` those of
`Update`. The others stay where the one file was — the `Operations*Tests` of
`FileSystem/`, the `Clipboard*Tests` of `State/`, the `FileViewer*Tests` of
`Integration/Viewer/` and the `HistoryDialog*Tests` of `Integration/Dialogs/`.

**What the files of one split share is a fixture module.** It sits beside them,
is compiled before them and is opened by name: `ShellFixture` carries the
do-nothing dependencies, the `capture` that records what was dispatched and the
helpers for text typed earlier; `ShellArchiveFixture` the archive most of the
archive tests unpack; `UpdateFixture`, `OperationsFixture`, `ClipboardFixture`,
`FileViewerFixture` and `HistoryDialogFixture` the arrange code of their
namesakes, as `ArchiveFixture` has done for the archive tests of `State/` since
before the split. A fixture holds no test, and a helper only one file needs
stays `private` in that file; the aliases every file writes for itself — `at`,
`fp`, `en` — are not shared either.

`Integration/` holds every test that constructs a Terminal.Gui view, from a lone
dialog up to a whole `MainWindow` driven by keystrokes. That is the line: a test
that touches Terminal.Gui needs `BeginInit`/`EndInit`, a layout pass and the
process-wide statics that force parallelisation off, so it belongs on the other
side of the fence from the pure ones. It carries the same subfolders as
`Marbas/Ui`, so a feature's unit tests and its Terminal.Gui tests sit at matching
paths on either side. `Disk/` and `Clipboard/` are the two subfolders with no
`Ui` counterpart: they hold the tests that need the real file system or the
real clipboard rather than a view, which are integration tests for the same
reason — they reach outside the process.

`Adapters/` is the exception the rule needs. Almost nothing there can be
reached without starting the real thing, which is why the folder is excluded
from coverage — but a branch that refuses *before* it starts anything is
reachable, and P10.6 made one: `LaunchProcess.start` takes the platform as an
argument, so `Adapters/LaunchProcessTests.fs` can ask a Unix system for
administrator rights on a Windows machine and read the refusal back. Only that
shape of test belongs there: no process, no registry, no real disk.

The split is what makes a file like `State/DeleteTests.fs` (does the panel come
back right once the entries are gone?) readable next to
`Integration/App/DeleteKeyTests.fs` (does `F8` confirm and then reach the trash?) —
they used to be one file. Either half
runs on its own:

```
dotnet test --filter "FullyQualifiedName~Marbas.Tests.Integration"
```

Six modules sit outside the folders because everything uses them: `Root` (the
platform's root, and paths built on it), `WindowsFact` and `UnixFact` (the `Fact`
that skips where drive letters do not exist, and the one that skips where they
do — with `UnixTheory` beside it in the same file since P22.5, the same skip
for a theory), `Expect` (result unwrapping), `Fakes` (the
do-nothing `Ports.Dependencies`) and `AssemblyInfo`. `Integration/Gui.fs` adds the view helpers — `focusedWindow`,
`leftList`, `rightList`, `press`, `panel`, `panelTitle`, `shownPath`, which
is the path part of a panel title, so a navigation test says which directory a
panel shows without repeating the sort indicator that follows it, and
`drawnText`, which is the text a view formats for the screen rather than the
text it was handed. `pressKey`
sends a key to the panel that has focus — since the bottom bar stopped binding
its keys application-wide, a function key only arrives if it is sent where the
user's keystroke would go. F# does not open an enclosing
namespace for you, so a file under `State/` that uses `Expect.ok` needs an
explicit `open Marbas.Tests`.

## The layers a test holds

The order of compilation stops a layer from calling one above it, but not from
calling the system: nothing in the compiler keeps `File.Exists` out of
`State`. Since P22.13 a test does. `Layers/` holds the tests about the
assembly as a whole rather than one module: `SystemCalls.fs` reads the
compiled `Marbas.dll` with Mono.Cecil, and `SystemCallTests.fs` asserts on
what it finds. `LayerReferences.fs` and `LayerReferenceTests.fs`, below, do
the same for which layer uses which.

The scan walks every instruction of every method of every type, closures and
nested types among them, and takes each call, `newobj`, `ldftn` and the like
whose target is one of these:

| What the outside world answers | Watched |
|--------------------------------|---------|
| the files, directories and drives | `File`, `Directory`, `FileInfo`, `DirectoryInfo`, `FileSystemInfo`, `DriveInfo`, `FileSystemWatcher`, `FileStream`, `RandomAccess`; of `Path` only `GetFullPath`, `GetTempPath`, `GetTempFileName` and `Exists`; the real `System.IO.Abstractions.FileSystem` |
| processes and the session | `Process`, `Environment`, `Console` |
| the platform | `OperatingSystem`, `RuntimeInformation`, `Microsoft.Win32`, `System.Net` |
| threads and timers | `Thread`, `ThreadPool`, `System.Threading.Timer`, `System.Timers.Timer`, `Task.Run` |
| the clock and chance | `DateTime.Now`, `UtcNow`, `Today`; `DateTimeOffset.Now`, `UtcNow`; `Stopwatch`; `Guid.NewGuid`; `Random` |
| native code | every method declared as a P/Invoke |

What is left out is left out on purpose: streams, `MemoryStream`,
compression in memory, the exception types of `System.IO`, the string work of
`Path`, and every member of `IFileSystem`, which is how the layers below reach
the disk.

A method belongs to the folder of the source file its first sequence point
names, which the symbols beside the assembly carry; a method with none — a
P/Invoke, a member the compiler made — belongs to the namespace of its
outermost type. The file matters more than the namespace for the static
initialiser of a file that opens with `namespace`, which F# compiles into
`<StartupCode$Marbas>.$<file name>` and so names no layer at all.

CI collects coverage, and the coverlet collector instruments `Marbas.dll` in
place before the tests read it: every method gains a call to a tracker type it
adds under `Coverlet.Core.Instrumentation.Tracker`, and that tracker writes its
hits to disk. The scan skips the types of the `Coverlet` namespace, so a run
with `--settings coverlet.runsettings` sees the same assembly a run without it
does.

`nothing outside Adapters and Program reaches the system directly` fails with
one line per call, naming the method and what it called:
``Marbas.State.Runtime/Runtime`3::paint calls System.IO.File::Exists``.
`the scan sees the calls Adapters makes to the system` runs the same scan over
`Adapters` and expects to find some, so that a scan gone blind — symbols
missing, a folder renamed — cannot pass the first. A way of reaching the
system the table does not hold joins it when `Adapters` first takes it up,
and the first test then says whether a layer below was already using it.

The order of compilation keeps only half of the table of §1 of
[Coding style](../coding-style-conventions.md#1-layers) besides: it stops a
layer from naming one above it, never one beside it. Since P23.1
`LayerReferences.fs` holds that table as `mayUse`, and
`LayerReferenceTests.fs` asserts on it. The scan reads `Marbas.dll` once, with
its symbols, and places every type in the layer the second segment of the
full name of its outermost declaring type names: `Marbas.State.Shell` is
`State`, `Marbas.Ui.Panels.FilePanel` is `Ui`, `Marbas.Program` is `Program`.
A folder inside a layer stays in it: `Marbas.FileSystem.Archives.Archive`, in
`FileSystem/Archives/` since P23.7, is `FileSystem`.
What the compiler generates — `<Module>`, `<PrivateImplementationDetails$Marbas>`
and every other type whose name opens with `<` — and the `Coverlet` tracker
are left out, with one
exception: the static initialiser of a file, `<StartupCode$Marbas>.$…`, holds
the module-level values of the file and so the references they make, and is
placed as `SystemCalls.fs` places a method, by the folder of the source file
its first sequence point names; one with no method has nothing to place.

Of each type the scan collects what it names: its base type and interfaces,
the types of its fields, the parameters and results of its methods, the
variables of their bodies, and every type, method and field an instruction
names, with the declaring type, the parameters and the result of a method
named and the type of a field named. Each of these is taken apart into its
generic arguments and element types, and only what is declared in `Marbas`
and not generated is kept, placed by its outermost type as well.

| Test | Fails with |
|------|------------|
| `every type of Marbas is in a layer of the table` | ``Marbas.Stray.Planted is in Stray, which is no layer of the table`` |
| `every layer uses only itself and the layers its row allows` | ``Marbas.Messages.ArchiveErrorMessage (Messages) uses Marbas.Collections.Zipper`1 (Collections)`` |
| `every layer of the table holds a type` | ``Find holds no type`` |
| `the scan sees the layers use the ones below them` | an empty list: the scan has gone blind |

The two first messages are the ones the planted failures of P23.1 gave. A
layer is added to the table, as a case of `Layer` and a row of `mayUse`, before
its first module moves into it, so that the third test fails until it does;
and a row is changed in the same change as §1 of Coding style.

## Arrange, act, assert

Every test is three blocks separated by a blank line — set the world up, do the
one thing under test, then assert. There are no `// Arrange` comments; the blank
lines carry the structure.

The act is a named binding, even when the call would fit inside the assertion:
`let rows = Drives.list fs None` followed by `Assert.Equal<DriveRow
list>([...], rows)`, never `Assert.Equal<DriveRow list>([...],
Drives.list fs None)`. Where a test
needs to compare before and after, the "before" value is captured in the arrange
block and asserted at the end, so no assertion sits between two acts.

`Expect.ok` and `Expect.error` unwrap a `Result` so it can be acted on in one
line; without them the `match` that unwraps the result also holds the
assertions, which fuses act and assert. A `Result` that *is* the thing under
test stays whole and is compared with `Assert.Equal(Error(...), result)`.
`Expect.some` does the same for the `Option` that a validating constructor
answers with, so `NamePattern.tryCreate "*.fs" |> Expect.some` builds a value
in the arrange block instead of branching there. `Expect.notNull` does it for
a value .NET or Terminal.Gui types `| null` —
`(Expect.notNull list.Source).Count`,
`archive.GetEntry name |> Expect.notNull` — so that a null fails the test with
a sentence, and the tests are ready for nullness checking, which only `Marbas`
builds with today ([Development](../DEVELOPMENT.md#build-configuration) says
why). Total constructors need no
such help: modules that have one expose it as `create`, and tests alias it at
the top the way they already alias `fp` — `let private en = EntryName.create`.

`Integration/Gui.fs` carries the arrange boilerplate: `focusedWindow deps`
builds, initialises and focuses a `MainWindow`, and `leftList` / `rightList`
reach the panel `ListView` a test drives. `pressReturning` is `press` that hands
the key back, for the tests whose subject is whether it was handled.

## Injecting the outside world

`MainWindow.create` takes a `Ports.Dependencies` record — the file system and
the three paths at the top, then four sub-records: `Prompts` (error reporter,
confirmation, overwrite question, the name, target, pattern and password
prompts, the drive, tab, find, link, handler, properties and settings popups,
the file viewer), `Processes` (trash, editor, launch, links, handlers, drives,
the session's owner and whether an owner runs), `Watches` (modifiers,
directories) and `Runners` (batch, measure, search, background).
Everything that would open a modal dialog, start a process or end the process
is a function, so tests pass fakes and assert on what was requested.
`Adapters/Prompts.fs` is the only place that supplies the real `MessageBox` dialogs.
`Fakes.dependencies` gives every port a do-nothing default — a
confirmation that answers no, an overwrite question that skips, prompts that
cancel — so a test only overrides the one it cares about, naming it through its
sub-record:
`{ Fakes.dependencies fs start with Prompts.AskName = fun _ -> Some name }`.
Each prompt takes one request record, so a fake reads the field it cares about
and ignores the rest; the prompt that carries a suggestion (`AskTarget`) is
easiest to fake as `fun request -> Some (FileSystemPath.value request.Suggestion)`,
which is exactly "the user pressed Ok".

**Background work is a port, so tests stay synchronous.** `Runners.Batch` is the
dependency that copies and moves go through; `Fakes` supplies one that
runs the work inline — `fun request -> request.Work Operations.unattended |>
request.Finish`, the request read by its fields — so a test presses `F5` and
finds the files already copied when the call returns. Nothing in the suite
waits on a thread. What that does *not* cover is `BatchRunner` — the real
runner, its progress dialog and the `Application.Invoke` marshalling are only
ever exercised by running the app. The pieces taken out of it are pure enough
to test without one: `BatchRunner.control` writes into a cell the test holds,
so it can show that a report only overwrites that cell; `BatchRunner.showLatest`
is the timer's tick, called by hand to show that it takes the latest path and
that a second tick with nothing new shows nothing; `counting` and `concluded`
are the debug log's count and verdict.

**Held modifiers are a port too.** `Watches.Modifiers` is handed a `ModifiersWatch` at
startup; a test keeps its `Report` and calls it with the modifiers it wants
held, which is how `Integration/Keys/ModifierRowTests.fs` presses no key at all and
still asserts what the bottom bar shows. The polling itself lives in
`ModifierWatch` and is only exercised by running the app.

**An external program is a port too.** `StartEditor` is what `F4` goes
through, so a test hands it a function that records the path — and, where the
point is that no refresh follows, one that writes into the `MockFileSystem`
before answering `Ok`. Nothing in the suite starts a process: what the adapter
would start is a `ProcessStartInfo` that `EditorProcess.describe` and
`LaunchProcess.describe` answer without starting it.

**The settings file is not a port.** `SettingsPath` is a plain path on the
dependencies and `Settings.load`/`Settings.save` use the injected file system, so
a test points the path into its own `MockFileSystem` and the real config file is
never touched. That is what lets `Integration/App/SettingsTests.fs` state the whole
promise of P7.4 in one test: build a window, sort it, show its hidden entries,
send another panel to a second drive, press `Ctrl+Q`, then build a second window
over the *same* mock file system and read the titles back. `WorkspacePath` is
the same arrangement for the session's working directory, which is why "quitting
takes it with it" is an ordinary `ShellQuitTests` assertion.

**Every window a test builds claims a working directory.** `MainWindow.create`
raises the sweep of P21.7 through `Startup.firstSteps`, and the fake `Runners.Background` runs it inline, so
after `MainWindow.create` the mock file system holds
`temp/Marbas/session/owner`. The fake `Processes.IsRunning` answers yes to every
owner, so nothing a test planted under `temp/Marbas` is removed unless the test
overrides it. The sweep's own rules are tested below the window:
`WorkspaceTests` hands `Workspace.sweep` the moment it is and a function for
who runs, and dates an unclaimed directory by setting `LastWriteTime` on the
file and on each directory above it — a mock directory is otherwise as old as
the test. `Fakes.HeldOpen` is the file that will not be deleted.
`SessionProcessTests` is the one place that asks the real system: about the
test host itself, and about an id no process has.

**A failure is arranged on the file, not faked around it.** `MockFileData` is
what makes the error paths testable: `Attributes <- FileAttributes.ReadOnly`
makes a delete or a write throw `UnauthorizedAccessException`, which is the
`AccessDenied` case, and `AllowedFileShare <- FileShare.None` makes every open
throw `IOException`, which is the unexpected-failure case that carries the OS
message. That is how "delete refuses a read-only file", "the viewer reports a
file it cannot open" and "an unreadable settings file falls back to the
defaults" are ordinary tests rather than mocks of the file system interface.

**What `MockFileData` cannot arrange is subclassed, not mocked.** Two failures
have no representation on a file: a directory the operating system will not
list or will not let you make, and a disk that refuses every write.
`Fakes.UnreadableDirectory`, `Fakes.UnwritableDirectory` and `Fakes.FullDisk`
are `MockFileSystem` subclasses that override one member each —
`MockDirectory.EnumerateFileSystemEntries` and `MockDirectory.CreateDirectory`
to raise `UnauthorizedAccessException` for one named path, and `MockFile.Create`
to hand back a stream that raises `IOException` on every write, for every file
or for one named one — and are otherwise the
mock file system the rest of the suite uses, so the arrange block still says
`AddFile` and `AddDirectory`. They are what proves that a copy reports a
directory it may not read instead of throwing past its batch, and that an
archive that cannot be finished is deleted rather than left unreadable. Anything
`MockFileData` *can* express belongs on the file: these two exist because
nothing else reaches those code paths. `Fakes.UndatableDirectory` is the same
shape for one more refusal, a directory that will not take a time, and holds
the rule that such a directory fails the target it was unpacked for.

**What a walk asks the file system is counted, not read off the code.**
`Fakes.WalkWitness` is a `MockFileSystem` whose `File`, `Directory`, `FileInfo`
and `DirectoryInfo` count what they are asked about: `Enumerations` says how
many times a directory was enumerated, and `AskedAbout` how many times a path
was probed — `Exists`, or an info built for it. It stops counting while a mock
operation is carrying itself out, and while an enumeration is building the
infos it answers with, so what is left is what Marbas itself asked. That is
what pins the promise of P15.14 for all six walks: a tree of three directories
is enumerated three times, and nothing is asked about a file inside it.

A UI test that needs the two panels on *different* directories gets there the
way a user would: `press list KeyMap.rightRootKey` with a `ChooseOption`
that answers with the wanted path. No test reaches into the state to set it.

## Terminal.Gui pitfalls

**`Key` is mutable and `Handled` sticks.** Sending a shared `Key` instance
(such as `KeyMap.refreshKey`) through `NewKeyDownEvent` sets `Handled` on
that object permanently; every later send of the same instance is ignored and
handlers stop firing. Use `Gui.press`, which sends a fresh copy.
The symptom of getting this wrong is a test that passes alone but fails as
part of the suite.

**Keys do not bubble.** `NewKeyDownEvent` on a nested view is not forwarded to
its parent, so key handlers are attached to the view that actually holds focus
— the panel's `ListView`.

**`View.AddCommand` is `protected`.** Custom shortcuts use `KeyDown` handlers
instead.

**Application-level bindings need the running app.** Defaults such as
Tab → `NextTabStop` and `Command.Quit` are not exercised by
`NewKeyDownEvent` in unit tests. Assert the binding
(`Application.GetDefaultKey`) and the resulting behaviour (`AdvanceFocus`)
separately.

**A `Dialog` is much smaller inside than out.** Border, button row and padding
eat six rows, so `Height = 7` leaves a one-row content area and anything placed
below it is clipped — while still taking focus and keys, which looks like an
invisible but typable field. Dialogs are built in `Ui` modules, not inline in
the adapter that runs them, so a test can lay one out and assert its views fit
inside `dialog.Viewport`.

**`ListView` type-ahead swallows printable keys.** `ListView.OnKeyDown` runs
before the `KeyDown` event and hands printable characters to its
`KeystrokeNavigator`, which reports a match even when nothing matches — so
`+` and `-` never reached Marbas until the panel set
`KeystrokeNavigator <- null`. The symptom is `NewKeyDownEvent` returning `true`
with `key.Handled` still `false`.

**StatusBar shortcuts swallow their keys.** A `Shortcut` marks its key handled
even with a `null` action, which is why every shortcut that should do
something gets a real callback. That is the reason the bottom bar stopped being
a `StatusBar` of `Shortcut`s: a plain `View` of slot views takes no key at all,
and a test asserts it by sending `F5` to the bar and finding it unhandled.

**A mouse click is raisable from a test.** `View.NewMouseEvent` is public and
takes a `Terminal.Gui.Input.Mouse`, so
`slot.NewMouseEvent(Mouse(Flags = MouseFlags.LeftButtonClicked))` is a click on
a bar slot — no driver, no layout pass, no `Position`. That is what covers the
one input the bottom bar owns.

**A key that escapes a view can only be caught as `Handled`.** Focus navigation
runs above `NewKeyDownEvent`, so a test cannot watch `↓` at the last entry move
to the other panel — what it can assert is that the panel marked the key
handled, which is the whole mechanism that keeps it in. `Gui.pressReturning`
sends a fresh key and hands it back for exactly that assertion; `Gui.press` is
it with the answer thrown away.

**`ListView.Viewport.Y` is the list's scroll offset** and `Viewport.Height` the
rows it shows, which is what the page jumps are measured from. Both are zero
until a layout pass, so a test about them lays the panel out first —
`view.Layout(Size(40, 12))` — and one test asserts `Viewport.Y > 0` after
scrolling, so the assumption fails loudly if a future version moves the offset
elsewhere.

**`RowRender` is raisable from a test.** `ListView.OnRowRender` is public, so a
test constructs a `ListViewRowEventArgs row`, raises it and reads
`RowAttribute` back — the one thing about colour that is observable without a
driver. `null` there means "leave the row alone", which is what the row under
the cursor of a focused list must get; a marked row comes back as
`ClassicScheme.markedEntry`.

**A built-in move of `ListView` is a real key in a test.** `↑`, `↓`, `PgUp`,
`PgDn`, `Home` and `End` reach the list's own commands through
`NewKeyDownEvent`, exactly as at the keyboard, so a test that presses them runs
`ListView.SetSelection` — which is how the vanishing marks of P17.1 were caught
without a driver. Setting `SelectedItem` does not go that way, so a test about
what a cursor move does to the view presses the key rather than setting the
property.

**`View.Text` is what was set, not what is drawn.** A `Label` that eats an
underscore as its hot key specifier still hands the underscore back from `Text`,
so an assertion there cannot see the bug at all. `Gui.drawnText` reads
`view.TextFormatter.GetLines()` instead, which is the formatted text with the
specifier already removed — the closest a driverless test gets to the screen.
`TitleTextFormatter` is not public, so the panel frame is covered the other way
round: a title carrying an underscore must leave `view.HotKey` empty. Both need
a layout pass first, and the underscore has to be followed by a character —
a trailing `_` is not a specifier and survives untouched.

**`RequestStop` does nothing outside a run.** `Runnable.RequestStop` goes
through the application that is running the dialog, so a dialog built in a test
never sets `StopRequested` — not even after `SetIsRunning true`. A view that
has to close itself therefore takes the closing action as an argument
(`FileViewer.create close …`), which a test can record and `Prompts.showFile` fills
in with `_.RequestStop()`.

**A view that paints by hand is asserted through what it would paint.** There
is no driver in a test, so nothing can read the screen back. `FileViewer`'s
content view therefore computes its rows in one place — `Rows()` — and
`OnDrawingContent` only paints what that answers. A test calls `Rows()` and
compares strings, which is how scrolling, wrapping, the hex switch and a
UTF-16 file are all covered without a terminal. A view that painted straight
from its own state inside the override would be untestable here.

**What a view remembers is tested without the view.** The viewer, the results
window, the settings popup and the user menu popup each keep their state in
one record, and what a key or a tick does to it is a function of
`ViewerState`, `ResultsState`, `SettingsForm` or `UserMenuState`. Those are
tested in `Ui/` like any pure module — a
`MockFileSystem` for the file the viewer reads, a `Size` for its screen, no
`BeginInit`, no layout — one test per transition. The tests in
`Integration/` that press a key on the real view stay beside them: they hold
the wiring, which is all a view is left with, and they are what passed
unchanged when the state moved out.

**Colours are asserted as schemes, never as pixels.** There is no driver in a
test, so nothing can read back what was painted. What a test can assert is the
`Scheme` a view resolves to — `view.GetScheme()` — and the attributes the scheme
holds, which is what `Integration/Theme/SchemeTests.fs` and `Ui/Theme/ClassicSchemeTests.fs`
do. A view without a scheme of its own inherits its super-view's, so the panel
list and the status line are covered by the schemes set on the panel frame and
on the window. It is also why the bottom bar draws a key name and its command as
two `Label`s rather than as one line in two attributes: two views are two
schemes to read back, one hand-painted line is nothing at all.

**Which `VisualRole` a view draws with is observable.** Subscribing to
`view.GettingAttributeForRole`, laying the view out and calling `Draw()` records
every role it asks for, in order, which is how the palette was matched to
`ListView`, `FrameView` and `StatusBar`. Useful when picking colours; too
coupled to Terminal.Gui's internals to keep as a test.

**Parallelisation is disabled** (`AssemblyInfo.fs`) because Terminal.Gui keeps
process-wide static state, including default key bindings.

## Layout assertions

Formatting depends on the panel width, so rows are only column-formatted after
a layout pass. Tests that assert on column alignment call
`window.Layout(Size(width, height))` first; tests that only check entry names
inspect the unformatted rows produced before the first layout.

## Seeing the screen

What no assertion reaches — a frame, a shadow, a glyph a font may not carry, one
attribute against another — can still be read rather than guessed. A throwaway
console project in the scratchpad, referencing `Marbas.fsproj`, runs the real
view under `Application.Create().Init()` with a piped stdout and prints
`Driver.ToString()` as text and `Driver.Contents[row, col]` as colours; a
timeout injects keys and takes the shot from inside the loop. The popups of the
`DialogStyle` round were settled that way, before and after side by side. It
belongs beside the suite rather than in it: it needs a driver, it is slow, and
what it proves is a picture. The `render-tui-screen` skill in `.claude/skills`
carries the harness and the traps — chiefly that the screen measures 0x0 when
stdout is piped, which makes `MessageBox` throw until it is set.
