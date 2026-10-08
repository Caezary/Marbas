# Coding style and conventions

The style Marbas holds itself to. It is the target, not a description of every
line that exists today: where the code still differs, the reviews of 2026-09-19
in [reports/](reports) name the place and [phase 15](Plan/PHASE-15-ANSWERING-THE-REVIEWS.md)
schedules the fix. A rule here beats a habit found in an older file.

## 1. Layers

| Layer | Holds | May use |
|-------|-------|---------|
| `Collections/` | `Zipper` and the like — no domain knowledge | nothing |
| `Domain/` | the vocabulary: `EntryName`, `FileSystemPath`, `NamePattern`, `SortOrder`, `HiddenEntries`, `FindQuery` | `Collections` |
| `FileSystem/` | reading and writing disks and archives, every call through `IFileSystem` | `Domain` |
| `State/` | `AppState`, messages, transitions, `Effect`; `Runtime` holds the one mutable cell | `FileSystem`, `Domain` |
| `Ui/` | views, key maps, `Ports.Dependencies` as ports, `Shell.interpret` | `State` and below |
| `Adapters/` | the real ports: dialogs, processes, registry, watches, runners | everything above it |
| `Program.fs` | starts the application, nothing else | `Adapters` |

- [ ] Dependencies point downward only. The compile order in `Marbas.fsproj` is
      the enforcement; if a reference needs to point up, the type is in the
      wrong layer.
- [ ] Only `Adapters` touches the operating system directly. Everything else
      reaches it through `IFileSystem` or a port on `Ports.Dependencies`.
- [ ] A type belongs to the lowest layer that speaks it, not the one that needed
      it first.
- [ ] One folder is one namespace, and a folder may only depend on folders above
      it in the compile order.
- [ ] `AppState` is immutable. A transition answers a new state; handlers never
      mutate.

## 2. Names

| Write | Not | Why |
|-------|-----|-----|
| `strictlyUnder`, `atOrUnder` | `below`, `under` | a preposition alone hides whether the boundary is strict |
| `segmentsFromTargetName` | `beneath` | say what is compared, not where it sits |
| `moveFromDisk`, `moveToArchive` | `moveIn`, `moveAcross` | a direction names both ends |
| `refuseUnchangeable` | `guarding` | a gate is named for what it refuses |
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
- [ ] Computation-expression cut-off: three binds, or one nested match.
      FsToolkit.ErrorHandling is already referenced — `result`, `option`,
      `Result.requireTrue` and friends are in scope.
      [Flat decisions](DEVELOPMENT.md#flat-decisions) works the rules through
      the sites they were first applied to, and says when a tuple match is the
      wrong answer.
- [ ] Mutation is allowed inside a walk that needs an early exit, and nowhere
      else; even there it is threaded, not scattered.
- [ ] A function that grows past one screen has lost a seam — lift the inner
      walk out with explicit inputs and an explicit result.
- [ ] Partial access (`.Value`, `List.head`, `Seq.pick`) only where the type
      cannot express the guarantee, and then next to the code that established
      it.

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
- [ ] A fake overrides one field of the dependencies record, or subclasses
      `MockFileSystem`; the whole interface is never stubbed by hand.
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
- [ ] Long work does not run on the UI thread: it is an effect behind
      `Runners.Batch`, with a progress window and `Esc`.

## 10. Before handing work back

- [ ] `dotnet build Marbas.slnx` is clean and `dotnet test Marbas.slnx` is green.
- [ ] `dotnet fantomas --check Marbas Marbas.Tests` changes nothing.
- [ ] No comment, no primed alias, no boolean parameter, no new `with _ ->` was
      added.
- [ ] Every new name says what it answers, and every boundary is in the name.
- [ ] Every new failure is a value, mapped at its family's one point.
- [ ] The document that describes the changed behaviour was changed with it.
- [ ] The user commits. The agent never runs `git commit`.
