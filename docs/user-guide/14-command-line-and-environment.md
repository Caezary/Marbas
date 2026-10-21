# The command line and the environment

Marbas takes two switches on its command line and reads a few environment
variables. A variable that is not set, or set to nothing, counts as not set.

## Switches

| Switch | Does |
|--------|------|
| `--version` | prints `Marbas` and its version, then exits without opening the panels |
| `--debug` | makes `marbas.log` record what happens in this session |

- With `--debug` the log records every command a key raised and every action
  taken, by name only, every batch with how many items it went through, and
  every error shown, in the words it was shown with.
- No file contents, no passwords and no clipboard text are written.
- The switch lasts one session. Without it, the log records only crashes and
  the leftovers of an earlier session that could not be removed. Where the log
  is, is in [The files Marbas keeps](15-files-marbas-keeps.md#the-log).

## Variables

| Variable | Read for |
|----------|----------|
| `EDITOR` | the editor `F4` starts when Setup names none |
| `PATH` | the *PATH* search of `Alt+F7`; on Windows, the program a bare name such as `code` stands for |
| `PATHEXT` | on Windows, the extensions a bare name is tried with; `.COM;.EXE;.BAT;.CMD` when not set |
| `XDG_CONFIG_HOME` | on Linux, the directory holding `Marbas`, and the first place of `mimeapps.list`; `~/.config` when not set |
| `XDG_CONFIG_DIRS` | on Linux, the further places of `mimeapps.list`; `/etc/xdg` when not set |
| `XDG_DATA_HOME` | on Linux, the trash and the first place of the programs `Alt+R` offers; `~/.local/share` when not set |
| `XDG_DATA_DIRS` | on Linux, the further places of those programs and the MIME types; `/usr/local/share:/usr/share` when not set |
| `TMPDIR` | on Linux, where the working directory is made; `/tmp` when not set |
| `TMP`, `TEMP` | on Windows, where the working directory is made |

- `PATH` is split at `;` on Windows and at `:` on Linux. A directory named
  twice is searched once.
- On Windows Marbas looks a bare program name up to tell a window program
  from a console one and a `.cmd` or `.bat` from both, so that a window
  program cannot write over the panels.
- What lives where these variables point is in
  [The files Marbas keeps](15-files-marbas-keeps.md).
