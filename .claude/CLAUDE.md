# Marbas

An orthodox (two-panel) console file manager in F# on Terminal.Gui 2.

## Documentation

Read these before starting work — they carry the context this file omits:

- `_documentation/coding-style-conventions.md` — the style and conventions code
  and tests are held to; a rule there beats a habit found in an older file
- `_documentation/PLAN.md` — the index of the phases; one file per phase in
  `_documentation/Plan/` (P1.1 … PX.Y)
- `_documentation/PROGRESS.md` — current status and what is next; the journal
  of each phase is in `_documentation/Progress/`
- `_documentation/Architecture/` — design decisions, one topic per file
  (start at `OVERVIEW.md`); `TESTING.md` there covers Terminal.Gui testing
  pitfalls

## Build and test

```
dotnet build Marbas.slnx
dotnet test Marbas.slnx
dotnet tool restore
dotnet fantomas Marbas Marbas.Tests
dotnet msbuild Marbas.slnx -t:AnalyzeFSharpProject
```

CI fails on any file `dotnet fantomas --check Marbas Marbas.Tests` would change,
and on any finding of the analyzers.

## Working rules

- **Write everything in the repository in English.**
- **Tests first.** Start each task with failing tests. Logic modules take
  `IFileSystem`, so tests run on `MockFileSystem` and never touch the disk.
- **No code comments.** Names and tests carry the intent; anything that needs
  explaining goes into `_documentation/*.md`.
- **Dependencies flow downward only:** `Program` → `Adapters` → `Ui` →
  `State` → `Guide` → `Messages` → `Find` → `Viewing` → `Locations` → `Profile` →
  `Programs` → `FileSystem` → `Domain` → `Collections`, and a layer uses only
  the ones its row in §1 of `_documentation/coding-style-conventions.md`
  allows — `Layers/LayerReferenceTests` fails on any other use. A new module
  goes into the layer whose subject it extends; into `FileSystem` only when it
  is about entries, paths, operations or archives and more than one layer
  needs it; and into a new layer, with its row in that table and in the test,
  when it fits none. `AppState` transitions may read and
  write through the
  `IFileSystem` they are given and reach no other port, and what outlasts a
  keystroke is an effect instead; the generic
  `State/Runtime.fs` host holds the only mutable cell and drives the
  update/render/effect loop. The outside world (file system, dialogs, quit) is
  injected through `Ports.Dependencies` and run by `Shell.interpret`;
  `Adapters/` holds the real ones and `Adapters/Dependencies.fs` wires them,
  `Program.fs` only starts the application. Only `Adapters` may touch the
  operating system directly.
- **Mind the hot paths.** A key in a panel, a directory listed and sorted,
  the viewer, copying and moving, deleting, searching by name and by text — on
  disk and inside archives — must not get slower. Time a change there before
  and after, and never ask the system once per line, entry or byte. The paths
  and the rules are §10 of `_documentation/coding-style-conventions.md`.
- Work task by task, in plan order, keeping the application runnable after
  each one.
- When working with documentation, be very concise but accurate and factual.
  Use understandable english. **Do not** create "a [Concept Name] is [General Statement]"
  statements, instead specify exact solutions and technical reasoning behind them.
