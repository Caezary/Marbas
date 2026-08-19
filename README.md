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

## Getting Marbas

Prebuilt, self-contained binaries for Windows and Linux are attached to every
[release](https://github.com/Caezary/Marbas/releases) — no .NET installation
needed. See the [user guide](_documentation/USER-GUIDE.md) for how to run it
and its keyboard shortcuts.

## Documentation

- [User guide](_documentation/USER-GUIDE.md) — running Marbas, keyboard shortcuts
- [Development](_documentation/DEVELOPMENT.md) — building from source, testing, architecture docs
