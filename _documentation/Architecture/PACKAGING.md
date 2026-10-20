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
floor before the first tag. `IncludeSourceRevisionInInformationalVersion` is
false, so the SDK does not append the commit (`0.1.0+d1c143e…`) and the
informational version is the version alone, which is what `Marbas --version`
prints; the file version is `0.1.0.0`. `-t:MinVer
-getProperty:MinVerVersion` prints the version without building, which is what
`release.yml` names the archives by. A shallow checkout builds without a
warning, but only `release.yml` fetches the whole history; the binaries of
`ci.yml` are never shipped.

## Releasing

`.github/workflows/release.yml` runs on a tag matching `v*`, in four jobs:

| Job | Needs | What it does |
|-----|-------|--------------|
| `check` | — | on a tag, `.github/scripts/check-release.sh` refuses a tag that is not `v<major>.<minor>.<patch>[-<pre-release>]`, one whose commit is not reachable from `origin/main`, and one whose version has no section `## <version>` in `CHANGELOG.md`; it answers whether the tag has a pre-release part |
| `build` | `check` | `ci.yml`, called as a reusable workflow (`workflow_call`): the format check, the analyzers, the build and the suite on both systems |
| `publish` | `check`, `build` | both profiles, archived |
| `release` | `check`, `publish` | on a tag only, the GitHub release with both archives and the notes of `release-notes.sh` as its description; `--prerelease` when the tag has a pre-release part (`v0.2.0-rc.1`) |

A tag on a commit whose suite fails therefore releases nothing, and a refused
tag runs nothing past `check`. Both scripts are bash with POSIX awk, run as
`bash <script>` so neither needs the executable bit, and `.gitattributes` keeps
`*.sh` at LF on a Windows checkout. Run locally,
`bash .github/scripts/check-release.sh v0.1.0` asks the same questions of a
local tag, and `bash .github/scripts/release-notes.sh v0.1.0` prints the
description the release would get.

`publish` builds both profiles, each on its own runner — `windows-latest` for
`win-x64`, `ubuntu-latest` for `linux-x64`, so a cross-published binary is never
what ships — and archives each one (`.zip` on Windows, `.tar.gz` on Linux, which
is what keeps the executable bit).

`workflow_dispatch` runs `check` without the tag step, `build` and `publish`
under the version MinVer gives the commit, and stops after uploading the
workflow artifacts, so the packaging can be exercised without releasing
anything.

## The notes

I1.4. The description of a release is written by hand in `CHANGELOG.md`, in the
commit the tag points to; the commit subjects are not written for a reader of
the releases page, and `--generate-notes` lists merged pull requests, which
this repository has none of. A section starts with `## <version> — <date>`;
`release-notes.sh <tag>` takes the second word of each `## ` line as the
version, prints the lines up to the next `## ` without the heading and without
the blank lines around them, and fails when no heading matches.

From the second release on it adds a link comparing the tag with the previous
release, found by `git describe --tags --abbrev=0 --match 'v*' --exclude 'v*-*'`
from the tag's parent, so a pre-release is never what a release is compared
with. A link in a section is absolute (`https://github.com/Caezary/Marbas/blob/main/…`):
GitHub resolves a relative one against the releases page, where it leads
nowhere.

## Numbering

I1.5. Semver, read from the side of the user, since Marbas is an application
and no one compiles against it:

| Part | Raised when |
|------|-------------|
| major | a settings, history, favourites or user menu file an older version wrote no longer reads, or a key binding is taken away |
| minor | a phase is finished |
| patch | a release holds fixes only |

The first release is `0.1.0`. The major part stays `0` while phases 25 to 29
may still change the formats; `1.0.0` is the promise that they no longer will.
A pre-release (`v0.2.0-rc.1`) is for a build someone should try before the
release; it is optional.

## Making a release

One release closes each phase, after the manual pass; a fix worth shipping
between two phases is a patch release made the same way. A release is never
made on every push: the manual pass is a person at a terminal, which no
workflow runs, and tools that tag on their own — semantic-release,
release-please — read Conventional Commits, which the commit subjects here are
not. Releases are made from `main` only, which `check` holds.

1. The last commit of the phase adds the section `## <version> — <date>` to
   `CHANGELOG.md`, the date being the day it will be tagged, and is pushed.
2. `release.yml` is run on `main` by hand, and its archives are downloaded:

   ```
   gh workflow run release.yml --ref main
   gh run download <run id> --dir <scratch directory>
   ```

3. The [manual pass](MANUAL-PASS.md) is walked on those binaries — the steps
   touched since the last pass, at the least. They carry
   `<version>-alpha.0.<n>` and are otherwise the binaries the tag will build:
   the same commit and the same workflow.
4. The tag goes on that commit:

   ```
   git tag v0.1.0
   git push origin v0.1.0
   ```

5. The release page holds both archives and the section as its description,
   and each binary answers `--version` with the version of the tag.

A tag refused by `check` or failed by `build` is deleted
(`git push origin :refs/tags/v0.1.0`, `git tag -d v0.1.0`), the commit fixed,
and the tag made again.

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
