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
