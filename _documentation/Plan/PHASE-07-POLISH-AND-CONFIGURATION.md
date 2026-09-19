# Phase 7 — Polish & configuration

Goal: comfort features that make it feel like a finished tool.

- **P7.1 Classic color scheme** — classic blue panels, cyan selection;
  respects terminals with limited colors. *Done when:* the app looks like the
  DOS-era two-panel managers it follows.
- **P7.2 Sorting options** — `Ctrl+F3..F6`-style toggles: name / extension /
  date / size, ascending & descending, per panel. *Done when:* sort mode is
  switchable and shown in the panel.
- **P7.3 Hidden files toggle** — show/hide dotfiles and hidden/system
  entries. *Done when:* toggle works on both Windows and Unix conventions.
- **P7.4 Persisted settings** — panel paths, sort modes, and toggles saved
  to a config file and restored on start. *Done when:* restart preserves the
  session.
- **P7.5 Modifier-aware key bar** — one table of every key binding, from which
  both the bottom bar and the key handler are built; the bar keeps a slot for
  every function key from `F1` to `F12`, evenly spread and with the key name in
  a background of its own, and holding a modifier swaps the commands on those
  slots for the ones that modifier reaches (`Ctrl` the sort keys, `Alt` the
  drive choosers). *Done when:* the bar follows the modifiers actually held and
  the key names stay put while it does.
