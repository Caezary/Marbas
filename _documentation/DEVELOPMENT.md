# Development

Requires the .NET 10 SDK.

```
dotnet build Marbas.slnx
dotnet run --project Marbas
dotnet test Marbas.slnx
```

The tests run against an in-memory file system and never touch the disk, so
`dotnet test` is safe to run anywhere. Coverage is collected with

```
dotnet test Marbas.slnx --settings coverlet.runsettings
```

## Build configuration

`Directory.Build.props` in the repository root holds what both projects share —
the target framework, the product name, the version and
`TreatWarningsAsErrors`, which is on in every configuration.
`Directory.Packages.props` holds the package versions: a new package needs a
`PackageVersion` there and a bare `PackageReference Include="..."`, without a
version, in the project that uses it.

## Publishing a binary

```
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=win-x64
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=linux-x64
```

Each profile writes one self-contained, single-file executable to
`Marbas/bin/publish/<runtime>/` — the .NET runtime travels inside it, so the
machine it is copied onto needs nothing installed. Either can be built from
either operating system. Tagging `v*` builds both on GitHub Actions and
attaches them to a release; see [Packaging](Architecture/PACKAGING.md).

## Probes

Two things the suite cannot reach are measured with throwaway programs instead
of asserted.

**Measuring what a terminal delivers.** `_probes/KeyProbe` prints what a
terminal really hands to Terminal.Gui for a key, which is not always what the
binding says it should be. Run it in every terminal that matters and compare;
the rows marked *probe* in [Cross-platform](Architecture/CROSS-PLATFORM.md) come
from it.

A real Linux terminal can be reached from a Windows machine without a Linux
machine: publish the probe as `linux-x64`, self-contained and single-file, and
run it under WSLg in a terminal installed inside the distribution
(`gnome-terminal` is VTE, which is what GNOME and Xfce use). Two things bite.
The distribution shuts itself down once nothing is attached to it, taking the
window and `/tmp` with it, so the probe is started from a `wsl.exe` command that
stays attached — `gnome-terminal --wait` — and writes its answer under `/mnt/c`
rather than into `/tmp`. And WSLg runs no window manager at all, so it measures
what the terminal encodes and can say nothing about a shortcut a desktop grabs
first; that half is read out of the desktop's own configuration instead, which
is the *desktop defaults* evidence in the checklist.

**Driving the real window over a real file system.** The `render-tui-screen`
skill's probe template takes a `ProjectReference` on `Marbas.fsproj`, forces a
screen size by reflection and reads `app.Driver.ToString()` back from inside a
timeout, so a scene can build `MainWindow`, change the disk behind its back, type
a key and print the screen. That is how the auto-refresh rows of the checklist
were measured — a file written from outside, the directory a panel stands in
deleted, a `subst` drive pulled out, a burst of two hundred files, and a prompt
held open while the disk changes. Build it in the scratchpad, never in the tree,
and give it `FSharp.Core` explicitly; see
[Auto-refresh](Architecture/AUTO-REFRESH.md) and the note on explicit
dependencies in [the overview](Architecture/OVERVIEW.md).

Under WSL the same scenes need a pseudo-terminal and an answer to the queries
Terminal.Gui sends on startup, exactly as the WSL rows of the checklist describe:

```sh
script -qec "stty rows 24 cols 100; dotnet run --no-build -- vanish" /dev/null \
  < <(sleep 2; printf '\033[8;24;100t\033[24;100R\033[?1;2c'; sleep 40) > vanish.log
```

Keep the build outputs apart from the Windows ones with
`-p:ArtifactsPath=/tmp/marbas`, and strip the escapes from the log with
`sed -r 's/\x1b\[[0-9;?]*[a-zA-Z]//g'`.

## Working rules

- Talk to the user in Polish; write everything in the repository in English.
- Tests first — logic modules take `IFileSystem`, so tests run on
  `MockFileSystem` and never touch the disk.
- No code comments — names and tests carry the intent; anything that needs
  explaining goes into `_documentation/*.md`.
- Dependencies flow downward only: `Ui` → `State` → `FileSystem` → `Domain`.

## Documentation map

- [Implementation plan](PLAN.md) — the index of the phases, one file per phase
  in [Plan/](Plan) (P1.1 … P15.23)
- [Progress](PROGRESS.md) — current status, with the journal of each phase in
  [Progress/](Progress) and the catalogue in
  [What works today](Progress/WHAT-WORKS.md)
- [Rough edges](ROUGH-EDGES.md) — what is known to be wrong or missing
- [Architecture](Architecture/OVERVIEW.md) — design decisions, by topic
  - [Domain types](Architecture/DOMAIN-TYPES.md)
  - [State and effects](Architecture/STATE-AND-EFFECTS.md)
  - [File operations](Architecture/FILE-OPERATIONS.md)
  - [UI and input](Architecture/UI-AND-INPUT.md)
  - [Settings persistence](Architecture/SETTINGS-PERSISTENCE.md)
  - [Archives](Architecture/ARCHIVES.md)
  - [Tabs](Architecture/TABS.md)
  - [Auto-refresh](Architecture/AUTO-REFRESH.md)
  - [Testing](Architecture/TESTING.md)
  - [Packaging](Architecture/PACKAGING.md)
  - [Cross-platform](Architecture/CROSS-PLATFORM.md)
- [User guide](USER-GUIDE.md) — running Marbas and its keyboard shortcuts
