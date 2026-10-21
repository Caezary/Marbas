# Changelog

Each release of Marbas, newest first. A release's section is its description
on the [releases page](https://github.com/Caezary/Marbas/releases); how
versions are numbered is in
[Packaging](_documentation/Architecture/PACKAGING.md#numbering).

## 0.1.0 — 2026-10-20

The first release of Marbas: a two-panel file manager for the terminal,
driven by the keyboard, in the spirit of the orthodox managers of the DOS era.

**What it does**

- **Panels and tabs.** Each tab is a pair of panels. Sort by name,
  extension, date or size, show hidden entries, and see changes made by other
  programs without refreshing. The whole session comes back on the next start.
- **File operations.** Copy, move, rename, make directories, delete to the
  trash or for good, wipe, and make symbolic links, hard links and junctions.
  Long work runs in the background and `Esc` stops it.
- **Marking.** One by one, a page at a time, by wildcard, by extension or by
  name, or inverted, with one step of undo.
- **Archives.** zip, tar, tar.gz, tar.bz2, tar.xz, 7z and rar open like
  directories; zip, tar.gz and 7z can also be created and changed in place.
- **Viewer.** Text in a code page of your choice, or a hex dump; search by
  text, by regular expression or by bytes.
- **Finding files.** By name and by content, on disk and inside archives. The
  results can go into a panel and be copied, moved or deleted from there.
- **Running files.** `Enter` starts a file with its associated program, `Alt+R`
  picks another program, `F4` opens your editor, and `F2` is a menu of your own
  commands.
- **History and favourites.** The directories visited, files viewed and
  edited, commands run, and ten favourite places under `Alt+0` … `Alt+9`.
- **Clipboard** (Windows). Names, paths and the files themselves, to and from
  other programs.

**Running it**

Download the archive for your system, unpack it and run `Marbas`; on Linux,
`chmod +x Marbas` first. Each binary carries its own .NET runtime, so nothing
needs to be installed. Settings live in `%APPDATA%\Marbas` on Windows and in
`~/.config/Marbas` on Linux. The keys are in the
[user guide](https://github.com/Caezary/Marbas/blob/main/docs/user-guide/README.md).

| Archive | System |
|---------|--------|
| `marbas-0.1.0-win-x64.zip` | Windows, x64 |
| `marbas-0.1.0-linux-x64.tar.gz` | Linux, x64 |

**Known limits**

- There is no macOS build.
- The clipboard works on Windows only.
- Some terminals and desktops keep keys for themselves: `F11` under Windows
  Terminal, `Alt+F7` on GNOME, `Alt+F11` in gnome-terminal. Most such keys
  have a second binding, listed in the user guide.
- On Linux, `F4` hands the whole terminal to the editor until it closes.
- Everything else known to be wrong is listed in
  [Rough edges](https://github.com/Caezary/Marbas/blob/main/_documentation/ROUGH-EDGES.md).

If Marbas stops on an error, it writes what happened to `marbas.log` beside its
settings. Please attach that file to a bug report.
