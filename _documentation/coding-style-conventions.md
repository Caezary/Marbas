# Coding style and conventions

The style Marbas holds itself to. It is the target, not a description of every
line that exists today: where the code still differs, the reviews of 2026-09-19
in [reports/](reports) name the place and [phase 15](Plan/PHASE-15-ANSWERING-THE-REVIEWS.md)
schedules the fix. A rule here beats a habit found in an older file.

## 1. Layers

| Layer | Holds | May use |
|-------|-------|---------|
| `Collections/` | `Zipper` and the like — no domain knowledge | nothing |
| `Domain/` | the vocabulary: `EntryName`, `NamePattern`, `SortOrder`, `HiddenEntries`, `FindQuery` | `Collections` |
| `FileSystem/` | reading and writing disks and archives, every call through `IFileSystem`; `FileSystemPath` is declared here, not in `Domain` | `Domain` |
| `Profile/` | what the user keeps between sessions: the settings, the history, the favourites, the user menu, each read and written through `IFileSystem` | `FileSystem`, `Domain`, `Collections` |
| `Locations/` | where a panel stands, where it lands, what it lists | `FileSystem`, `Domain` |
| `Viewing/` | a file's content as a window of bytes and lines: its encoding, the window, the lines, the viewer's searches | `FileSystem`, `Domain` |
| `Find/` | searching by name and by text, on disk and inside archives | `Locations`, `Viewing`, `FileSystem`, `Domain` |
| `Messages/` | the words the application says: an error or a question -> text, and `Problem`, everything that is reported to the user | `Profile`, `FileSystem`, `Domain` |
| `State/` | `AppState`, messages, transitions, `Effect`; `Runtime` holds the one mutable cell; then `Ports.Dependencies` as ports, `Shell.interpret` and `Startup` | `Messages` and below |
| `Ui/` | views, key maps, and the wiring of both to the runtime | `State` and below |
| `Adapters/` | the real ports: dialogs, processes, registry, watches, runners | everything above it |
| `Program.fs` | starts the application, nothing else | `Adapters` |

- [ ] Dependencies point downward only, and only to the layers a row allows.
      The table is a test: `Layers/LayerReferenceTests` reads the compiled
      assembly and fails on a type whose namespace names no layer of it, on a
      reference its row does not allow — a layer beside, which the compile
      order in `Marbas.fsproj` lets through, as much as one above — and on a
      layer of it that holds no type; the test and this table change
      together. If a reference needs to point up or sideways, the type is in
      the wrong layer.
- [ ] Only `Adapters` touches the operating system directly. Everything else
      reaches it through `IFileSystem` or a port on `Ports.Dependencies`.
      `Layers/SystemCallTests` holds the rule: it reads the compiled assembly
      and fails on a call outside `Adapters` and `Program.fs` into the files,
      directories and drives of `System.IO`, processes, the environment, the
      console, the registry, the network, the platform, threads and timers,
      the clock, `Guid.NewGuid`, `Random`, `Stopwatch` or a P/Invoke —
      [Testing](Architecture/TESTING.md#the-layers-a-test-holds) has the list.
- [ ] A type belongs to the lowest layer that speaks it, not the one that needed
      it first.
- [ ] One folder is one namespace, and a folder may only depend on folders above
      it in the compile order. No folder is compiled in the middle of another.
- [ ] `Messages` words values and holds none of its own but `Problem`. A value
      it has to word is declared in a layer its row allows; a transition names
      a `Problem` and never a title or a sentence.
- [ ] A port takes one value. Where it has to be handed something to call, the
      function is a named field of a request record — `BatchRequest.Finish`,
      `LocationsWatch.Notify` — never a second argument, and a function a prompt
      carries has a type whose name says what it answers (`HistoryAfter`).
- [ ] Inside `State`, the ports come last. A transition cannot name `Ports` or
      `Shell`, because neither is compiled yet where `Update` is; what it wants
      from the world it raises as an `Effect`.
- [ ] Nothing under `State/` opens a `Terminal.Gui` namespace. A module that
      needs one is a view or its wiring, and belongs to `Ui`.
- [ ] `AppState` is immutable. A transition answers a new state; handlers never
      mutate.

## 2. Names

| Write | Not | Why |
|-------|-----|-----|
| `strictlyUnder`, `atOrUnder` | `below`, `under` | a preposition alone hides whether the boundary is strict |
| `segmentsFromTargetName` | `beneath` | say what is compared, not where it sits |
| `moveFromDisk`, `moveToArchive` | `moveIn`, `moveAcross` | a direction names both ends |
| `requestIfChangeable`, `requestUnlessRefused` | `guarding`, `refuseUnchangeable` | a gate is named for what it does when it lets through, and the condition says when it does not; a name that only refuses lies on the path where nothing is refused |
| `makeDirectory fs state name` | `makeDirectory'` | no primed aliases; pass the argument |
| `cursorEntry` | `getCursorEntry` | a function is named for what it answers |
| `keepUnfinished`, `wouldPackItself` | `handleResults`, `check` | the name carries the rule |

- [ ] A reader should not have to open the body to learn the rule.
- [ ] Two helpers in one module never differ only by a prime or a preposition.
      [Names that tell the boundary](DEVELOPMENT.md#names-that-tell-the-boundary)
      works the rules through the renames of P15.20.
- [ ] Module-level helpers are `private` unless a test or another module needs
      them.
- [ ] English everywhere in the repository. Conversation with the user is in
      Polish.
- [ ] No third-party file manager names anywhere — say "orthodox" or "classic".
- [ ] No personal names in fixtures or samples: `user`, `owner`, `start`.

## 3. Types before primitives

- [ ] A name, a path, a pattern, a password, a program is a single-case union
      with a private constructor and a companion module.
- [ ] Constructors follow one rule: `create` is total, `tryCreate` validates and
      answers an `option`, `value` unwraps. Trimming and the blank check live in
      `tryCreate`.
- [ ] A boolean parameter or field is a two-case union instead — `HiddenEntries`,
      `SelectionMode`, `CursorDirection`, `LetterCase`, `WordMatch`, `LinkWalk`,
      `Elevation`. A record of three booleans is a set of cases.
- [ ] A moment is a `DateTimeOffset`, never a `DateTime`.
- [ ] An identity carries its place. A bare name is not an identity: two entries
      in different directories may share one.
- [ ] Impossible states are unbuildable — `FindQuery` cannot carry options
      without text, `Zipper` cannot be empty, `ArchivePath` cannot climb.
- [ ] **Never let a null propagate into the system**. Always convert them with
      `| null ->` arm, `Option.ofObj`, or `NonNull` to an `option` or a plain
      value, exactly where the null would originate.
- [ ] A view id, a label string or a `%O` rendering is never the identity of
      anything.
- [ ] Where a domain type looks impractical, ask before falling back to a
      primitive; a deliberate primitive (an ordinal index, text not yet parsed)
      is written down where it is chosen.

## 4. Functions and control flow

| Instead of | Write |
|------------|-------|
| `match a with … -> match b, c with … -> match d with …` | a match over the tuple, or a `result {}` / `option {}` with the refusals as guards |
| three or more `Result.bind (fun x -> …)` | `result { let! … }` |
| `request state (Report (Error (spelled fs a b)))` | `spelled fs a b \|> Error \|> Report \|> request state` |
| `match ask () with \| Some x -> act x \| None -> ()` | `ask () \|> Option.iter act` |
| a `bool` parameter | a two-case union |
| a continuation passed in to avoid repeating a guard | decide the guard once, before the match |
| forty partial applications at the top of a function | pass the argument at the call site |
| a four-clause `if`, one clause with a side effect | one named predicate per clause |
| free `mutable`s across a recursion | a record threaded through the walk |

- [ ] One statement per line. Where a pipeline or an argument list breaks is
      Fantomas's decision, not the author's (§6).
- [ ] Pipelines read left to right; nested parentheses three deep do not.
- [ ] A match that only hands out work — `Update.transition` over the
      messages, `Shell.interpret` over the effects — has one-line arms, so
      that it reads as an index of what each case does. An arm that needs more
      is a private function above the match, named for what it decides or
      does and taking what the arm had: `fs` and the state, or `deps` and
      `dispatch`, and what the case carries. A pipeline Fantomas would break
      over several lines takes a name too.
- [ ] Computation-expression cut-off: three binds, or one nested match.
      FsToolkit.ErrorHandling is already referenced — `result`, `option`,
      `Result.requireTrue` and friends are in scope.
      [Flat decisions](DEVELOPMENT.md#flat-decisions) works the rules through
      the sites they were first applied to, and says when a tuple match is the
      wrong answer.
- [ ] Outside `Ui`, a `mutable` binding, a reference cell, a `ResizeArray`, a
      `Dictionary` or an array written to after it was made earns its place in
      five ways, and in no other:

| Where | What it covers | Today |
|-------|----------------|-------|
| a hot loop that was timed | a loop over bytes or entries, confined to one function or to the private state of one type; nothing mutable leaves it | `LineIndex` (the scan of a chunk, the two splits, the binary search), `TextEncoding.isUtf8`, `ArchiveReader.stepping` |
| a contract .NET sets | a `byref` an interop call fills; the position a `Stream` subclass has to keep | `NativeLinks`, `SystemClipboard`, the queue read in `SearchRunner`; `RewindingStream`, `OwningStream` |
| a cache that only saves work | private to one module or one object, and what it answers is the same with it and without it | `ArchiveIndex`; the checkpoints, blocks and texts of `LineIndex.Index` and how far it has scanned; the buffer `ZipMoments.read` borrows from the shared pool for a central directory |
| what an adapter keeps for the system | what it has to remember between two callbacks or timer ticks, or share with a thread it started | `BatchRunner`, `SearchRunner`, `WindowSearchRunner`, `DirectoryWatch`, `ModifierWatch` |
| the runtime's cell | the state, the queue of messages and the two flags of the one loop | `State/Runtime.fs` |

- [ ] Everything else is a fold or a returned value. A walk threads a record
      and answers it — `FileSearch.run` its counts, the places it has visited
      and whether it was stopped; unpacking its progress per target — and a
      read that calls back is a fold: `ArchiveReader.fold` hands each step what
      the steps before it made, and `ArchiveReader.walk` answers what was
      reached even when the archive breaks half-way.
- [ ] Setting a property of an object a library made — a `ProcessStartInfo`, a
      zip entry's date, a stream's position — is a call into that library, not a
      binding of ours, and the rule does not count it.
- [ ] `Ui` is outside the rule: a view holds what is on screen. What a view
      keeps is the subject of §9.
- [ ] A function that grows past one screen has lost a seam — lift the inner
      walk out with explicit inputs and an explicit result.
- [ ] Partial access (`.Value`, `List.head`, `Seq.pick`) only where the type
      cannot express the guarantee, and then next to the code that established
      it.
- [ ] A string is compared with a named `StringComparison`:
      `key.StartsWith(prefix, StringComparison.Ordinal)`, never
      `key.StartsWith prefix`. Without one, `StartsWith`, `EndsWith`,
      `IndexOf` and `LastIndexOf` with a string compare by the culture of the
      machine, through its ICU: a key that opens with a soft hyphen then
      starts with the prefix, and is cut at the prefix's length one character
      short. What is read from a file — the settings, the history, the user
      menu, the MIME database — or typed is compared by ordinal, as
      case-sensitive as it needs to be; `OrdinalIgnoreCase` says so where it
      is not. The analyzers of §11 hold the rule.

## 5. Errors

- [ ] Errors are values. A module answers `Result<_, SomeError>`; no exception
      leaves it.
- [ ] Exactly one place per family turns an exception into an error —
      `Operations.attempting`, `ArchiveReader.mapException`,
      `ContentError.mapException`.
- [ ] `with _ -> …` is allowed only where "an error is a reason to skip" is the
      rule: an adapter probing the system, or a walk stepping over an entry it
      may not read. Never over a save, a measurement or a verification.
- [ ] Nothing fails silently. A failure reaches the caller as a value and the
      user as a sentence.
- [ ] A refusal is reported the same way wherever it is decided; two paths
      refusing the same thing differently is a defect.

## 6. Formatting

- [ ] Fantomas formats every `.fs` file, with the settings in the repository's
      `.editorconfig` and nothing else: its defaults, except the `cramped`
      bracket style (`{ Name = …` on the line of its first field, the closing
      bracket on the line of its last) and `lf` line endings. No per-file
      overrides, no `// fantomas` exclusions. The version is pinned in
      `.config/dotnet-tools.json`; `dotnet tool restore` installs it,
      `dotnet fantomas Marbas Marbas.Tests` formats, and the `format` job in CI
      runs the same with `--check` and fails on any file that would change.
- [ ] A commit that only reformats is listed in `.git-blame-ignore-revs`, so
      blame skips it. GitHub reads the file on its own; locally,
      `git config blame.ignoreRevsFile .git-blame-ignore-revs` once per clone.
- [ ] `TreatWarningsAsErrors` is on in every configuration: a warning is a build
      failure, not a note.
- [ ] Warning FS1182 is on (`--warnon:1182` in `Directory.Build.props`), so with
      the rule above an unused parameter, binding or pattern variable does not
      build. A name the code must keep — a signature an overridden member or a
      Terminal.Gui handler dictates — is spelled `_name`; anything else unused
      is removed, from the signature and from every caller.
- [ ] Lines stay under about 120 characters. Longer usually means a missing
      pipeline or a missing name.
- [ ] No trailing whitespace, no double blank lines.
- [ ] A file of functions declares `module Marbas.Layer.Name`. A file whose
      companion module shares a type's name declares `namespace Marbas.Layer`
      and nests `module Name =` under the type.
- [ ] `open` lines are grouped system-first, then `Marbas.*` innermost-out.

## 7. Comments and documentation

- [ ] No code comments. None. Names and tests carry the intent.
- [ ] What needs explaining goes into `_documentation/`:

| What | Where |
|------|-------|
| a design decision and its reasoning | `Architecture/<TOPIC>.md` |
| what a phase did | `Progress/PHASE-NN-*.md` |
| what the application does today | `Progress/WHAT-WORKS.md` |
| a known defect or awkwardness | `ROUGH-EDGES.md` |
| work waiting to be scheduled | the plan, as a task |

- [ ] A behaviour change that contradicts a document changes that document in
      the same task.

## 8. Tests

- [ ] Tests first. A task starts with a failing test.
- [ ] Logic modules take `IFileSystem`, so tests run on `MockFileSystem` and
      never touch the disk. `Integration/Disk/` is the last resort, for
      behaviour only the real system can answer.
- [ ] Arrange, act, assert — separated by blank lines, in that order, and no
      blank line *inside* a section, so the three blocks are visible at a
      glance. A section that is not needed is left out, not faked.
- [ ] The act is a line of its own and its result is bound to a name. It is
      never folded into the assertion: `let refused = wouldPackItself …` then
      `Assert.False refused`, not `Assert.False(wouldPackItself …)`.
- [ ] No `if`, no `for`, no `match` inside a test. Cases go into `[<Theory>]`
      with `[<InlineData>]`.
- [ ] A `[<Theory>]` does not take the expected answer as a `true`/`false`
      column. The affirming rows and the denying rows are two theories, each
      named for what it claims, and each asserting one way.
- [ ] One test module per module under test, mirroring the folder layout.
- [ ] No test file runs past a thousand lines, and most are a few hundred. One
      that would not fit is split by feature — `ShellDeleteTests`,
      `OperationsBatchTests` — and what the parts share moves into a fixture
      module compiled before them; a helper with one caller stays `private`
      beside it. [Testing](Architecture/TESTING.md#layout) has the folders.
- [ ] A value typed `| null` that a test needs — the source of a list, an
      entry of a zip, a type found by name — is unwrapped with
      `Expect.notNull`, as an option is with `Expect.some`, so a null fails
      the test with a sentence and never reaches the assertion.
- [ ] A fake overrides one field of the dependencies record, or subclasses
      `MockFileSystem`; the whole interface is never stubbed by hand.
- [ ] What a view remembers is tested on its record, with no view built: one
      test per transition in `Ui/`, beside the tests that press keys on the
      real view in `Integration/`.
- [ ] Paths in an arrange block are built with `Root.at`, so one suite runs on
      both platforms.
- [ ] Assertions name the behaviour, not the implementation: a test that breaks
      when a `list` becomes an `array` was testing the wrong thing.
- [ ] Anything about how the screen looks is verified by rendering the screen —
      the `render-tui-screen` probe — not by reading a view's properties.

## 9. UI and Terminal.Gui

- [ ] Terminal.Gui v2 instance API (`Application.Create()`); the static API is
      deprecated and not used.
- [ ] Views are built by a `create` function and read by a typed accessor; no
      reaching into `SubViews` by index at a call site. The accessor is one
      line over `SubView.byId<T> id parent`, or `SubView.byType<T> parent`
      for the one view of its type.
- [ ] Every command is reachable by a key without a mouse, and the key lives in
      `KeyMap` — one table, one row per binding.
- [ ] A key that a terminal or a desktop may swallow carries a second binding,
      and what was measured is a row in `Architecture/CROSS-PLATFORM.md`.
- [ ] Dialogs wear the classic scheme through `DialogStyle`; no view sets its
      own colours.
- [ ] A view that remembers anything between two events keeps it in one
      immutable record behind one `let mutable` — `ViewerState`,
      `ResultsState`, `SettingsForm`, `UserMenuState` — and every change is a
      function over that record in a module compiled before the view, which
      opens no `Terminal.Gui` namespace. A handler reads what the toolkit
      knows, asks the module for the next value and stores it; it decides
      nothing itself. The views that still keep two or three settable
      properties — `FavoritesDialog`, `HistoryDialog`, `PanelView` — are held
      to this when they are next changed.
- [ ] What a widget already holds — the text of a field, the cursor of a list,
      the collection a list shows — is read from the widget when it is needed
      and is not copied into a second place.
- [ ] Long work does not run on the UI thread: it is an effect behind
      `Runners.Batch`, with a progress window and `Esc`.

## 10. Performance

A hot path is one a user waits on, or one that runs for every key, every entry
or every byte. A change there is held to what the path cost before it.

| Hot path | What runs on it | Where it was timed |
|----------|-----------------|--------------------|
| a key in a panel | the transition in `Update`, the render of both panels, the status line and the bar | [the performance review](reports/2026-09-19-performance-review.md), P1, P10 and P14; Measurements, P22.11 |
| a directory listed and sorted | `DirectoryListing`, the sort, the rows of a panel | the same review, P6 and P12; Measurements, P22.2 |
| a file viewed, as text or as a dump | `FileWindow`, `TextEncoding`, `LineIndex`, `ViewerState`, the searches of `WindowSearch` and `ByteSearch` | [Measurements](Progress/MEASUREMENTS.md), P21.15 and P21.16 |
| a copy, a move, a delete, a wipe, a size measured | the walks of `Operations` and `DirectoryMeasure`, and what a batch reports | Measurements, P15.25 and P21.3 |
| a search by name or by text | `FileSearch`, `ContentMatch`, the results window, the results in a panel | Measurements, P21.14 and P21.16 |
| any of those inside an archive | `ArchiveReader`, `ArchiveIndex`, `ZipMoments`, `ZipEntries`, packing and unpacking in `Archive` | Measurements, P15.17, P21.14, P22.3, P22.4 and P22.5 |
| a refresh nobody asked for | the watch over the directories on screen, the hits of a results panel verified again | [Auto-refresh](Architecture/AUTO-REFRESH.md) |
| the first screen | the settings read, both panels' directories listed, the first watch, the sweep of old sessions | — |

- [ ] A change on a hot path does not make it slower, whatever the change was
      for. One made for tidiness is timed like one made for speed: a rewrite of
      a walk's bookkeeping was a tenth slower until it was measured.
- [ ] Timing is a probe built in Release against the code before and the code
      after, run alternately on the same data on a real disk, and its rows are
      an entry in [Measurements](Progress/MEASUREMENTS.md).
- [ ] A path that is slow is taken apart before it is changed — its parts timed
      one added to the next — so that the cause is a number and the fix is the
      smallest one the number points to.
- [ ] Nothing that calls into the system is asked once per line, per entry or
      per byte. The length of a stream, whether a path exists, the attributes
      of an entry are asked once per chunk or per directory and handed on. The
      length of a file opened for shared writing costs a call each time it is
      read.
- [ ] A loop over bytes makes no `Seq`, no slice of an array and no other
      allocation per byte or per pair. It is a recursion, or the loop §4 allows.
- [ ] Work is linear in the data. Nothing rebuilds the whole on every tick,
      appends to a list with `@` inside a loop, or walks a list for what a map
      or a set answers.
- [ ] A transition and a render cost what changed, not what a panel lists.
- [ ] A report of progress overwrites what the last one said; it never queues a
      call per file.
- [ ] Nothing that reads a disk for longer than a moment runs on the UI thread
      (§9), and nothing that starts with the application holds back the first
      screen.
- [ ] An archive is opened once for a walk, not once per entry, and an entry is
      streamed where its format allows; where one is buffered whole,
      [Archives](Architecture/ARCHIVES.md) says why.
- [ ] A cache earns its place by a measurement, and falls under §4.

## 11. Before handing work back

- [ ] `dotnet build Marbas.slnx` is clean and `dotnet test Marbas.slnx` is green.
- [ ] `dotnet fantomas --check Marbas Marbas.Tests` changes nothing.
- [ ] A change on a hot path (§10) was timed before and after, and the rows are
      in [Measurements](Progress/MEASUREMENTS.md).
- [ ] No comment, no primed alias, no boolean parameter, no new `with _ ->` was
      added.
- [ ] Every new name says what it answers, and every boundary is in the name.
- [ ] Every new failure is a value, mapped at its family's one point.
- [ ] The document that describes the changed behaviour was changed with it.
- [ ] `dotnet msbuild Marbas.slnx -t:AnalyzeFSharpProject` reports nothing
      ([Development](DEVELOPMENT.md#analyzers) has the setup).
- [ ] The user commits. The agent never runs `git commit`.
