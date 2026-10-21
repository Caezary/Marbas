# Marbas

[![build](https://github.com/Caezary/Marbas/actions/workflows/ci.yml/badge.svg)](https://github.com/Caezary/Marbas/actions/workflows/ci.yml)

A two-panel file manager for the terminal, driven by the keyboard, in the
spirit of the orthodox managers of the DOS era. Written in F# on
Terminal.Gui 2, for Windows and Linux.

## What it does

- **Panels and tabs** — sorting, hidden entries, panels that follow the disk,
  the session restored on the next start
- **File operations** — copy, move, rename, delete to the trash or for good,
  wipe, links and junctions, in the background
- **Marking** — by hand, by page, by wildcard, by extension or name, inverted
- **Archives** — zip, tar, tar.gz, tar.bz2, tar.xz, 7z and rar browsed like
  directories; zip, tar.gz and 7z created and changed in place
- **Viewer** — text in a chosen code page or a hex dump, searched by text,
  regular expression or bytes
- **Finding files** — by name and content, on disk and inside archives
- **Running files** — associations, a program of your choice, your editor, a
  menu of your own commands
- **History and favourites**, and the **clipboard** on Windows

What changed in each release is in the [changelog](CHANGELOG.md).

## Getting Marbas

Prebuilt, self-contained binaries for Windows and Linux are attached to every
[release](https://github.com/Caezary/Marbas/releases) — no .NET installation
needed. See the [user guide](docs/user-guide/README.md) for how to run it
and its keyboard shortcuts.

## Documentation

- [User guide](docs/user-guide/README.md) — running Marbas, keyboard shortcuts
- [Development](_documentation/DEVELOPMENT.md) — building from source, testing, architecture docs
