# After the first plan

Eight rounds of work between the original plan, which closed with P8.3, and the
ninth phase.

**Since the plan closed, one round of panel work** answered five things a user
met straight away: the cursor no longer falls out of a panel at its last entry,
the left and right arrows jump by page instead of switching panels, entries are
coloured by what they are, the bottom row of a panel is separated by a rule and
says what the directory holds, and typing a letter searches the panel. **A
second round widened marking**: holding `Shift` and running an arrow marks or
unmarks an entry per step, in the direction of the arrow, and a shifted page
key does the same to the whole page at once. **A third round fixed two things
about names carrying an underscore**: they now sort where a file manager puts
them — first, ahead of the letters — and they are drawn with the underscore in
them, which Terminal.Gui had been reading as a hot key marker. **A fourth round
rebuilt the file viewer.** It had buffered the whole file, capped it at a
megabyte, called every UTF-16 file a binary, taken the page keys but not the
arrows, and rested on a `TextView` that 2.4.17 deprecated. It is now a
frameless full-screen window that reads only the rows it shows, over a file held
open and a line index scanned forward on demand; it settles the encoding before
deciding anything, always opens as text, and puts wrapping and the hex dump on
`F2` and `F4`. **A fifth round took the popups in hand.** They wore
Terminal.Gui's own defaults — a heavy frame and brackets in glyphs a console
font does not carry, and a shadow under each button that pushed the buttons past
the frame and squeezed the prompt's own label and input field off the dialog.
Every popup now wears a double frame, centred buttons, no shadow and the
classic colours, message boxes included; `Esc` closes any of them again, which
the quit key had quietly taken away; and `F7` opens on the name of the last
directory made, remembered across a restart. **A sixth round widened `F4`.** It
had one editor, `$EDITOR` or `notepad`, and it did nothing at all on a
directory. There is now a default editor and an editor per extension, both kept
in the settings file and both edited from a settings popup on `F12`;
`Shift+F4` creates a file in the panel's directory and opens it in the editor
its extension asks for; and `F4` on a directory opens a properties popup — the
read-only, hidden, system and archive flags and the three timestamps — writing
back only what it was given and re-reading the panel afterwards. The same round
moved every timestamp in the app from `DateTime` to `DateTimeOffset`, so a
moment always says which offset it was read in. **A seventh round was
housekeeping.** `Ui` had grown to forty-three modules in one folder, so they
were split into eight — `Theme`, `Format`, `Keys`, `Messages`, `Panels`,
`Viewer`, `Dialogs` and `App` — each folder a namespace, with the tests
following into the same folders on both sides of the unit/integration fence.
Nothing the application does changed: the compile order simply became the
folder order, and every dependency inside `Ui` now points at a folder above it.

**An eighth round emptied `Program.fs`.** It had become initialisation, the
wiring of the dependencies, the implementations of those dependencies and four
separate questions about which operating system this is. The real
implementations of `Shell.Dependencies` now live in `Adapters/` — the prompts,
the recycle bin, the editor process, the batch runner and the modifier poll —
the platform and the environment are read once into `HostEnvironment` and
carried as a value, and `Dependencies.create` is the single place where a
Windows function or a Unix one is chosen per port. `Program.fs` is down to
starting the application. Nothing the application does changed.
