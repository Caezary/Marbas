# Contributing

Bug reports, ideas and pull requests are welcome. Marbas follows
[a plan](_documentation/PLAN.md) of numbered phases, so a change that fits
neither the plan nor a reported bug may be declined; ask in an issue first and
no work is lost.

## Reporting a bug

Use the *Bug report* form. Most problems in a terminal application depend on
the terminal, so the form asks for it, along with the version
(`Marbas --version`) and the system. Marbas writes crashes to `marbas.log`, in
`%APPDATA%\Marbas` on Windows and `~/.config/Marbas` on Linux; `--debug`
makes it record every command of a session, which shows what a key did.

Check [Rough edges](_documentation/ROUGH-EDGES.md) first: it lists what is
already known to be wrong, with the reason it stands.

A security problem goes to a private advisory, not to an issue; see
[Security](SECURITY.md).

## Proposing a change

Open an issue before a pull request for anything larger than a typo or an
obvious one-line fix. Say what the change is for, and which keys, dialogs or
chapters of the [user guide](docs/user-guide/README.md) it touches.

## Building and testing

The .NET 10 SDK is the only requirement:

```
dotnet build Marbas.slnx
dotnet test Marbas.slnx
dotnet tool restore
dotnet fantomas Marbas Marbas.Tests
dotnet msbuild Marbas.slnx -t:AnalyzeFSharpProject
```

The tests run against an in-memory file system and never touch the disk.
[Development](_documentation/DEVELOPMENT.md) covers the build configuration,
the analyzers and the probes; [Architecture](_documentation/Architecture/OVERVIEW.md)
covers the design.

## What a pull request is held to

The rules are in [Coding style](_documentation/coding-style-conventions.md);
the ones most often missed:

- **Tests first.** A change starts with a failing test. Tests use
  arrange/act/assert with no `if`, `for` or `match` inside; cases go into
  `[<Theory>]` (§8).
- **No code comments.** Names and tests carry the intent; an explanation goes
  into `_documentation/` (§7).
- **Layers.** Dependencies flow downward only, as the table in §1 allows;
  `LayerReferenceTests` fails on any other use. Only `Adapters/` touches the
  operating system.
- **Hot paths.** A change to panel keys, listing, the viewer, copying, deleting
  or searching is timed before and after (§10).
- **The user guide.** A change the user can see changes its chapter in
  `docs/user-guide/` in the same pull request.
- **English** in code, tests, documentation and commit messages.

CI fails on a formatting difference, on any analyzer finding and on a failing
test, on Windows and on Linux.

## Pull requests

- One change per pull request, branched from `main`.
- Describe what was verified and how; for anything on screen, how it was
  looked at.
- Pull requests are squash-merged, so the title becomes the commit message.

Contributions are accepted under the [MIT License](LICENSE) of the project.
