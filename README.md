# Marbas

[![build](https://github.com/Caezary/Marbas/actions/workflows/ci.yml/badge.svg)](https://github.com/Caezary/Marbas/actions/workflows/ci.yml)

A console file manager written in F#, inspired by the classic two-panel
managers of the DOS era.

## Goal

Marbas presents a full-screen terminal UI split into two side-by-side panels.
Each panel shows the file listing of its current directory, and the user can
browse, navigate and manage files using the keyboard — in the spirit of the
orthodox file managers.

Planned core features:

- Two independent directory panels displayed side by side
- Keyboard-driven navigation (arrows, Tab to switch panels, Enter to open)
- Basic file operations: copy, move, rename, delete, create directory
- Cross-platform: Windows, Linux, macOS (.NET)

## Building and running

Requires the .NET 10 SDK.

```
dotnet build Marbas.slnx
dotnet run --project Marbas
dotnet test Marbas.slnx
```

The tests run against an in-memory file system and never touch the disk, so
`dotnet test` is safe to run anywhere. Coverage is collected with
`dotnet test Marbas.slnx --settings coverlet.runsettings`.

## Documentation

- [Progress](_documentation/PROGRESS.md)
- [Implementation plan](_documentation/PLAN.md)
- [Testing notes](_documentation/TESTING.md)
- [Cross-platform checklist](_documentation/CROSS-PLATFORM.md)
