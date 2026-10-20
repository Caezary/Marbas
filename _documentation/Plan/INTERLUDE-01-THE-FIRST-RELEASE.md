# Interlude 1 — The first release

Goal: `v0.1.0` is on the releases page with both binaries and a description a
reader can act on, and every later release is one tag away. The interlude sits
between phases 23 and 24 and adds no feature to the panels. Its tasks are
numbered `I1.<task>` and titled as the tasks of phase 24 are.

`release.yml` already publishes both profiles on a tag `v*` and attaches them to
a release (P8.3). What it lacks: it ships a commit whose tests fail, its notes
are empty because `--generate-notes` lists merged pull requests and every
change lands on `main` directly, the version in `Directory.Build.props` is
`0.1.0` in every build whatever the commit, and the binary cannot say which
version it is.

Four decisions run through the interlude.

**A release is a tag, made by hand.** One release closes each phase, after the
[manual pass](../Architecture/MANUAL-PASS.md) on the published binaries; a fix
worth shipping between two phases is a patch release. A release on every push
would skip the manual pass, which no workflow can run. Tools that tag on their
own — semantic-release, release-please — read Conventional Commits, which the
commit subjects here are not.

**Semver, read from the user's side.** Major: a settings, history, favourites
or user menu file an older version wrote no longer reads, or a key binding is
taken away. Minor: a phase. Patch: fixes only. The numbering starts at `0.1.0`
and stays below `1.0.0` while phases 24 to 28 may still change the formats.

**The version comes from the tag.** MinVer computes it at build time from the
nearest tag `v*`: `0.1.0` on the tag itself, `0.2.0-alpha.0.<commits since>`
after it, with the minor part raised because the next release is a phase. No
file holds a version to forget, and `-p:Version` leaves the workflow.

**The notes are written, not generated.** `CHANGELOG.md` holds one section per
version, written by hand in the commit the tag points to; the workflow takes
the section of the tag's version as the release description and refuses a tag
without one. The commit subjects are not good enough to stand as notes.

- **I1.1 Take the version from the git tags** — MinVer in
  `Directory.Packages.props` and `Marbas.fsproj` as a build-only reference,
  `MinVerTagPrefix` `v`, `MinVerMinimumMajorMinor` `0.1`,
  `MinVerAutoIncrement` `minor`; `Version` leaves `Directory.Build.props`; the
  checkout of `release.yml` fetches the whole history. *Done when:* a build of
  `main` reports `0.1.0-alpha.0.<height>`, a build of a local tag `v0.1.0`
  reports `0.1.0`, and a shallow checkout builds without a warning.
- **I1.2 Answer --version and name the version on a crash** —
  `Marbas --version` prints `Marbas <version>` and exits without opening the
  panels; the crash entry in the log and the crash message on the standard
  error name the version. The version is read once from the entry assembly in
  `Adapters`. *Done when:* the arguments, the crash entry and both crash
  messages are proved by tests, and the published binary prints its version.
- **I1.3 Gate the release on CI and on main** — `ci.yml` also runs as
  `workflow_call`, and `release.yml` needs it before publishing; a first job
  refuses a tag that is not `v<semver>`, whose commit is not on `main`, or
  whose version has no section in `CHANGELOG.md`. A tag with a pre-release
  part (`v0.2.0-rc.1`) makes a pre-release. *Done when:* a `workflow_dispatch`
  run passes the checks and publishes both archives, and each refusal is shown
  by running the check script locally against a tag it refuses.
- **I1.4 Write the notes of 0.1.0** — `CHANGELOG.md` with the section of
  `0.1.0`: what Marbas is, what it does, what it runs on, how to start it,
  where its known limits are; short enough to read on the releases page. The
  release takes it as its description, with a link to the comparison against
  the previous tag from the second release on. The README's *Planned core
  features* gives way to the same summary. *Done when:* the section is
  reviewed and the script that extracts it, run locally, prints exactly it.
- **I1.5 Release 0.1.0** — [Packaging](../Architecture/PACKAGING.md) and
  [Development](../DEVELOPMENT.md) describe the version from tags, the steps of
  a release and the semver policy above; the manual pass is walked on the
  binaries a `workflow_dispatch` run publishes; the user tags `v0.1.0` and
  pushes it. *Done when:* the release page holds both archives and the notes,
  and each archive's binary prints `Marbas 0.1.0`.

Every task updates [What works today](../Progress/WHAT-WORKS.md) and the
[user guide](../USER-GUIDE.md) for what it adds to them.
