# Links

Phase 13. What a link is to Marbas, how the three kinds are made, and why every
walk in the application stops at one — but the search, which phase 14 lets
through.

## The three kinds

**`LinkKind` is a word in `Domain`: `Symbolic`, `Hard`, `Junction`.** They are
three different things that happen to share a menu.

| Kind | What it is | Points at | Windows | Unix |
|------|------------|-----------|---------|------|
| Symbolic | a small entry holding a path, followed by the system | a file or a directory, anywhere, even nothing | yes, for an administrator or with Developer Mode on | yes |
| Hard | a second name for the same file | a file on the same volume | yes | yes |
| Junction | an NTFS mount point aimed at a directory | a directory | yes | **no** |

A symbolic link and a junction are *links*: an entry of their own that says
where it leads, which is what `FileSystemInfo.LinkTarget` reports. A hard link is
not: once made, both names are the file, neither is the original, and no listing
can tell which came first. So a hard link is drawn and walked as the ordinary
file it is, and everything below about "a link" means the other two.

**What a kind can point at is a fact of the kind, answered before the system is
asked.** `Links.refusal` reads the platform and the file system and answers a
`LinkRefusal` — a hard link to a directory, a hard link onto another volume, a
junction to a file — or nothing. Each has one sentence in `LinkRefusalMessage`,
which the `Alt+F6` popup shows under the choices while the path is typed and
the failures dialog shows afterwards, so the two never disagree. The volume is
the longest drive root holding the path, compared without regard to case on
Windows. `Links.create` answers the refusals every kind shares first: a location
something already stands at is `TargetExists`, and a target that is not there
is `SourceNotFound`, because a file manager has no business making a link to
nothing. The target is written absolute; a relative one is read from the
directory the link will stand in.

**Which kinds a platform can make is said by the adapter, not checked by the
logic.** `NativeLinks` is a record of two functions — `MakeHard`, and
`MakeJunction` as an option. Windows supplies both and Unix supplies no junction,
so no code on Unix can make one, and `Links.available` reads the kinds off that
record into `Shell.Dependencies.LinkKinds`, from which the popup is built. On
Linux the junction is not a greyed row; it is not there. `LinkKind.Junction`
stays a value every platform can spell, because it travels as data, and
`Links.create` handed one with nothing to make it answers `KindUnavailable` — a
branch no popup reaches.

## Which system carries which

**The symbolic link is the one the framework gives.** `fs.File.CreateSymbolicLink`
and `fs.Directory.CreateSymbolicLink`, chosen by what the target is. Windows
refuses it to a user who is neither an administrator nor in Developer Mode, and
the refusal is not the access refusal one would guess: it is an `IOException`
carrying `0x80070522`, `ERROR_PRIVILEGE_NOT_HELD`, with a message in the
system's own language. `Operations.mapException` reads the code into
`SymbolicLinkNotPermitted`, whose sentence says which of the two to switch on and
where, so copying a directory link and making one say the same thing.

**The other two have no managed API and live in `Adapters/NativeLinks.fs`.**

| Call | System | Makes |
|------|--------|-------|
| `CreateHardLinkW` from `kernel32` | Windows | a hard link |
| `link` from `libc` | Unix | a hard link |
| `Directory.CreateDirectory`, then `CreateFileW` and `DeviceIoControl` with `FSCTL_SET_REPARSE_POINT` | Windows | a junction: a directory turned into a mount point, removed again if that fails |

A Windows failure becomes the exception its error code stands for; a Unix
`errno` becomes an access refusal for `EPERM` and `EACCES` and an `IOException`
with the system's words otherwise, so every failure reaches `mapException` the
way a managed one does. Only `Adapters` touches the system, as the dependency
rule has it: `Dependencies.create` wires `MakeLink` to `Links.create` over the
real adapter, and `RefuseLink` to `Links.refusal` with the platform, which only
`Adapters` knows.

## Why only one kind is testable without a disk

`MockFileSystem` implements `CreateSymbolicLink` and `LinkTarget`, so the
symbolic link — the kind that matters most — is made and read back in the unit
suite like any other operation. Nothing a mock offers reaches `kernel32` or
`libc`, so the hard link and the junction are made once each on the real disk in
`Integration/Disk/NativeLinkTests.fs`, and everything that can be decided
without the system is tested on the mock with the native functions replaced by
a recorder that proves they were not called. One refusal escapes even that: the
mock names every drive `/:\` on Linux, so a hard link across two Unix mounts is
left to the real system, whose refusal still arrives mapped. See
[Testing](TESTING.md) for the disk tests and `[<SymbolicLinkFact>]`, which skips
where Windows refuses the user a symbolic link.

## Every walk stops at a directory link

**A directory link is a leaf.** A listing entry carries `LinkTarget`, read
through `FileSystemPath.linkTargetOf` — the one answer to "is this a link" that
the listing and every walk share. A link is drawn in bright cyan whatever it
points at, and the status line says `twin -> C:\data\docs` after its name. Then
every recursive walk in the application takes the link and not the tree behind
it:

| Walk | What happens to a directory link |
|------|----------------------------------|
| Copying | a new symbolic link pointing where the old one did |
| Moving | the same, then the old link is removed |
| Packing | an empty directory entry |
| Deleting | the link alone is removed |
| Measuring | counted as nothing |
| Wiping | removed, never written through |

**The reason is a cycle, and the second reason is what the link points at.** A
link to its own ancestor turns any walk that follows it into one that never
ends, and .NET follows: enumerating a directory link on a real disk walks into
the target, which was measured rather than assumed. And walking through a link
acts on things the user never selected — deleting or wiping a link must not
empty the directory it names somewhere else. A file link is still copied and
packed by its contents, since following it cannot spin; a directory link is
never a *non-empty* directory, so the delete confirmation does not warn about
contents it will not touch.

**Deleting walks the tree itself.** `Directory.Delete` with `recursive` removes
a symbolic link inside a tree without following it, but it stumbles on a
junction: it asks `DeleteVolumeMountPoint` about every mount point it meets,
throws on the junction, and leaves the tree behind. So `Operations.delete` walks
a tree the way wiping always has — a directory link is removed on its own, a
directory is emptied and then removed, nothing is followed — and the disk tests
record both halves: what .NET does, and that the walk succeeds and leaves what
the junction points at.

**One consequence is a failure where there used to be a copy.** Copying a
junction, or any directory link on Windows for a user without the right to make
symbolic links, now fails for that entry, since the copy is a new link; the
failure says why.

## The one walk that may follow a link

**Searching is the exception, and only when asked.** Every walk above stops at a
directory link because following one acts on what the user never selected; a
search acts on nothing, and a user who keeps their projects behind a link wants
them found. So `FileSearch.run` follows a directory link when the query says
`FollowLinks`, and treats it as a leaf like every other walk when it says
`StopAtLinks`.

**A set keeps it from spinning.** The walk remembers every directory it has
entered, and a link's identity is its target rather than its own path: a link is
entered only if the path it resolves to is not in the set yet. A link pointing
at its own ancestor is therefore refused rather than walked, a cycle terminates
with every real directory reported exactly once, and a directory reached both
directly and through a link is searched once. A link leading out of the tree is
entered like any other place. `Fakes.LinkFollowing` — the mock that walks
through a link the way a real system does and throws once a walk has gone round
one — proves all three, and that `StopAtLinks` never enters a link at all. See
[Finding files](FINDING-FILES.md).

## `Alt+F6`

The key takes the operation targets — the marks, or the entry under the cursor —
and opens *Link*: a field holding the other panel's directory and, for one
entry, that entry's name, above a choice of the kinds the platform offers. The
field is read the way `F6` reads its own: for one entry it is where the link
will stand, a bare name beside the entry and an existing directory inside it
under the entry's name; for several it is the directory each link lands in.
`Links.requests` is that reading, and the popup and the update both ask it.
Each keystroke asks `RefuseLink` about every kind and greys out what is refused,
with the sentence beneath; with several entries a kind is greyed out only when
every one of them refuses it, and the entries that refuse come back as failures
of their own. A chosen kind the new text refuses gives way to the symbolic link.
Terminal.Gui cannot disable one option of an `OptionSelector`, but its options
are `CheckBox` subviews, and disabling one was measured to take it out of the
arrow keys and its hot key alike. The links are made one after another without
a progress window, since each costs one call; what failed keeps its mark, and
both panels are read again with the cursor on the new link.
