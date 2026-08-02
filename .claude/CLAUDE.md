# Marbas

A Norton Commander style console file manager in F# on Terminal.Gui 2.

## Documentation

Read these before starting work — they carry the context this file omits:

- `_documentation/PROGRESS.md` — what is done, key decisions, traps already hit
- `_documentation/PLAN.md` — the task breakdown (P1.1 … P8.3) and architecture
- `_documentation/TESTING.md` — Terminal.Gui testing pitfalls

## Build and test

```
dotnet build Marbas.slnx
dotnet test Marbas.slnx
```

## Working rules

- **Talk to the user in Polish; write everything in the repository in English.**
- **Tests first.** Start each task with failing tests. Logic modules take
  `IFileSystem`, so tests run on `MockFileSystem` and never touch the disk.
- **No code comments.** Names and tests carry the intent; anything that needs
  explaining goes into `_documentation/*.md`.
- **The user commits manually.** Never run `git commit`.
- **Dependencies flow downward only:** `Ui` → `State` → `FileSystem`.
  `AppState` transitions are pure; the generic `State/Runtime.fs` host holds
  the only mutable cell and drives the update/render/effect loop. The outside
  world (file system, dialogs, quit) is injected through
  `Shell.Dependencies` and run by `Shell.interpret`; `Program.fs` is the only
  place wiring the real ones.
- Work task by task, in plan order, keeping the application runnable after
  each one.
