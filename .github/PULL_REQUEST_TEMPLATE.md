## What and why

<!-- What the change does, and the issue it answers: Closes #... -->

## How it was verified

<!-- Tests added, platforms run on, how anything on screen was looked at, timings for a hot path. -->

## Checklist

- [ ] The change started with a failing test.
- [ ] `dotnet test Marbas.slnx` passes.
- [ ] `dotnet fantomas --check Marbas Marbas.Tests` changes nothing.
- [ ] `dotnet msbuild Marbas.slnx -t:AnalyzeFSharpProject` reports nothing.
- [ ] No code comments were added.
- [ ] A change the user can see changes its chapter in `docs/user-guide/`.
- [ ] A change on a hot path was timed before and after.
