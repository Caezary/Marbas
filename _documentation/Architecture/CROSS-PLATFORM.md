# Cross-platform checklist

P8.2. What has to hold on Windows and on Linux, how to check it, and what is
known to differ. Run it before a release and after anything that touches
`Adapters/`, `Drives`, `Links`, `Settings` or a key binding.

Six kinds of evidence appear below:

- **suite** — an automated test asserts it; `dotnet test` on both platforms is
  the whole check, and `.github/workflows/ci.yml` runs both legs on every push.
- **WSL** — verified by running the built application under WSL (Ubuntu 24.04,
  .NET 10.0.110) on 2026-08-18, driving it through a pseudo-terminal and
  reading the screen back.
- **probe** — measured by a throwaway program rather than asserted, in three
  flavours. `_probes/KeyProbe` reports what a terminal really hands to
  Terminal.Gui rather than what a binding says it should; the rows marked so
  under *Keyboard and terminal* were measured with it on 2026-09-05 in cmder,
  Windows Terminal and the Windows console, and under WSL started from cmder and
  from the console. The rows under *Watching the directories on screen* were
  measured on 2026-09-11 with a probe that builds the real `MainWindow` over a
  real file system and changes the disk behind its back. The drive popup rows were
  rendered on 2026-09-14 with the `render-tui-screen` probe, which runs the real
  application with `Launch` replaced by a recorder. The rows of phases 17 to 20
  were measured on 2026-10-02 and 2026-10-03 in the five terminals of P13.1 with
  the guided walk the key probe has had since P17.2. See
  [Development](../DEVELOPMENT.md), *Probes*.
- **disk** — an integration test in `Marbas.Tests/Integration/Disk` does it
  against the real file system of whichever machine runs the suite, so it runs on
  both legs of CI; it is reached for only where no mock can say what the system
  does.
- **desktop defaults** — read out of a desktop environment's own configuration
  rather than pressed. A window manager grabs a shortcut before any terminal sees
  it, so what it claims by default decides whether a key can reach Marbas at all;
  `gsettings list-recursively org.gnome.desktop.wm.keybindings` under WSL answers
  for GNOME and Mutter without a desktop being installed. Read on 2026-09-16,
  and again on 2026-10-03 for the keys of phases 17 to 20.
- **terminal defaults** — read out of a terminal's own key table rather than
  pressed: Windows Terminal's `defaults.json` in its package directory (1.24.11911.0)
  and gnome-terminal's `org.gnome.Terminal.Legacy.Keybindings` (3.52.0), read with
  `gsettings` under WSL. Read on 2026-10-03. It says why a key the probe found
  missing went missing, and which keys a terminal takes only in a state no probe
  run had — several tabs open, or text selected.
- **manual** — needs a person at a real terminal. These are the items where the
  answer is "does it *look* right", which nothing here can assert.

## Paths

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Panels list a real directory, `..` first | `C:\…` | `/…` | suite, WSL |
| Entering and leaving directories keeps the cursor on the one left | same | same | suite |
| A panel at the root has no `..` | `C:\` | `/` | suite |
| `F6` typing a bare name renames in place | `notes.txt` | `notes.txt` | suite |
| `F6` typing a name with a separator means a directory | `docs\notes.txt` | `docs/notes.txt` | suite |
| A backslash inside a Linux file name is part of the name, not a separator | n/a | `docs\notes.txt` is one file | suite |

Nothing in `FileSystem`, `State` or `Ui` builds a path by hand: every join,
split and parent goes through `fs.Path`, so the separator follows the platform
without a single conditional. The tests say so too — `Marbas.Tests.Root.at`
turns `"start/docs"` into `C:\start\docs` or `/start/docs`, which is why one
suite covers both.

## Roots and drives

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `Alt+F1` / `Alt+F2` open *Drives* | lists ready drives, `C:\`, `D:\`… | lists the storage mounts, then the home directory | suite; probe on Windows; **manual** on Linux |
| Each row shows root, label, kind, size and free space in aligned columns | yes | yes, with no label column for mounts | suite; probe on Windows; **manual** on Linux |
| A drive plugged in while Marbas runs is listed on the next opening | same | same | suite |
| Choosing a drive the other panel stands on lands in that panel's directory | same | same | suite |
| Choosing any other drive lands in its root | same | same | suite, WSL |
| Cancelling changes nothing | same | same | suite, probe on Windows |
| A drive that cannot be read is reported, panel stays put | same | same | suite |
| `Alt+R` in the popup opens the drive in the system file manager, panels unmoved | Explorer | `xdg-open` | suite (injected), probe on Windows; **manual** for the window that opens |
| A drive that is not ready, or whose size cannot be read, is left out | yes | yes | suite |
| A mount that is not storage (`squashfs`, `tmpfs`, `proc`…) is left out | n/a | yes | suite |
| `Ctrl+4` / `Ctrl+\` send the panel to the root of its volume | yes | yes | suite, probe |

Both lists are `Drives.list fs home`: the ready drives that are storage, and a
row for `$HOME` after them when a home is given, which is what P3.5 asked for.
`Dependencies.create` gives one on Unix and none on Windows; everything below it
takes the home as a value, so both branches are tested on both platforms. The
mount points are spelled through `Fakes.MountedFileSystem`, a fake drive
factory, because `MockFileSystem` names every drive `/:\` on Linux; the tests
over drive letters carry `[<WindowsFact>]`.

**On Unix most mounts are not drives, so the list filters by format.**
`DriveInfo.GetDrives()` on Linux answers every entry of the mount table: the
root and a home partition, and beside them `proc`, `sysfs`, the cgroups,
`devpts`, `tmpfs` for `/run` and `/dev/shm`, an `overlay` per container and a
`squashfs` loop per snap — easily a dozen rows burying the two a user can store
anything on. `Drives.list` drops a mount whose `DriveFormat` is one of the
formats that hold no files of the user's (`autofs`, `binfmt_misc`, `bpf`,
`cgroup`, `cgroup2`, `configfs`, `debugfs`, `devpts`, `devtmpfs`, `efivarfs`,
`fuse.gvfsd-fuse`, `fuse.portal`, `fusectl`, `hugetlbfs`, `mqueue`, `nsfs`,
`overlay`, `proc`, `pstore`, `ramfs`, `rpc_pipefs`, `securityfs`, `selinuxfs`,
`squashfs`, `sysfs`, `tmpfs`, `tracefs`), a drive that is not ready — an empty
card reader — and one whose size throws when asked, which is what a mount the
user may not enter does. The format list runs on Windows too, where no drive
carries one of those names, so it needs no platform. A format not on the list
shows up as a drive; the list is a denial list on purpose, because a real file
system Marbas has not heard of is better listed than lost. The system's
`DriveType` is read into four kinds, with a RAM disk and an unknown type counted
as fixed.

## Links, wiping and measuring

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| A symbolic link or junction is drawn in its own colour, `-> target` on the status line | same | same | suite |
| Copying, moving, packing, deleting, measuring and wiping stop at a directory link | same | same | suite, disk |
| `Alt+F6` makes a symbolic link | only for an administrator or with Developer Mode on; otherwise refused with a sentence saying which | yes | suite, disk |
| `Alt+F6` makes a hard link to a file on the same volume | `CreateHardLinkW` | `link` | disk |
| `Alt+F6` makes a junction to a directory | `CreateFileW` + `DeviceIoControl` | **not offered** | disk |
| A kind the typed target cannot carry is greyed out with its reason | same | same | suite |
| `F3` on a directory measures it; `Esc` leaves `<DIR>` | same | same | suite |
| `Alt+Del` wipes behind the progress window, `Esc` names the file it was in | same | same | suite |
| The old blocks of a wiped file are unreadable afterwards | **not promised** on SSD, copy-on-write or journaling file systems | the same | stated in the confirmation |

**Interop is confined to one adapter.** The hard link
and the junction have no managed API, so `Adapters/NativeLinks.fs` holds the
`DllImport`s: `kernel32` on Windows (`CreateHardLinkW`, `CreateFileW`,
`DeviceIoControl` with `FSCTL_SET_REPARSE_POINT`) and `libc` on Unix (`link`).
`NativeLinks.forPlatform` picks the record for the platform and leaves the
junction out on Unix, so no import of the other system is ever bound — the same
guarantee `ModifierWatch` relies on for `user32`. The *disk* rows are
`Integration/Disk/NativeLinkTests.fs` and `LinkWalkTests.fs`, which run on both
CI legs and skip the symbolic-link half where the user may not make one. The
whole story is in [Links](LINKS.md).

## Finding files

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `Alt+F7` opens *Find* | yes | yes in a terminal; **no** under a GNOME desktop, which takes the key first | suite; probe; desktop defaults |
| `Alt+Shift+F` opens *Find* wherever `Alt+F7` does not | yes | yes | suite; probe |
| `PATH` is split at the platform's own separator | `;`, quotes round an entry trimmed | `:` | suite |
| A directory `PATH` names twice, in any spelling, is searched once; one that is not there is left out | same | same | suite |
| *From the root of the volume* does not cross into another listed drive below it | a drive letter holds nothing else | `/` does not enter `/media/user/stick` | suite, over `Fakes.MountedFileSystem` |
| *All drives* walks every listed drive and not the home row | the ready drives | the storage mounts; the pseudo file systems are never listed, so never started from | suite |
| *From the root of the volume* stays out of `/proc`, `/sys` and the other pseudo file systems | n/a | **no** — they are not listed roots, so nothing bounds them | code reading |
| A directory link is followed only when asked, and a link to its own ancestor ends the walk | same | same | suite, over `Fakes.LinkFollowing` |
| A UTF-16 file is searched as text | yes | yes | suite |

**The `PATH` separator is the one platform difference the search reads, and it
is read where the platform is known.** `HostEnvironment` carries `PATH` as the
raw string and `Dependencies.create` picks the separator; `FindGround`
`.pathDirectories` takes both as arguments, so the Windows and the Unix split are
tested on either system. Whether one path lies under another is the second
difference, and since P15.2 it is one rule: `FileSystemPath.strictlyUnder`,
`atOrUnder` and `sameEntry` take a `Platform` and fold case on Windows only,
`FileSystemPath.platformOf` reads that platform off the `IFileSystem` the module
already holds, and the rule is covered by `[<Theory>]` rows for both platforms
whichever system the suite runs on. Drive letters, mount points, search
boundaries, the archive's own refusal to pack itself and the walk's record of
where it has been all go through it.

**The volume is bounded by the other drives, not by asking the system.**
`FromVolumeRoot` starts at the root `Drives.holding` names and refuses to enter
any other root `Drives.list` gives, which is the plainest statement of what a
volume is to Marbas: on Windows no drive lies inside another and the rule never
fires, on Unix it is what keeps a search of `/` off a stick, a second disk and a
network share mounted below it. The price of reading the boundary off the list
is what the list leaves out: a mount the P13.12 filter drops as not being
storage — `/proc`, `/sys`, `/run` — is no listed root, so a search of `/` walks
into it like a directory. That is a defect, recorded in
[Rough edges](../ROUGH-EDGES.md); the fix is to hand the walk the
filtered mounts as boundaries too.

## Hidden entries

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| A dotfile counts as hidden | yes | yes | suite, WSL |
| An entry the OS marks hidden or system counts as hidden | yes | yes (dotfiles carry the flag) | suite |
| `Alt+H` reveals them and the title says `hidden` | same | same | suite, WSL |
| `..` is never concealed | same | same | suite |

`EntryVisibility.classify` asks about both conventions on both platforms rather
than switching on the OS, which is why a `.gitignore` copied onto a Windows
drive still reads as hidden there.

## Deleting

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F8` / `Del` ask before sending the targets to the trash | same | same | suite |
| `Shift+F8` / `Shift+Del` ask before deleting for good | same | same | suite |
| What the trash does with them | recycle bin | `~/.local/share/Trash` | **manual** |
| A deleted entry can be put back by the desktop's own tool | Explorer's *Restore* | the file manager's *Restore* | **manual** |
| A trash that refuses is reported entry by entry, marks kept | same | same | suite |

`Shell` asks `deps.Processes.SendToTrash`, and `Dependencies.create` decides what that is:
`RecycleBin.send` on Windows, which is
`Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` /
`DeleteDirectory` with `RecycleOption.SendToRecycleBin` — the VB
runtime ships with .NET, so nothing is added to the package list — and
`Trash.send` elsewhere, which is the freedesktop home trash written through
`IFileSystem` and therefore tested on `MockFileSystem` on both legs of CI. A
permanent delete is the same `Operations.delete` as before on either platform.

## Archives

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `Shift+F1` packs the targets into an archive in the other panel | same | same | suite |
| `Shift+F2` empties the archives under the cursor or marked into the other panel | same | same | suite |
| An archive made by another tool unpacks here with its tree and its times | 7-Zip, Explorer | `zip`, `tar` | **manual** |
| The name is deduced from the targets, the root from the drive | `diskCcontents.zip` | `rootcontents.zip` | suite (one test per platform) |
| Entry paths inside the archive are relative and separated by `/` | `docs\a.txt` is stored as `docs/a.txt` | already `/` | suite |
| A backslash inside a Linux file name stays in the entry name | n/a | it does not: `SharpZipLib`'s `ZipEntry` cleans the name, so `a\b.txt` is written as `a/b.txt` and the archive gains a level the disk never had | **manual** (seen while writing P9.7, never run on Linux) |
| A backslash in an entry another tool wrote is read as part of the name | same | same: `ArchiveListing` splits on `/` alone | suite |
| The zip opens in the system's own tool | Explorer, *Extract All* | `unzip`, the desktop's archive manager | **manual** |
| The tar.gz opens in the system's own tool | `tar -xzf` | `tar -xzf` | **manual** |
| A password-protected zip asks for the password there | Explorer cannot open AES zips at all; 7-Zip can | `7z x` | **manual** |
| Modification times survive the round trip | same | same | **manual** |

Everything up to the bytes is tested on `MockFileSystem`, both writers being
stream APIs, and one test reads a zip back with `System.IO.Compression` after
`SharpZipLib` wrote it — so the two halves of the suite already disagree if the
output is not a real zip. What no test can answer is whether the other side of
the platform fence likes it, which is why the round trips are manual. The one to
try first is the AES zip: Windows Explorer has never supported AES-encrypted
entries and will report the archive as empty or damaged rather than asking for a
password, which is a property of Explorer and not of the archive.

## Permissions

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Deleting a read-only file reports access denied, file survives | same | same | suite |
| A file held open by another process is reported, not thrown | same | same | suite |
| An unreadable directory leaves the panel where it was | same | same | suite |
| A root-owned file refuses to be deleted and is named in the dialog | n/a | **manual** | manual |
| A directory without `+x` is reported when entered | n/a | **manual** | manual |

The suite provokes failure on the file itself — `MockFileData.Attributes` set to
`ReadOnly`, `AllowedFileShare` set to `None` — which covers the mapping. What it
cannot cover is a real Unix permission bit, so the last two rows are for a
person with a `sudo`-owned file to hand.

## Session

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Quitting writes the session | `%APPDATA%\Marbas\settings.conf` | `~/.config/Marbas/settings.conf` | suite, WSL |
| Starting restores both panel paths, sorts and toggles | same | same | suite, WSL |
| Starting from a different working directory still restores | same | same | WSL |
| A stored path that no longer exists falls back to the working directory | same | same | suite |
| Starting restores every tab, and opens on the one it was left on | same | same | suite |
| Each tab comes back with both cursors where they stood | same | same | suite |
| A session written before tabs existed still opens, as one tab | same | same | suite |

`Settings.location` is what makes the Linux column true: .NET answers
`SpecialFolder.ApplicationData` with an **empty string** on a machine whose
`~/.config` does not exist yet, and the old code combined that empty string into
a *relative* path — so the session was written to `./Marbas/settings.conf`,
wherever the app happened to be started from. The fallback to `$HOME/.config` is
tested and was confirmed by running the app.

## Keyboard and terminal

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F1`–`F10`, `Tab`, `Insert`, `Enter`, arrows | same | same | suite, WSL |
| `Ctrl+Q` quits and saves | yes | yes | suite, WSL |
| `Ctrl+R`, `Ctrl+F3`–`Ctrl+F6` | yes | **manual** | manual |
| `Alt+H` toggles hidden entries | yes | yes | suite, WSL |
| `Ctrl+H` toggles hidden entries | only where the terminal reports it as a key | only under the kitty keyboard protocol | known difference |
| The bar follows a held `Ctrl` / `Alt` | yes | **no, by design** | known difference |
| `+` / `-` open the pattern prompt | yes | **manual** | manual |
| `Shift+↑` / `Shift+↓` arrive as keys of their own and mark a run | **manual** | **manual** | suite (injected), manual |
| `Shift+PgUp` / `Shift+PgDn` and `Shift+←` / `Shift+→` mark a page | **manual** | **manual** | suite (injected), manual |
| The screen redraws when the terminal is resized | **manual** | **manual** | manual |
| The sixteen classic colours look right | **manual** | **manual** | manual |
| Directory / file / archive / program colours are told apart | **manual** | **manual** | manual |
| Bright blue file names stay readable on the blue panel | **manual** | **manual** | manual |
| The rule under the listing joins the panel border | **manual** | **manual** | manual |
| Arrows stop at the ends of a panel; only `Tab` leaves it | **manual** | **manual** | suite (as handled keys), manual |
| `←` / `→` jump by the page on screen | **manual** | **manual** | suite, manual |
| Typing opens the search bar and moves the cursor | **manual** | **manual** | suite, manual |
| The mouse selects an entry and clicks the key bar | **manual** | **manual** | manual |
| `F11` lists the tabs | **only under cmder** — Windows Terminal and the console keep it for full screen | the terminal hosting the session decides, not the platform | probe |
| `Alt+T` lists them where `F11` is eaten | yes | yes | probe |
| `Alt+F11` opens a tab, `Alt+Shift+F11` closes it | yes | yes | probe |
| `Shift+F9` writes the session | yes | yes | probe |
| `Alt+←` / `Alt+→` walk from tab to tab | yes | yes | probe |
| `Ctrl+T`, the other candidate for listing tabs | **no under cmder**, which opens its own console tab | yes | probe |
| The tab counter is legible in the top right corner | **manual** | **manual** | manual |
| `Alt+Del`, the key phase 13 wipes with | yes | yes | probe |
| `Alt+Shift+Del`, the stand-in it turned out not to need | yes | yes | probe |
| `Ctrl+\` reaches the application as a backslash | **no**, arrives as `Ctrl+4` | **no**, the same | probe |
| `Alt+\` reaches the application at all | **no** | **no** | probe |
| `Shift+Enter` reaches a popup that is up | **no**, arrives as `Enter` | **no**, the same | probe |
| `Alt+R` reaches a popup that is up | yes | yes | probe |
| `Alt+F7`, the key phase 14 finds with, reaches the terminal | yes | yes | probe |
| `Alt+F7` reaches the application on a desktop | n/a, no window manager claims it | **no** under GNOME, whose `begin-move` is `<Alt>F7` by default | desktop defaults; **manual** for KDE and Xfce |
| `Alt+Shift+F`, the stand-in phase 14 therefore binds | yes | yes | probe |
| `F3` reaches a full-screen window that is up | yes | yes | probe |
| `Enter` reaches a full-screen window that is up | yes, in a `Window` with no buttons; **no** in a `Dialog`, where it presses the default button | the same | probe |
| `Esc` can be taken by a full-screen window and close it on the second press | yes | yes | probe |
| `Alt+F1`, `Alt+F2` and `Alt+F6`, already bound, reach the terminal | yes | yes | probe |
| `Alt+F1`, `Alt+F2` and `Alt+F6` reach the application on a desktop | n/a | **no** under GNOME: `panel-main-menu`, `panel-run-dialog` and `cycle-group` | desktop defaults |
| `Alt+F12`, the key phase 16 opens the history with | yes | yes | probe |
| `Alt+Shift+H`, its stand-in | yes | yes | probe |
| `Ctrl+PgUp`, `Ctrl+PgDn`, `Del`, `Shift+Del` and `Alt+G` reach a popup that is up | yes | yes | probe |
| `Alt+↓` and `F4` reach a drop-down field inside a popup | yes | yes | probe |
| `Alt+F12`, `Alt+Shift+H`, `Ctrl+PgUp`, `Ctrl+PgDn`, `Alt+G`, `Alt+↓`, `F4`, `Del` and `Shift+Del`, the keys of phase 16, are claimed by a desktop | n/a | no, none of them under GNOME; its only `Page_Up` bindings carry `<Super>` and its only `Down` bindings `<Control><Alt>` | desktop defaults, read 2026-09-28; **manual** for KDE and Xfce |
| The grey `+`, `-` and `*` are told apart from the keys of the main keyboard | **no**, the same code in all three | **no**, the same in both | probe |
| `Ctrl` with the grey `+` or `-`, the keys P17.3 was planned on | **no**, nothing arrives; Windows Terminal keeps both for the font size | **no**, nothing arrives | probe, terminal defaults |
| `Ctrl` with the main `+`, `=` or `-` | **no**: `Ctrl+5` and `Ctrl+7` under cmder, a bare `+` or nothing in Windows Terminal and the console | **no**: `Ctrl+5` and `Ctrl+7` under cmder, a bare `=` or nothing in gnome-terminal, which zooms on `Ctrl+plus` and `Ctrl+minus` | probe, terminal defaults |
| `Alt` with the grey or main `+`, `-` or `=` | **no**: the terminal sends `ESC` and the character and Terminal.Gui swallows it; cmder hands `Alt+grey -` over as a bare `-`; Windows Terminal keeps `Alt+Shift+plus` for a split pane | **no**, swallowed, or a bare `+` or `-` | probe, terminal defaults |
| `Shift` with the grey `+` or `-`, the keys P17.4 was planned on | **no**, a bare `+` or `-` | **no**: a bare `+` or `-` under cmder, nothing in gnome-terminal | probe |
| `Ctrl` with the grey or main `*` | **no**: a bare `*`, nothing under cmder, and `Backspace` for the main `*` | **no**: nothing or `Backspace` under cmder, a bare `*` in gnome-terminal | probe |
| A bare `*`, grey or main | yes | yes | probe |
| `Ctrl+M` | **no**, arrives as `Enter` | **no**, the same | probe |
| `Ctrl+PgUp` reaches the panel | yes | yes with one tab; gnome-terminal keeps it for `prev-tab` once there are several | probe, terminal defaults |
| `Alt+0` … `Alt+9` | yes | yes with one tab; gnome-terminal keeps them for `switch-to-tab` once there are several | probe, terminal defaults |
| `Shift+Alt+0` … `Shift+Alt+9` | **no**: `ESC` with `)`, `!`, `@` …, which Terminal.Gui swallows; cmder opens a new console on `1` to `9` | **no**, swallowed | probe |
| `Alt+F8` | yes | yes in the terminal; **no** on a GNOME desktop, whose `begin-resize` is `<Alt>F8` | probe, desktop defaults |
| `F2` | yes | yes | probe |
| `Ctrl+Ins`, `Alt+Shift+Ins`, `Ctrl+Alt+Ins` | yes; Windows Terminal's copy on `Ctrl+Insert` lets the key through while nothing is selected | yes under WSL in cmder; **no** in gnome-terminal | probe, terminal defaults |
| `Ctrl+Shift+Ins` | yes | yes under WSL in cmder; **no** in gnome-terminal, which pastes | probe |
| `Ctrl+Shift+C` and `Ctrl+Shift+X` | **no**, they arrive as `Ctrl+C` and `Ctrl+X` | **no**: `Ctrl+C` and `Ctrl+X` under cmder; gnome-terminal keeps `Ctrl+Shift+C` for its copy and hands over `Ctrl+X` | probe, terminal defaults |
| `Ctrl+Shift+V` | **no**: `Ctrl+V` under cmder, a paste in Windows Terminal and the console | **no**: `Ctrl+V` under cmder, a paste in gnome-terminal | probe, terminal defaults |
| `F7`, `Shift+F7` and `F8` reach a full-screen window that is up | yes | yes | probe |
| A bare letter, a digit, `Enter`, `Ins`, `Del`, `F4`, `Ctrl+↑` and `Ctrl+↓` reach a list inside a modal dialog | yes | yes | probe |
| The stand-ins of phases 17 and 18: `Alt` and `Alt+Shift` with `E`, `N`, `A` and `C`; `Alt+Shift` with `X` and `V`; `Alt` with `I`, `M`, `U`, `B`, `K`, `P`, `L` and `J`; a digit after `Alt+K` | yes | yes | probe |
| The keys of phases 17 to 20 are claimed by a desktop | n/a | `Alt+F8` alone under GNOME; none of the stand-ins | desktop defaults, read 2026-10-03; **manual** for KDE and Xfce |

`Ctrl+Q` deserves a note: it is `0x11`, which is also `XON`, so a terminal doing
software flow control could in principle eat it. Terminal.Gui puts the terminal
into raw mode, and the WSL run confirmed the key arrives and the session is
written — but `F10` is the binding to reach for if a particular terminal
disagrees.

**`F11` is the one key of phase 11 an application cannot count on, and the
terminal decides, not the platform.** Five runs of `_probes/KeyProbe` on
2026-09-05 agree on everything but two keys. `F11` never reaches the
application under Windows Terminal or under the Windows console, which both
keep it for full screen; cmder passes it through. A WSL session inherits
whatever hosts it — the same Linux build answers `F11` when started from cmder
and never sees it when started from the console — so the Linux column of that
row is not a property of Linux at all. `Ctrl+T`, the obvious spare key, is the
one cmder keeps for its own new-console popup, which is why the second binding
is `Alt+T`: a letter with `Alt` is the shape every terminal in this checklist
has been seen to deliver, the same reasoning that gave P10.7 `Alt+W` and
`Alt+Shift+R`. Everything else phase 11 asks for — `Alt+F11`, `Alt+Shift+F11`,
`Shift+F9`, `Alt+←` and `Alt+→` — arrived in all five runs and needs no spare.

`F11` keeps the label on the bar all the same. It is the key the phase was
asked for, it works where the terminal lets it, and the bar has no way to know
which terminal it is drawn in; `Alt+T` is named in the user guide beside it for
the terminals that eat it.

The five keys were then pressed in Marbas itself, under Windows Terminal, on
2026-09-09: `Alt+T` opens the popup, `Alt+F11` adds a tab and the counter
follows, `Alt+←` / `Alt+→` walk between them, `Alt+Shift+F11` closes one, and
`Shift+F9` writes `%APPDATA%\Marbas\settings.conf` without quitting. The probe
says a key arrives; that pass says the application answers it.

**`Ctrl+\` never arrives as a backslash, and the five runs of P13.1 agree on
why.** The terminal does not send a key there at all: it sends the control byte
`0x1C`, which is what `Ctrl` with a backslash has meant since the teletype, and
the driver decodes that byte back into the key that also produces it — `Ctrl+4`,
`code=0x40000034`. The same thing one place over: `Ctrl+/` is `0x1F` and arrives
as `Ctrl+7`. `0x1C`–`0x1F` are the four bytes the digits `4`–`7` share with the
punctuation beside them, and nothing downstream can tell which of the two was
pressed. So the command reads `Ctrl+4` and keeps `Ctrl+\` bound beside it — the
shape `hiddenKeys` and `openWithKeys` already have — and the second binding is
there for a driver that one day resolves the byte differently, not for a
terminal seen in this checklist. `Alt+\` was measured as the way out and is
worse: it arrives as nothing at all, in every one of the five.

**`Shift+Enter` is no better inside a popup than it was in the panel.** P10.7
found it arriving as a bare `Enter` and called that worse than a dead key; on a
popup it is worse still, because a bare `Enter` there is the default button, so
a drive the user meant to reveal would be opened in the panel instead — the
silent wrong answer again. The probe asked this of a real modal `Dialog` rather
than of the panel: `Alt+R` arrives inside one in all five terminals, and it is
the only binding P13.15 takes. `Alt+Del` and `Alt+Shift+Del` arrive everywhere
too, so wiping needs no spare at all — a letter or a named key with `Alt` is
once more the one shape that travels.

**`Alt+F7` is the first key in this checklist that no terminal argues about and
a desktop takes anyway.** P14.1 measured it on 2026-09-16 in cmder, Windows
Terminal, the Windows console and a real VTE terminal — `gnome-terminal` run
under WSLg, which is where a Linux terminal's own encoding can be read without a
Linux machine. All four hand the key over as `Alt+F7`, `code=0x80110075`. The
loss happens one layer higher: GNOME's `org.gnome.desktop.wm.keybindings`
claims `<Alt>F7` for `begin-move` by default, and a window manager's grab means
the terminal is never sent the key at all. So the measurement had to be split —
what the terminal encodes, which a probe can press, and what the desktop claims,
which is read out of its configuration. WSLg is exactly the right place for the
first half and cannot answer the second at all, because it runs no window
manager.

The same reading found three keys Marbas already binds in the same list:
`<Alt>F1` is `panel-main-menu`, `<Alt>F2` is `panel-run-dialog` and `<Alt>F6` is
`cycle-group`, which are the drive choosers of P13.13 and the link key of
P13.11. On a GNOME desktop those three commands cannot be reached, and unlike
`Alt+F7` they carry no second binding. That is a defect rather than a
measurement, and it is recorded as one in [Rough edges](../ROUGH-EDGES.md).

**A full-screen window may take `Enter`; a dialog may not.** The results window
of phase 14 needs `Enter` to choose the row under the cursor, and the probe first
asked this of a `Dialog`, which answered by closing: `Enter` presses the default
button before a handler sees it, exactly as `Shift+Enter` arriving as a bare
`Enter` does to a popup. Asked again of a full-screen `Window` with no buttons —
the shape `FileViewer` has had since P6.1 — the same key is delivered to the
handler in all four terminals, `F3` with it, and `Esc` can be taken on the first
press and close the window on the second. So the shape of the window is what
decides, not the terminal: a window that wants the keys of a panel must be
built like the viewer and reach its commands with keys rather than buttons.

**Every key of phase 16 travels, and the one surprise was the probe's own.**
P16.1 ran `_probes/KeyProbe` on 2026-09-28 in the five terminals of P13.1 —
cmder, Windows Terminal, the Windows console, WSL inside cmder and
`gnome-terminal` under WSLg — and all five agree to the code: `Alt+F12` is
`0x8011007A`, `Alt+Shift+H` `0x90000048`, `Ctrl+PgUp` and `Ctrl+PgDn`
`0x40110020` and `0x40110021`, and `Del`, `Shift+Del`, `Alt+G`, `Alt+↓` and
`F4` arrive as themselves, inside a dialog and inside a drop-down field alike.
Each run had one terminal tab open, so a terminal that walks its own tabs on
`Ctrl+PgUp` once there are several is not ruled out; `←` and `→` walk the
history's tabs as well and need no binding of ours. The stand-in is therefore not needed
anywhere measured; it stays bound because a terminal or a desktop not in this
list may still take `Alt+F12`. `Esc` did not close the probe's popups, which is
the probe and not the terminals: it bound `Command.Quit` to `Ctrl+Q` alone,
where `MainWindow.configureQuitKey` binds `Esc` beside it. The probe now binds
both, as the application does. What the same run showed about `Enter` on an
open drop-down list — that it closes the popup as well as choosing the row — is
the control's doing, not the terminal's, and is taken up in
[History](HISTORY.md).

**The grey keys are not keys of their own, and `Alt` with punctuation is not a
key at all.** P17.2 measured every key phases 17 to 20 bind, in the same five
terminals, with a probe rebuilt for the question: it names one physical key at a
time and records whatever arrives — at the application, at the view that would
handle it, and as a sequence the driver swallowed or a paste — because a
checklist that waits for an exact `Key` cannot tell a grey `+` from a main one.
Nothing can. A terminal sends the character either key produces, and
Terminal.Gui 2.4.17 runs its `ansi` driver on Windows as well as on Linux; even
its `windows` driver maps the keypad's `VK_ADD` and the main `VK_OEM_PLUS` to the
same character. So the plan's first fallback, the same modifier on the main key,
was measured too, and it fails in a different way in each terminal. `Ctrl` with
`=` or `-` is a control byte under cmder, decoded as `Ctrl+5` and `Ctrl+7` — the
`Ctrl+\` story again; Windows Terminal and the console drop the `Ctrl` or keep
the key for the font size; gnome-terminal zooms. `Shift` on a grey key is
dropped everywhere, which leaves a bare `+` or `-` — the key that already opens
the pattern popup. `Ctrl+M` is byte `0x0D` and arrives as `Enter` in all five.

`Alt` with punctuation is lost one layer higher. Every terminal sends what an
`Alt` chord has always been, `ESC` followed by the character, and the probe
caught each of them as a sequence the driver swallowed: Terminal.Gui's
`EscAsAltPattern` accepts `ESC` followed by a control character, a letter, a
digit or `_`, and nothing else. That takes `Alt` with the grey or main `+`, `-`
and `=`, and it takes `Shift+Alt+0` … `Shift+Alt+9`, which reach the driver as
`Alt` with the shifted character of the layout — `)`, `!`, `@` … — in every
terminal; cmder opens a new console on `Shift+Alt+1` to `9` before that. A
Terminal.Gui that decoded those sequences would still hand over a character that
depends on the layout, so there is no stable key to wait for. The same pattern
explains a row of P13.1 that was left as a fact: `Alt+\` is `ESC \`, and it
arrived as nothing at all for exactly this reason.

What travels is what travelled before: a letter or a digit with `Alt`, with or
without `Shift`. A second round on 2026-10-03 pressed every stand-in chosen
from that shape — `Alt` and `Alt+Shift` with `E`, `N`, `A` and `C`, `Alt+Shift`
with `X` and `V`, `Alt` with `I`, `M`, `U`, `B`, `K`, `P`, `L` and `J` — and each
arrived as itself in all five, as did a bare `*`, grey or main, and a digit after
`Alt+K`. The spelling each task of phases 17 and 18 binds is listed in
[the plan of phase 17](../Plan/PHASE-17-MARKS-PANEL-AND-FAVOURITES.md); the keys
of phases 19 and 20 — `F2`, `F7`, `Shift+F7`, `F8`, and a letter, a digit,
`Enter`, `Ins`, `Del`, `F4`, `Ctrl+↑` and `Ctrl+↓` inside a dialog holding a
list — arrived everywhere and need none.

The rest is terminals and desktops keeping keys for themselves. gnome-terminal
delivers none of `Ctrl+Ins`, `Alt+Shift+Ins`, `Ctrl+Alt+Ins` and
`Ctrl+Shift+Ins`, the last of which it pastes with, and keeps `Ctrl+Shift+C` for
its copy; `Ctrl+Shift+X` arrives without its `Shift` everywhere, and
`Ctrl+Shift+V` is a paste in three terminals and a bare `Ctrl+V` in the other
two. Windows Terminal's own copy on `Ctrl+Insert` lets the key through while
nothing is selected, which is why it arrived there. Every run had a single tab:
gnome-terminal's table binds `Ctrl+PgUp` to `prev-tab` and `Alt+1` … `Alt+0` to
`switch-to-tab`, so with several tabs open those keys stay in the terminal —
`Alt+U` stands beside `Ctrl+PgUp`, and the favourites popup reaches every slot
where the digits do not. And GNOME, which already took `Alt+F7`, holds `<Alt>F8`
for `begin-resize`, so the favourites popup has `Alt+B` beside it. `Alt+D` is
left unbound on purpose: it is the natural stand-in for the drive popups,
`Alt+F1` and `Alt+F2`, which GNOME also takes and which still have none.

## Watching the directories on screen

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| A file written by another program appears, a deleted one goes | same | same | probe |
| A directory made outside the application shows up | same | same | probe |
| Two hundred files written in one burst cost one listing, not two hundred | one or two, the burst outran the window | one | probe |
| Deleting the directory a panel stands in leaves it in the parent, no dialog | same | same | probe |
| Pulling out the drive a panel stands on leaves it on a root that exists | `subst Y: /D`, panel lands on `C:\` | n/a, no drive letters | probe (Windows), suite |
| A change made while a prompt is up waits for it to close | same | same | probe |
| Only the tab in front is watched | same | same | suite |
| Entries added inside a share the watcher says nothing about | **not seen until `Ctrl+R`** | **not seen until `Ctrl+R`** | known limit |

The **probe** rows were measured on 2026-09-11 with the scenes described under
[Development](../DEVELOPMENT.md): the real `MainWindow` over a real file system in
a temporary directory, changed from outside while the loop runs, the screen read
back afterwards. On Linux the same four scenes ran under WSL (Ubuntu 24.04,
.NET 10.0.110) through a pseudo-terminal, which is also where the dialog and the
panel underneath it were seen in one picture.

**The two systems do the same thing through entirely different machinery, and
`FileSystemWatcher` hides it.** On Windows it is `ReadDirectoryChangesW`, a
directory handle and a completion buffer; on Linux it is `inotify`, a per-user
instance with one watch per directory. Both report a directory and not a tree
here — `IncludeSubdirectories` is off — and both are given the same
`NotifyFilters`, though what each one maps those onto differs: the Linux driver
translates them onto the inotify masks it has, which is why the debounce matters
more there than the filter does.

**The inotify limits are two, and Marbas is nowhere near either.** Each
`FileSystemWatcher` takes one inotify *instance*
(`fs.inotify.max_user_instances`, 128 on a default Ubuntu) and one *watch* per
directory it is given (`fs.inotify.max_user_watches`, 8192 on older kernels,
65536 or more now). One tab watches at most two paths, and the previous pair is
disposed before the next is opened, so the count is two per application and not
two per directory ever visited. An application that watched a tree recursively
would have to care; this one does not.

**A file system that reports nothing is the other reason the poll exists.** SMB
and NFS shares deliver change notifications unreliably or not at all — the
server decides, not .NET — and so do some virtual file systems. The two-second
poll of P12.4 notices a watched path that went or came back on any of them,
because it asks `Directory.Exists` rather than the watcher. What it cannot
notice is a file added *inside* such a share, since it reads no listing: that is
the one row above marked a known limit, and `Ctrl+R` is still the answer there.

## Viewing and editing

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F3` opens the viewer, `F3`/`Esc` closes it | same | same | suite |
| `F4` in the viewer switches to a hex dump | same | same | suite |
| `F8` in the viewer reads the file in the code page chosen, popup and all | same | same | suite; probe on Windows |
| A UTF-16 file is shown as the text it is | same | same | suite |
| The viewer's arrows, page keys, `Home`/`End` and `F2` wrap | same | same | suite |
| `F4` starts the editor the extension is associated with | same | same | suite |
| `F4` falls back to the default editor, then `$EDITOR` | same | same | suite |
| `F4` falls back again when neither is set | `notepad` | `vi` | suite |
| `F4` on a directory opens its properties | same | same | suite |
| A flag written to a directory is read back | same | **manual** | suite (mock) |
| `Shift+F4` creates a file and opens it | same | same | suite |
| `F12` opens the settings popup | same | same | suite |
| `F4` waits for the editor | **no**, started through the shell | yes, with the terminal handed over | suite |
| A console editor gets a window of its own | yes, a console of its own | n/a | **manual** |
| A file saved in the editor shows in the panel | the directory watch | the directory watch | **manual** |
| A full-screen console editor gets every key and gives the terminal back | n/a, a console of its own | yes, the driver stopped and built again | suite (the switch); probe (tmux); **manual** |
| The screen is whole again after the editor exits, at the size it has then | n/a, nothing waited | yes | probe (tmux); **manual** |
| `Enter` walks into a directory and an archive | same | same | suite |
| `Enter` runs a document, an executable and an associated file | same | same | suite |
| `Alt+W` walks into the entry under the cursor | same | same | suite |
| `Ctrl+PgDn` reaches the panel as a key at all | **yes** | **manual** | manual (Windows Terminal) |
| A started program leaves the panels un-scribbled | a console program: own console, shell verb; a window program: started from its file, streams drained | streams drained | suite (the start described); probe (ConEmu); **manual** |
| A program named by its start is resolved to its file | as written, `PATH` with `PATHEXT`, App Paths | n/a | suite (mock); probe |
| A started program does not hold the loop up | same | same | **manual** |
| `F2` opens the user menu, a letter and `Enter` run an entry | same | same | suite; probe on Windows |
| A user menu entry is started the way it names | shell verb; `cmd /s /c`, `cmd /s /k` | drained; `/bin/sh -c` for both shell ways | suite (the start described); **manual** (each way run) |
| A user menu entry runs as administrator | `runas`, `cd /d` first in the shell | refused by platform | suite (described); **manual** |
| The list file of `%f` is written before the start | same | same | suite |
| `Ins`, `F4`, `Del`, `Ctrl+↑`, `Ctrl+↓` edit the user menu popup | same | same | suite; probe on Windows; keys measured in P17.2 |
| A program that is not there is named, not thrown | `Win32Exception` 2 | `Win32Exception` 2 | **manual** |
| `Alt+R` asks which program opens the entry | same | same | suite |
| `Shift+Enter` reaches the panel as a key at all | **no**, arrives as `Enter` | **manual** | manual (Windows Terminal) |
| `Alt+R` on a directory opens the file manager | Explorer | the desktop's own | **manual** |
| `Alt+Shift+R` runs the entry elevated | same | same | suite |
| `Ctrl+Alt+Enter` reaches the panel as a key at all | **no** | **manual** | manual (Windows Terminal) |
| An executable starts with administrator rights | `runas`, the system prompts | refused by platform | **manual** |
| A prompt the user turns down leaves no trace | `Win32Exception` 1223, silence | n/a | **manual** |
| A document refuses to be run elevated | same | same | suite |
| The handlers of a common extension are listed | registry | XDG mime database | Windows: **manual**; Unix: suite |
| An extension the system knows nothing about lists nothing | same | same | Windows: **manual**; Unix: suite |

**The terminal handed over.** On Windows `F4` has not waited for the editor
since P20.1: it goes through the shell verb, so `notepad` opens its own window
and a console editor its own console, and the panels stay live behind both. On
Unix there is no window to hand a console editor, and since P21.5 `F4` hands it
the terminal instead: the driver is stopped, the editor runs and is waited for,
and the driver is built again inside the same `IApplication` and draws the
whole screen. How, and why it reaches past the public API, is in
[File operations](FILE-OPERATIONS.md). A probe drove the real binary in tmux on
WSL (Ubuntu 24.04) with `EDITOR=vim`: a line added and saved, the window
resized while `vim` held it, the panels back at the new size with the file's
new length, a second `F4`, and an editor that is not there named in a dialog
over whole panels. A person at a Linux desktop has not run it yet; the row
stays *manual* for P21.16.

**`Shift+Enter` was tried and does not arrive.** Under Windows Terminal the
modifier is dropped and the panel is handed a bare carriage return, which the
list reads as `Accepting` — so the key *runs* the entry instead of asking what
should open it. That is worse than a dead key, so `Alt+R` was added beside it
and is the binding named everywhere: `openWithKeys` holds both, exactly as
`hiddenKeys` holds `Alt+H` beside the `Ctrl+H` no terminal delivers either. The
plan foresaw this — a modified `Enter` is what it calls the least portable thing
phase 10 asks for — and P10.7 is where the remaining two commands get the same
treatment.

**`Ctrl+PgDn` arrives, `Ctrl+Alt+Enter` does not.** Both were tried by hand
under Windows Terminal on 2026-09-04: the walk into a directory or an archive
answers to `Ctrl+PgDn`, and nothing at all happens on `Ctrl+Alt+Enter` —
`Alt+Enter` is the terminal's own full-screen toggle, and the combination with
`Ctrl` does not reach the application either. That is why P10.7 gave both
commands a letter of their own, `Alt+W` and `Alt+Shift+R`, and left the older
names bound beside them: a letter with `Alt` is the one shape of modified key
every terminal in the checklist has been seen to deliver.

**The shifted letter was checked against the drivers, not only against the
suite.** `Alt+Shift+R` asks for a letter *and* a shift, and a driver that
reported the uppercase rune without the shift flag would hand the panel `Alt+R`
instead — the open-with popup rather than any rights raised, the same shape of
silent wrong answer `Shift+Enter` turned out to be. Terminal.Gui's two key
converters were asked directly, by reflection, outside any terminal: the
Windows one, which reads a console input record, and the net one, which reads a
`ConsoleKeyInfo`. Both spell `Alt+Shift+R` as the uppercase `R` with the shift
flag set and read it back as the same key, and both round-trip `Alt+W`, `Alt+R`
and `Alt+H` the same way. What is left manual is one step further out: whether
the terminal hands the driver those modifiers at all. `Alt+H` and `Alt+R` have
been doing so under Windows Terminal since P7.3 and P10.5, which is the whole
reason the letters were chosen.

**Administrator rights are a Windows-only answer, and Unix says so rather than
pretending.** On Windows `AsAdministrator` is one field — the `runas` verb on
the same shell execute every other start goes through — and the system, not
Marbas, does the asking; a prompt the user turns down comes back as
`Win32Exception` 1223 and becomes nothing at all on the screen, because refusing
is an answer and not a failure. Unix has no equivalent a full-screen terminal
application can ask for: `sudo`, `pkexec` and their kin either want a password
on the tty the panels are drawn on or want a desktop agent Marbas cannot count
on. `LaunchProcess` therefore refuses an `AsAdministrator` request before it
reaches a process and hands back the reason, which the same message box that
reports every other launch failure puts in front of the user. That refusal is
the one part of the elevated run the suite can prove on either platform, since
the platform is an argument rather than something read at the call.

**Which programs open a file is a question with two entirely different
answers.** Windows keeps them in the registry: `HKCU\…\FileExts\.ext\UserChoice`
is what the user picked, `HKCR\.ext` its default ProgId, `OpenWithProgids` under
both roots the rest, and `OpenWithList` the per-user open-with names — every one
of them resolved through a `shell\open\command` value. Unix keeps them in files:
`mime/globs` under the XDG data directories turns the extension into a MIME
type, `mimeapps.list` under the config directories turns that into `.desktop`
identifiers, and the `.desktop` files under `<data>/applications` say what each
one is called and what it runs. The two adapters therefore share only the type
they answer with and the command-line parser at the bottom, and neither reports
a failure: a platform that knows nothing answers an empty list.

**`Microsoft.Win32.Registry` needed no package after all.** The registry types
are in the shared framework on a plain `net10.0` target — they compile and load
on Linux and throw there if called, which nothing does, because
`Dependencies.handlersFor` picks the adapter by platform. The project therefore
still builds on one framework for both systems with no Windows-flavoured target
and no extra dependency in the single-file publish.

**Starting a program is where the two systems differ most.** Windows has one
answer for almost everything — `ShellExecuteEx`, which `UseShellExecute`
reaches — and it gives a console program a console of its own, so nothing it
prints can reach the panels; `runas` is the same call with the verb set, and it
is the only way a terminal application asks for administrator rights without a
password prompt on a screen it does not own. A window program is the
exception: the verb gives it no console, an Electron program then attaches to
Marbas's own, and under ConEmu (cmder) a fresh VS Code wrote its log over the
panels, while under Windows Terminal and conhost it wrote nothing. Since P21.6
a window program named by a start is resolved to its file and started without
the verb, its streams drained, as
[File operations](FILE-OPERATIONS.md) describes; a probe under ConEmu ran a
user menu entry `code.exe <file>` through the real adapter with a fresh VS Code
and found nothing in the console but what the probe wrote itself. Unix has no such verb. An executable entry is
started directly with both of its streams redirected and drained, because it
would otherwise inherit the terminal the panels are drawn on, and anything else
goes to `xdg-open`, which is the desktop's own idea of what opens a file. The
first launch on either system is what the three rows above check by hand; the
adapter itself is out of the suite's reach, like every other one.

**Attributes are not the same bits everywhere.** The properties popup offers
read-only, hidden, system and archive because that is what `FileAttributes`
carries; on Linux only the read-only bit maps onto anything real (the write
permission), and .NET reports hidden for a name starting with a dot rather than
for a bit that can be set. `EntryProperties.write` folds the four flags onto the
attributes the entry already has and swallows nothing — a platform that refuses
one is reported as an operation failure — but *what* a tick does on Linux is a
manual row, since `MockFileSystem` accepts every flag the tests set.

## Decisions

**The roots a panel can be sent to are a value, not a platform check.**
`Drives.list` used to call `DriveInfo.GetDrives()` everywhere, which on Unix
meant a list of mount points — and no way at all to reach the home directory,
though P3.5 had asked for `/` and `~`. For a while it was `Roots.list fs choice`,
with `RootChoice` being `MountedDrives` or `RootAndHome home`, answering bare
paths. P13.12 made it `Drives.list fs home` again, answering `DriveRow`s — a
`Drive` with its label, kind and sizes, or the home directory — once the mounts
that are not storage were filtered out and the mount list became worth showing
on Unix too. `Dependencies.create` still decides whether there is a home row,
so neither platform is a special case below the composition root. The key keeps
its vocabulary — `RootRequested`, `PromptForRoot`, `leftRootKey` — while the
popup, since P13.13, is titled *Drives* and shows each row with its sizes.

## Differences that are meant to be there

- **The modifier row is Windows-only.** Holding `Ctrl` or `Alt` swaps the bottom
  bar for the commands that modifier reaches. It is polled through
  `GetAsyncKeyState`, a `user32.dll` import that is never reached off Windows —
  `Dependencies.create` binds `ModifierWatch.unsupported` there, so the P/Invoke
  is never bound, which the WSL run confirms by not crashing. No Unix driver in
  Terminal.Gui 2.4.17 reports a standalone modifier outside the kitty keyboard
  protocol, so there is nothing to poll.
- **`Ctrl+H` is `0x08` on a VT terminal**, the byte backspace already owns, so it
  cannot be told apart from `Backspace` outside the kitty protocol. `Alt+H`
  carries the label on the bar for exactly this reason.
- **Drive letters do not exist on Unix**, so the tests over them skip there
  rather than pretending. `MockFileSystem` emulates drives Windows-style and answers a
  duplicated `/:\` on Linux, which is a second reason not to assert on it.
- **A junction exists on Windows alone.** The `Alt+F6` popup on Unix offers the
  symbolic and the hard link and no junction row at all, because
  `NativeLinks.forPlatform` supplies no function to make one.
- **Windows makes a symbolic link only for an administrator or with Developer
  Mode on.** Linux lets any user make one. The refusal is mapped to a sentence
  saying which of the two to switch on, rather than passed through as an access
  error.
- **Dialogs and message boxes keep Terminal.Gui's own theme** on both
  platforms; the main screen and the viewer wear the classic colours.
- **Only the home trash is written on Unix.** The freedesktop spec puts a trash
  on every volume (`$topdir/.Trash-$uid`) so that deleting never crosses a
  device; `Trash.send` always writes `$XDG_DATA_HOME/Trash` and lets
  `Operations.moveToPath` copy the entry over when the volume differs. It ends up
  in the right place either way — it is slower for a large tree on another
  disk, and the desktop's *Restore* still works, because the original path is
  recorded in the `.trashinfo` file.
- **macOS would get the freedesktop trash, not `~/.Trash`.** Nothing publishes
  a macOS build today; when something does, the Finder trash is one more
  implementation of the same port.

## Running the checks

```
dotnet test Marbas.slnx          # both legs of the table marked "suite"
dotnet run --project Marbas      # everything marked "manual"
```

Before a release, run the *manual* rows against the published binary rather than
`dotnet run` — it is a different build (self-contained, single-file,
ready-to-run), and it is what a user gets:

```
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=win-x64
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=linux-x64
```

See [PACKAGING.md](PACKAGING.md) for what those profiles decide and how a tag
turns them into a release.

Under WSL, build outputs are best kept away from the Windows ones:

```
dotnet test Marbas.slnx -p:ArtifactsPath=/tmp/marbas
```

### Driving the application without a person

The **WSL** rows above were checked by running Marbas in a pseudo-terminal and
reading the screen back, which takes three tricks worth writing down:

```sh
script -qec "stty rows 40 cols 120; dotnet run --project …" /dev/null \
  < <(sleep 6; printf "\033[8;40;120t\033[40;120R\033[?1;2c"; sleep 8; …) > run.log
```

`script` gives the process a pty it will draw into. `stty` gives that pty a
size, without which nothing is drawn at all. And the `printf` answers the
queries Terminal.Gui sends on startup — window size (`\e[18t`), cursor position
(`\e[6n`) and device attributes (`\e[0c`); a real terminal replies to those, a
bare pty does not, and until they are answered the driver renders an empty
screen. Strip the escapes from the log with `sed -r "s/\x1b\[[0-9;?]*[a-zA-Z]//g"`
and the panel contents are readable text.

Keys go in as their terminal sequences: `\033[21~` is `F10`, `\021` is `Ctrl+Q`,
`\033h` is `Alt+H`, and `Alt+F1` is `\033[1;3P` — *not* `\033\033OP`, which the
driver ignores. Whether the session file appeared afterwards is the cheapest
proof that a key reached the application at all, since quitting is what writes
it.
