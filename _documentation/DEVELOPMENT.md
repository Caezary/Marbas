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

## Working rules

- Talk to the user in Polish; write everything in the repository in English.
- Tests first — logic modules take `IFileSystem`, so tests run on
  `MockFileSystem` and never touch the disk.
- No code comments — names and tests carry the intent; anything that needs
  explaining goes into `_documentation/*.md`.
- Dependencies flow downward only: `Ui` → `State` → `FileSystem`.

## Documentation map

- [Implementation plan](PLAN.md) — the task breakdown (P1.1 … P8.3)
- [Progress](PROGRESS.md) — current status, what works today, what's next
- [Architecture](Architecture/OVERVIEW.md) — design decisions, by topic
  - [Domain types](Architecture/DOMAIN-TYPES.md)
  - [State and effects](Architecture/STATE-AND-EFFECTS.md)
  - [File operations](Architecture/FILE-OPERATIONS.md)
  - [UI and input](Architecture/UI-AND-INPUT.md)
  - [Settings persistence](Architecture/SETTINGS-PERSISTENCE.md)
  - [Tabs](Architecture/TABS.md)
  - [Testing](Architecture/TESTING.md)
  - [Packaging](Architecture/PACKAGING.md)
  - [Cross-platform](Architecture/CROSS-PLATFORM.md)
- [User guide](USER-GUIDE.md) — running Marbas and its keyboard shortcuts
