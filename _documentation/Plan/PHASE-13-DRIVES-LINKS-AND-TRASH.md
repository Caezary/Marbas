# Phase 13 — The drive, the link and what is left behind

Goal: five commands about what an entry sits on and what becomes of it, rather
than about the entry itself. `Alt+F1` and `Alt+F2` stop offering a bare list of
paths and show the drives as drives — what kind each one is, how large it is and
how much of it is free — landing the panel where the other panel already stands
when the drive chosen is the drive that one is on. `Ctrl+\` reaches the root of
the volume in one key. `F3`, which does nothing at all on a directory today,
measures it. `Alt+F6` puts a link beside what it points at, in all three kinds
the two systems know. `Alt+Del` removes an entry without leaving its contents
behind on the disk.

What the terminals deliver was measured before any of these keys was bound, the
way P10.7 measured `Enter` with a modifier and P11 measured `F11`. Five runs of
`_probes/KeyProbe` on 2026-09-12 — cmder, Windows Terminal, the console, and a
WSL session started from the first and from the third — agree on every one of
them. `Alt+Del` arrives everywhere and wants no stand-in. `Ctrl+\` never arrives
as a backslash: the terminal sends the control byte `0x1C` and the driver reads
that byte back as `Ctrl+4`, the other key it belongs to, so `Ctrl+4` is what the
command is bound to and `Ctrl+\` is kept beside it. `Alt+\`, the way out this
phase had planned, arrives as nothing at all. And `Shift+Enter` inside a popup is
the bare `Enter` P10.7 already found in the panel, which on a popup is the
default button — so `Alt+R`, which does arrive inside a modal dialog, is the only
key P13.15 takes.

- **P13.1 The keys the terminals deliver** — `_probes/KeyProbe` gains `Alt+Del`,
  `Ctrl+\` and the stand-ins the phase would fall back on, `Alt+\` and
  `Alt+Shift+Delete`, and asks besides whether `Shift+Enter` and `Alt+R` reach a
  dialog that is up rather than the panel behind it, since P13.15 wants them
  there. Five runs as in P11 — cmder, Windows Terminal, cmd, and a WSL session
  started from the first and from the third. *Done when:* each new key either
  arrives everywhere or has a second binding that does, and what was measured is
  a row in [Cross-platform](../Architecture/CROSS-PLATFORM.md).
- **P13.2 The root of the volume** — `VolumeRootRequested` walks the active
  panel to the root of the path it stands on, on `Ctrl+4` — which is what
  pressing `Ctrl+\` delivers — with `Ctrl+\` itself bound beside it, the way
  `hiddenKeys` holds two. A panel inside an archive leaves
  the archive, because the root asked for is the volume's and the archive is a
  file on it; a panel already standing on a root has nothing to do and says so
  by staying. The move goes through the transition `PathOpened` already uses, so
  the cursor rules and the watch of P12.2 hold without a second path through the
  update. *Done when:* a panel nested three directories deep, one on the root,
  one inside an archive and one on `/` are each covered, and the effect the
  watch needs is raised.
- **P13.3 Measuring a directory** — `DirectoryMeasure.measure` walks everything
  below a directory through `IFileSystem`, summing the lengths of the files,
  reporting each directory it enters through the `BatchControl` the batches
  already carry and stopping when that control is cancelled. A subdirectory it
  cannot read is skipped rather than raised, which makes the answer a partial
  one, so the answer is a value that says which it is — an exact total or a
  lower bound. A directory link is a leaf here as everywhere else (P13.8), so a
  link pointing at its own ancestor cannot spin. A directory inside an archive is
  measured from the archive's index instead: read once through `ArchiveReader`,
  summed below the level asked for, every level under it reported through the same
  control. Level by level through `ArchiveListing` would re-open and re-scan the
  whole archive once per directory, which a packed source tree would pay for a
  few hundred times over. What it answers is what the entries say they hold
  unpacked, not what they cost packed. *Done when:* a nested tree on `MockFileSystem`
  measures to the byte, an unreadable subdirectory answers a lower bound, a level
  inside an archive answers the unpacked size of everything under it, and a
  cancelled walk answers what it had counted.
- **P13.4 `F3` on a directory** — `ViewRequested` on a directory raises
  `MeasureTargets`, over the operation targets every other key takes — the
  marks, or the entry under the cursor — and the walk runs behind the progress
  window with `Esc` stopping it, since a tree of a hundred thousand files must
  not hold the loop. What comes back is kept against the entries of that panel
  and shown in two places: the size column, in place of the `<DIR>` the entry
  wears today, and the status line under the panels, a lower bound prefixed with
  `≥`. A measurement belongs to the listing it was taken over, so re-reading the
  panel — `Ctrl+R`, or the watch of P12 — forgets it. `..` measures the
  directory it leads to: the parent, and with it the panel's own tree and every
  sibling beside it.
  *Done when:* `F3` on a directory shows its size in both places, several marks
  are measured in one run, and `Esc` leaves the panel as it was.
- **P13.5 Sorting the measured directories** — sorting by size compares
  directories by what was measured and files by their length, which the group
  rank of P2 already keeps apart, so the two never meet in one comparison. A
  directory nobody has measured counts as nothing and therefore keeps the name
  order among its own kind, which is what the panel showed before the key was
  pressed. *Done when:* a panel of measured and unmeasured directories and files
  sorts as described in both directions.
- **P13.6 Wiping** — `Operations.wipe` clears the read-only attribute, writes one
  pass of random bytes over the whole length of the file through `IFileSystem`,
  buffer by buffer, reporting and asking the batch control about cancellation at
  each of them, flushes, truncates the file to nothing, renames it to a name of
  its own length in the same directory and then deletes it — the name being as
  much of a leak as the contents. A directory is walked depth-first and removed
  after what was in it. One pass, not three: on a solid-state disk with `TRIM`,
  on a copy-on-write file system and on one with a journal the further passes
  buy nothing that the first did not, and cost the whole length of the file
  again. A run stopped inside a file leaves that file where it is — its contents
  are already gone — and reports it, because pretending `Esc` undid it would be
  untrue. *Done when:* the mock shows the contents replaced before the entry is
  removed, a read-only file is wiped rather than refused, and a cancelled run
  names the file it had begun.
- **P13.7 `Alt+Del`** — `DeleteMode` gains a third case beside `ToTrash` and
  `Permanently`, and the confirmation carries words of its own: what wiping
  does, and what it cannot promise — that on a solid-state disk, on a
  copy-on-write file system or on one with a journal the old blocks may outlive
  the file anyway. The effect leaves the synchronous path the other two deletes
  take and runs through `RunBatch`, which is what the rough edge recorded under
  delete has been asking for: this is the one delete that has something to
  report. The panel is read again afterwards and the marks of what survived
  stay. *Done when:* `Alt+Del` wipes the operation targets behind the progress
  window, the confirmation says what it is about to do, and `Esc` stops the run.
- **P13.8 A link says it is one, and the walks stop at it** — the listing asks
  `ResolveLinkTarget` and an entry carries what it found, so a link is drawn in
  a colour of its own and the status line says `-> target` after its name. Every
  recursive walk in the app — copying, moving across volumes, packing,
  measuring, wiping, deleting — treats a directory link as a leaf: the link is
  taken, the tree under it is not. That is what keeps a link pointing at its own
  ancestor from spinning, and it is the reason this task comes before anything
  creates one. What .NET does on its own here is proved by a test rather than
  assumed. *Done when:* a directory link is copied as a link and not as a tree,
  deleting one leaves what it points at, and a cycle terminates.
- **P13.9 The symbolic link** — `LinkKind` (`Symbolic`, `Hard`, `Junction`) and
  the request that carries a kind, a target and where the link is to stand.
  `Links.create` makes the symbolic one through `fs.File.CreateSymbolicLink` and
  its directory twin, which `MockFileSystem` implements, so the rule that logic
  is tested without touching the disk holds for the kind that matters most. The
  target is written absolute. Windows refuses a symbolic link to anyone who is
  neither an administrator nor in developer mode, which arrives as a bare access
  refusal and must not be reported as one: it is a refusal of its own, saying
  which of the two to switch on. *Done when:* links are made and resolved on the
  mock, and the Windows refusal is mapped rather than passed through.
- **P13.10 The two the framework does not give** — neither a hard link nor a
  junction has a managed API. `CreateHardLinkW` in `kernel32` makes the first on
  Windows and `link` in `libc` makes it on Unix; a junction is a reparse point
  written with `DeviceIoControl` and exists on Windows alone. All three live in
  `Adapters/NativeLinks.fs`, behind a `MakeLink` port in `Shell.Dependencies`,
  so the update and the dialog stay testable against a fake and only `Adapters`
  touches the system, as the rule has it. What a kind can point at is a fact of
  the kind and is answered before the system is asked: a hard link only to a
  file on the same volume, a junction only to a directory and only on Windows, a
  symbolic link to either on both. *Done when:* each refusal is answered without
  a system call, and each kind is made once against the real file system in the
  integration tests.
- **P13.11 `Alt+F6`** — the popup asks for the target — an edit box holding the
  other panel's directory and the name of what is under the cursor, read the way
  `F6` reads its own — and for the kind, with the kinds this target cannot carry
  disabled and the reason on one line beneath. Several marks make one link each
  inside the directory that was typed, the box then naming that directory alone,
  which is how `F5` and `F6` already read a target with several sources. The
  other panel is read again with the cursor on what was made. *Done when:*
  `Alt+F6` makes each of the three kinds where the system allows it, refuses the
  rest with a sentence, and the new link is visible as a link straight away.
- **P13.12 A drive is a value** — `Drive` — the root it is reached by, its
  label, its kind, its size and its free space — and `DriveKind`, which says
  `fixed`, `removable`, `network` or `optical` rather than leaving a
  `System.IO` enumeration to reach the screen. `Drives.list` reads them through
  `fs.DriveInfo` and hides what cannot be used: a drive that is not ready, which
  is what an empty card reader looks like, and on Unix the mounts that are not
  storage at all — `squashfs`, `tmpfs`, `devtmpfs`, `overlay` and their like —
  which otherwise bury the two real mounts under a dozen snap loops. The home
  directory keeps the row `RootAndHome` gives it today. `Roots` and `RootChoice`
  fold into this; the root prompt is their only caller. *Done when:*
  `MockFileSystem` with drives added covers the kinds, the sizes, both filters
  and the home row, on each platform's spelling of a root.
- **P13.13 The drive popup** — one row per drive: the root, the label, the kind
  spelled out, the total size and the free one, in columns that line up the way
  `TabRow` lines up tabs. A `ChooseDrive` port of its own replaces the generic
  `ChooseOption` for this effect, because a row is now four things and not a
  path. The list is read at the moment the popup opens, so a stick plugged in
  while Marbas was running is simply there, and nothing polls for it in the
  background. Read off a rendered screen, not off a property of the view. *Done
  when:* `Alt+F1` and `Alt+F2` show the drives with their sizes, a drive added
  between two openings appears in the second, and `Esc` changes nothing.
- **P13.14 Where the panel lands** — the drive chosen is the drive the other
  panel stands on, and the panel goes to that panel's directory rather than to
  the root: two panels on one disk are almost always wanted in one place. Any
  other drive gives its root, and so does the drive this panel is already on,
  since `Ctrl+\` is the key that asks for a root deliberately. A drive that went
  away between the listing and the choice — the stick pulled out while the popup
  was up — reports itself as the listing failure it is and leaves the panel
  where it was. *Done when:* each of the three cases is covered by a test over
  the update, and the vanished drive raises a dialog rather than an empty panel.
- **P13.15 The drive in the system file manager** — `Alt+R`, one of the two keys
  the panel already uses to hand something to the system, answers inside the
  popup as well, and what it answers with is a second kind of choice: the drive
  to stand on, or the drive to hand over. `Shift+Enter`, its twin in the panel,
  is not bound here — P13.1 found it arriving as a bare `Enter`, which a popup
  reads as its default button, so it would put the panel on the drive the user
  asked to see elsewhere. Handing it over is the `LaunchRequest` of P10.2
  unchanged — on Windows the shell opens the directory in the system file
  manager, on Unix `xdg-open` does — and the popup closes with both panels where
  they were. *Done when:* `Alt+R` on a drive opens it outside Marbas and moves
  neither panel.
- **P13.16 The documentation** — `Architecture/LINKS.md` says what the three
  kinds are, which system carries which, why only one of them is testable
  without a disk, and why every recursive walk stops at a directory link;
  [File operations](../Architecture/FILE-OPERATIONS.md) gains wiping — the one
  pass, the name, and what it cannot promise — and the measurement behind `F3`;
  [UI and input](../Architecture/UI-AND-INPUT.md) gains the drive popup, the size
  column a measured directory fills and the three new keys;
  [Cross-platform](../Architecture/CROSS-PLATFORM.md) gains the interop row and the
  Unix mount filter; and the user guide gains all five commands. *Done when:*
  the phase is described where the rest of the design is, not only in this plan.

## Interop

Nothing new is taken from the package feed. The hard link and the junction are
three `DllImport` declarations — `kernel32` for `CreateHardLinkW` and
`DeviceIoControl`, `libc` for `link` — which the single-file publish of P8.3
carries as it carries the rest of the runtime, since a platform invoke into a
system library bundles nothing. The price is that these two kinds cannot be
exercised on `MockFileSystem`, which is why P13.10 answers every refusal it can
before calling out and leaves the calls themselves to the integration tests.
