# Interlude 1 — The first release

The tasks are in
[Interlude 1 — The first release](../Plan/INTERLUDE-01-THE-FIRST-RELEASE.md).

**I1.1 the version comes from the git tags.** MinVer 8.0.0 is a build-only
reference of `Marbas.fsproj` (`PrivateAssets="all"`), with `MinVerTagPrefix`
`v`, `MinVerMinimumMajorMinor` `0.1` and `MinVerAutoIncrement` `minor`;
`Version` left `Directory.Build.props`. The test project takes no version and
so no reference. `release.yml` fetches the whole history, reads the version with
`dotnet msbuild Marbas/Marbas.fsproj -restore -t:MinVer
-getProperty:MinVerVersion` to name the archives, and no longer passes
`-p:Version`.

Checked on 2026-10-20, the tags made only in throwaway clones:

| Checkout | Version |
|----------|---------|
| `main`, 297 commits, no tag | `0.1.0-alpha.0.297` |
| `v0.1.0` on `HEAD` | `0.1.0`; the published `Marbas.exe` reads `0.1.0+d1c143e…`, file version `0.1.0.0` |
| `v0.1.0` on `HEAD~1` | `0.2.0-alpha.0.1` |
| `--depth 1` | `0.1.0-alpha.0`, built with no warning under `TreatWarningsAsErrors` |

One thing went differently from the plan: only `release.yml` fetches the whole
history. `ci.yml` builds nothing that ships, and its checkouts stay shallow.

**I1.2 the binary names its version.** `ProductVersion` in `Domain` wraps the
version; `VersionMessage.product` words it as `Marbas 0.1.0`.
`HostEnvironment.productVersion` reads it from the
`AssemblyInformationalVersionAttribute` of the Marbas assembly, and
`Launch.ofArguments` answers `PrintVersion` for `--version`, which
`Session.run` prints and exits on with code 0 before the log is cut or a hook
subscribed. `Crashed` carries the version, so the crash line in the log reads
`crash on the UI thread in Marbas 0.1.0: …`; `Crashes.guarded`, `onUnhandled`,
`onUnobserved` and `watch` take it first, and both lines of `CrashMessage`
start `Marbas 0.1.0 stopped …`. Nine tests are new — the version worded, the
switch present and absent, the version read from the assembly — and the
crash tests of `LogEntry`, `Crashes` and `CrashMessage` expect the version.

From a throwaway clone tagged `v0.1.0`, the published `win-x64` binary and the
`linux-x64` one run under WSL both print `Marbas 0.1.0` and exit with 0; a
build of `main` prints `Marbas 0.1.0-alpha.0.298`.

One thing went differently from the plan:
`IncludeSourceRevisionInInformationalVersion` is now false in `Marbas.fsproj`.
The SDK appended the commit to the informational version (`0.1.0+d1c143e…`,
as I1.1 recorded), which `--version` would have printed; a release's commit is
its tag, so the version alone is enough.

**I1.3 the release waits for CI and checks its tag.** `ci.yml` also answers
`workflow_call`. `release.yml` runs `check`, then `build` (`ci.yml`), then
`publish`, then `release`; `release` passes `--prerelease` when `check` says
the tag has a pre-release part. `.github/scripts/check-release.sh` holds the
three refusals, and `.github/scripts/release-notes.sh`, which prints the
section of one version of `CHANGELOG.md`, answers the third; I1.4 makes it the
release's description. `.gitattributes` keeps `*.sh` at LF.

Run in a throwaway clone on 2026-10-20:

| Tag | Answer |
|-----|--------|
| `0.1.0`, `v0.1`, `v01.0.0`, `v0.1.0+meta`, `v0.1.0-` | not `v<major>.<minor>.<patch>[-<pre-release>]` |
| `v0.1.0`, no `CHANGELOG.md` | `CHANGELOG.md not found.` |
| `v0.1.0`, only a section `0.2.0` | `CHANGELOG.md has no section '## 0.1.0'.` |
| `v0.1.1` on a commit made with `git commit-tree` beside `main` | not on main |
| `v0.1.0` with its section | passes, `prerelease=false` |
| `v0.2.0-rc.1` with its section | passes, `prerelease=true` |

`release-notes.sh` printed a section that had two blank lines after its heading
and before the next one without either, under gawk on Windows and under WSL;
no `mawk` was at hand, and the program uses only POSIX awk.

Two things went differently from the plan. `build` needs `check`, so a refused
tag runs nothing else. The `workflow_dispatch` run that proves the workflow on
GitHub waits for these files to reach `main`; it is the first step of I1.5.

**I1.4 the notes of 0.1.0.** `CHANGELOG.md` holds the section of `0.1.0`: what
Marbas is, what it does in nine points, how to run it with the two archives,
and its known limits, pointing at the user guide and Rough edges by absolute
links. The README's *Goal* and *Planned core features* — which still promised
macOS and called copying and deleting planned — gave way to the same summary
and a link to the changelog.

`release-notes.sh` now takes the tag, not the version, and adds a link
comparing the tag with the previous release that has no pre-release part.
`release.yml`'s `release` job checks out the history, writes the notes to
`$RUNNER_TEMP/notes.md` and passes them with `--notes-file` instead of
`--generate-notes`.

In a throwaway clone the notes of `v0.1.0` were byte for byte the section of
`CHANGELOG.md` below its heading, with no link; with tags `v0.1.0`,
`v0.2.0-rc.1` and `v0.2.0` on three commits of `main`, both later tags were
compared with `v0.1.0`.

The date in the heading is the day the section was written; it is set to the
day of the tag when I1.5 tags it.
