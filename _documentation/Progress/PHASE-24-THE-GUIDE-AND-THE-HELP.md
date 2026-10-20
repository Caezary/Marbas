# Phase 24 — The guide and the help

The tasks are in
[Phase 24 — The guide and the help](../Plan/PHASE-24-THE-GUIDE-AND-THE-HELP.md).

**P24.1 F1 needs no spare.** `_probes/KeyProbe` got a `help` round: `F1`,
`Ctrl+F1`, `Alt+S` and `Alt+Y` in a panel, in a full-screen window, in a popup
holding a list and in a popup holding a text field, and `Tab`, `Shift+Tab`,
`Backspace` and `Alt+←` in the full-screen window. `Tab` being measured, the
round moves on with `Enter` and back with `Ctrl+P`. A person ran it on
2026-10-21 in cmder, Windows Terminal, the Windows console, WSL and
gnome-terminal 3.52.0 under WSLg, and every key arrived in all five. The plan
expected gnome-terminal to keep `F1` for its own help; 3.52.0 binds no key to
it, and GNOME's window manager binds only `<Alt>F1`. The rows are in
[Cross-platform](../Architecture/CROSS-PLATFORM.md#keyboard-and-terminal), the
key table of the help in [Help](../Architecture/HELP.md).

Those runs showed a defect of the probe: it kept what arrived under the name
of the key alone, so `F1` pressed in the panel stood in the report of every
popup after it. It now keeps it per place. Which view receives a key is decided
inside Terminal.Gui, not by the terminal, so the places were proved with the
fixed probe in tmux under WSL, one key left unpressed in one popup to see it
reported as nothing there and as arrived in the next.

A spike rendered a chapter in Terminal.Gui's own `Markdown` view at 40, 80
and 120 columns. Its links, `LinkClicked` and GitHub anchors work; its tables
lose rows, its alerts lose their kind, and it cannot show a match. The help
keeps a view of its own, as decided; the findings are in
[Help](../Architecture/HELP.md#the-view-is-marbass-own).
