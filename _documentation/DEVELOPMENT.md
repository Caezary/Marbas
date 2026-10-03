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
`TreatWarningsAsErrors`, which is on in every configuration, and
`--warnon:1182`, which turns an unused value into a warning and so, with the
first, into a build failure.
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

Since P17.2 the probe is a guided walk rather than a checklist. It names one
physical key at a time — "Ctrl with the + of the numeric keypad" — and records
everything that arrives while that name is up, in three places: the
application's `Keyboard.KeyDown`, the `KeyDown` of the view that would handle
the key in Marbas, and the driver's `AnsiSequenceSwallowed` and `Paste` events.
`Tab` moves to the next key, `Shift+Tab` back, `Ctrl+Q` stops, and the report
lands in `keyprobe-<moment>.txt` in the working directory, which git ignores. A
checklist waits for an exact `Key` and so cannot say what arrived instead, which
is the whole answer for a grey key, for `Ctrl+M` or for `Alt` with a shifted
digit; the swallowed sequences are what showed that Terminal.Gui drops `Alt`
with punctuation. Each key walks through the place it is meant for — a focused
`ListView` for the panel, a full-screen `Window` for the viewer, a modal
`Dialog` holding a list — and the keys come in rounds: no argument runs the keys
of phases 17 to 20, `stand-ins` only the letters chosen to stand beside the ones
that failed. A new phase adds its keys as a round of its own.

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

## Flat decisions

Three rules decide when a decision is written out and when it is folded into
one expression. They are about shape, not about taste, so they can be applied
without arguing.

**Three binds or one nested match becomes a computation expression.** A chain
of two `Result.bind`s or `Option.bind`s reads fine as a pipeline. At the third
step, or as soon as a `match` on a `Result` or an `Option` contains another
one, the block becomes `result { }` or `option { }` (FsToolkit), with one
`let!` per step. `Links.create`, `Archive.copyToArchive`, `Workspace.scratch`
and `AppState.finishTransfer` / `finishPack` / `finishLinks` / `makeFile` are
what the rule looks like applied.

A computation expression is the wrong answer when what the chain really needs is
one fewer case. `Shell.passwordOutcome` answered three — not needed, given, turned
down — and feeding that to `option { }` meant mapping it to
`ArchivePassword option option`, where "not needed" is written `Some None`.
Nothing downstream told the first two apart, so the union says
`OpenWith of password: ArchivePassword option | NotGiven` and the consumers are
a two-arm match. A nested option is a sign that two questions were forced into
one type; name the answer instead of wrapping it.

**A `match` never nests three deep.** Two independent questions become one
`match` over the tuple, provided both are cheap to ask: a tuple is evaluated
eagerly, so a question that touches the disk or creates something stays behind
its own arm. What is left after that goes into a named helper — the inner block
of `Update.editing` is `showingProperties`, the third level of `Links.create`
is `make`, the body of `Archive.copyToArchive` is `carriedInto`.

**A chain of three constructors becomes a pipeline.** `request state
(ReportOperationError(Operations.ArchiveFailed(KindCannotBeChanged path)))` is
read inside-out; the same thing as
`KindCannotBeChanged path |> Operations.ArchiveFailed |> reporting state` is
read left to right. Where the innermost two are always the same pair, a helper
named after the whole — `Update.reporting` — removes a level everywhere at
once.

**An `Option` consumed for its effect is `Option.iter`, not a `match` with an
empty `None` arm.** Every prompt in `Shell.interpret` answers an option; the
ones with a single `Some` arm are `|> Option.iter (Message >> dispatch)`, and
the ones whose answer is a union of their own keep the `match`.

## Names that tell the boundary

A preposition says where something sits and nothing about the rule: `under`
does not say whether the target itself counts, `into` does not say what it
answers, and two helpers that differ only by `Of`, `In` or a prime have to be
opened to be told apart. Four rules decide a name, and P15.20 applied them to
every module.

**A name is never only a preposition.** `Archive.at` is `sinkAtLevel`,
`Archive.under` is `entryAtOrUnder`, `PanelLocation.into` is
`PanelLocation.child`, `Zipper.at` is `Zipper.refocus`, and
`ContentMatch.inStream` is `searchStream` beside `search`. A preposition may
stay when the words beside it carry the rule — `atOrUnder` and `strictlyUnder`
say whether the boundary is strict, `keyAtLevel` says what is keyed.

**Two helpers in one module differ by a noun, not by a preposition or a
prime.** `DirectoryListing.entryOf` and `entryAt` are `entryFromInfo` and
`existingEntry`; `Links.refusal` and `refusalFor` are `refusal` and
`refusalForAbsolute`; `Settings.numberOf` and `numberIn` are `parsedNumber`
and `tabNumberInKey`; `LineIndex.scan` and `scanThrough` are `scanChunk` and
`scanUntilKnown`; `ArchiveFormat.kind` and `ofKind` are `kind` and
`tryCreate`, the name §3 of the conventions gives a constructor that validates;
`AppState.settings` and `fromSettings` are `settings` and `restore`, with
`restoredPanel` and `restoredTab` under it; `locationAbove`, `locationOf` and
`locationOfName` are `parentLocation`, `enteredLocation` and `childLocation`;
`navigateUp` and `navigateInto` are `navigateToParent` and `navigateToChild`;
`KeyMap.onBar` and `offBar` are `labelled` and `unlabelled`, which is what
they build. A local closure counts when it is a helper of its own —
`SearchListing`'s memoising `held` kept its name and the module-level `heldIn`
it wrapped became `indexedPrefixes`. A primed parameter is an alias too:
`Operations` lost `let copyToTarget fs' source' = copyFile fs' true source'
target` to a named `copyReplacing target`.

Two shapes do *not* count. A function and the value it answers, bound under the
function's noun — `let probe = probeOf stream` names one thing twice, not two
helpers. And `withX` is the record-copy idiom — `withCursor index panel` is the
panel with its cursor replaced — so it stands beside `cursor` the way
`{ p with Cursor = … }` does.

**A direction names both ends.** `Archive.moveIn`, `moveOut`, `moveAcross` and
`copyAcross` said the direction and left the other side implied; they are
`moveFromDisk`, `moveToDirectory`, `moveToArchive` and `copyToArchive`, the
vocabulary `FromDisk` / `ToDirectory` / `ToArchive` the transfer types already
spoke. `Shell`'s `approvedOnDisk` / `approvedInArchive` / `approvedInto` /
`approvedAcross` became `pieceApprovedForDirectory`, `pieceApprovedForArchive`,
`pathsApprovedForArchive` and `entriesApprovedForArchive` — what is approved,
and for which kind of target.

**A name that lied about its boundary says the real one.**
`toggleSelectionThrough` stopped *short* of its target, which the tests had to
spell out ("marking through an entry stops short of it"); it is
`toggleSelectionShortOf`. `Archive.beneath` kept the target's own name, so it
is `segmentsFromTargetName`, not a path after the target. `withCursorWithin`
clamped the index to the listing, and is `withCursorClamped`.

The `changing` and `opened` families are named for what they do rather than
the verb they share: `Update.refuseUnchangeable`, `openCursorEntry`,
`openFile`, `openWithHandler`, `reportNotRunnable`, `reportResultsRefusal`,
`rereadAndSave`; `Archive.withArchiveOpenForChange` hands its work an
`OpenForChange` of `Held` entries and a `Sink`, and dispatches to
`updatingZip` or `rewritingWhole`, which writes through `writingFreshCopy`.

## Documentation map

- [Implementation plan](PLAN.md) — the index of the phases, one file per phase
  in [Plan/](Plan) (P1.1 … P15.26)
- [Progress](PROGRESS.md) — current status, with the journal of each phase in
  [Progress/](Progress) and the catalogue in
  [What works today](Progress/WHAT-WORKS.md)
- [Coding style and conventions](coding-style-conventions.md) — the rules code
  and tests are held to
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
