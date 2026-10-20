# Packaging

P8.3. How a release binary is produced, what the publish profiles decide, and
why. Two profiles live in `Marbas/Properties/PublishProfiles/`:

| Profile | Runtime identifier | Output |
|---------|--------------------|--------|
| `win-x64` | `win-x64` | `Marbas/bin/publish/win-x64/Marbas.exe` |
| `linux-x64` | `linux-x64` | `Marbas/bin/publish/linux-x64/Marbas` |

```
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=win-x64
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=linux-x64
```

Either command answers with one file of roughly 50 MB that runs on a machine
with no .NET installed. Both can be produced from either operating system —
publishing `linux-x64` from Windows needs nothing but the SDK.

## What the profiles say, and why

**Self-contained, because a file manager is a tool you copy onto a machine.**
`SelfContained` plus `PublishSingleFile` means the .NET runtime travels inside
the executable, so there is nothing to install and nothing to match versions
with. The price is the size: a framework-dependent build would be about a
megabyte, and would refuse to start wherever .NET 10 is not already there.

**Compressed and extracted at first run.** `EnableCompressionInSingleFile`
roughly halves the file. `IncludeNativeLibrariesForSelfExtract` puts the native
libraries inside it as well, so the published directory holds exactly one entry;
they are unpacked to a cache directory the first time the binary runs, which
makes that run measurably slower and every later one normal.

**Not trimmed.** `PublishTrimmed` is off on purpose. Terminal.Gui resolves
drivers and views through reflection, and FSharp.Core carries reflection-based
formatting and equality; a trimmer has no way to see either, so a trimmed build
is the kind that starts and then fails on a code path nobody tried. The size it
would save is not worth a runtime failure in a released binary.

**Ready-to-run.** `PublishReadyToRun` compiles the IL ahead of time, which is
what keeps the startup of a self-extracting binary tolerable. It only adds to
the file, it never replaces the IL, so nothing about behaviour changes.

**Debug symbols are embedded.** `DebugType` is `embedded`, so a stack trace
still names files and lines and the publish directory still holds one file
rather than a binary and a stray `.pdb`.

**English resources only.** `SatelliteResourceLanguages` is `en`: the localised
resource assemblies of the dependencies would otherwise be bundled in, and
Marbas has one language.

Globalization is left on. Entry names are compared and sorted culture-aware, so
`InvariantGlobalization` would quietly change the order a panel lists its files
in — a saving of a few megabytes for a change in behaviour.

## The version

I1.1. No file holds a version. MinVer, a build-only package referenced by
`Marbas.fsproj`, computes it on every build from the nearest tag `v*` reachable
from the commit, so the version a binary carries and the tag it was built from
are the same by construction:

| Commit | Version |
|--------|---------|
| tagged `v0.1.0` | `0.1.0` |
| 3 commits after `v0.1.0` | `0.2.0-alpha.0.3` |
| 297 commits, no tag yet | `0.1.0-alpha.0.297` |
| shallow checkout, no tag in it | `0.1.0-alpha.0` |

`MinVerAutoIncrement` is `minor`, not MinVer's default `patch`, because the
release after a tag is the next phase. `MinVerMinimumMajorMinor` `0.1` sets the
floor before the first tag. The SDK appends the commit to the informational
version (`0.1.0+d1c143e…`); the file version is `0.1.0.0`. `-t:MinVer
-getProperty:MinVerVersion` prints the version without building, which is what
`release.yml` names the archives by. A shallow checkout builds without a
warning, but only `release.yml` fetches the whole history; the binaries of
`ci.yml` are never shipped.

## Releasing

`.github/workflows/release.yml` runs on a tag matching `v*`. It publishes both
profiles, each on its own runner — `windows-latest` for `win-x64`,
`ubuntu-latest` for `linux-x64`, so a cross-published binary is never what ships
— archives each one (`.zip` on Windows, `.tar.gz` on Linux, which is what keeps
the executable bit) and creates the GitHub release with both archives attached
and generated notes.

```
git tag v0.1.0
git push origin v0.1.0
```

`workflow_dispatch` runs the same publish steps under the version MinVer gives
the commit and stops after uploading the workflow artifacts, so the packaging
can be exercised without releasing anything.

## Checking a published binary

`dotnet publish` succeeding is not the same as the binary running: a
self-contained build fails at startup, if it fails at all. Both were run.

- **Windows.** `Marbas.exe` was started from its publish directory in its own
  console and was still running when it was killed a few seconds later — which
  says the self-extraction and the runtime are sound, and says nothing about the
  screen. What it draws is the *manual* column of
  [CROSS-PLATFORM.md](CROSS-PLATFORM.md), a person at a terminal.
- **Linux.** The `linux-x64` binary was copied into WSL (Ubuntu 24.04), given
  the executable bit and run in a pseudo-terminal by the recipe in
  [CROSS-PLATFORM.md](CROSS-PLATFORM.md): it drew both panels over a real
  directory, `F10` quit it and the session file appeared in
  `~/.config/Marbas/settings.conf` — which is the cheapest proof that a key
  reached the application.

Run both again after anything that touches the profiles, the runtime version or
a native dependency. Nothing here is covered by `dotnet test`, because what is
being tested is the build itself.

## A trap already hit

The stock Visual Studio `.gitignore` ignores `*.pubxml`, because a *web* deploy
profile can hold a connection string with a password in it. A publish profile
for a console application holds nothing of the sort, so both files would have
been silently left out of the commit; `.gitignore` now un-ignores
`Marbas/Properties/PublishProfiles/*.pubxml`. `git status` is what says so —
a new file that does not appear there is ignored, not missed.
