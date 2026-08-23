---
name: render-tui-screen
description: Render Marbas screens — dialogs, message boxes, panels, the viewer — into text and per-cell colours, and drive them with synthetic keys, without a terminal. Use when a change is about how something looks (frames, shadows, glyphs, alignment, colours), when a Terminal.Gui default has to be measured rather than guessed, when comparing before and after a visual fix, or when a keystroke has to be proved end to end through a running Application.
---

# Rendering a Marbas screen

Unit tests reach a view's properties, never its pixels. This renders the real
screen into text, so the look can be read instead of assumed.

## Quick start

```bash
S="$SCRATCHPAD/probe"                     # scratchpad dir from the system prompt
mkdir -p "$S" && cp .claude/skills/render-tui-screen/probe/* "$S"
cd "$S" && timeout 120 dotnet run -- prompt
```

`probe/Program.fs` is a template — edit the `scene` function for what is being
looked at. It prints the screen as text and, per row, the runs of equal
`Attribute` (foreground, background, style).

## What the harness has to do, and why

- **Reference the real modules.** `probe.fsproj` has a `ProjectReference` to
  `Marbas.fsproj`, so scenes call `PromptDialog.create`, `DialogStyle.apply`,
  `MainWindow.create` — the code that ships, not a copy of it.
- **Keep it out of the repo.** Build it in the scratchpad. Inside the tree it
  would inherit `Directory.Packages.props` and clutter `git status`.
- **Set `app.Screen` by reflection.** `Application.Create().Init()` works with a
  piped stdout, but the screen measures 0x0, and anything sized with `Dim.Auto`
  — `MessageBox` above all — then throws `ArgumentOutOfRangeException: width`.
  The template forces 100x24.
- **Snapshot from a timeout, not after `Run`.** `app.AddTimeout` fires inside
  the loop, where the screen is drawn; `app.RequestStop()` ends the scene.
  `app.StopAfterFirstIteration <- true` is enough for a still picture of a
  dialog with no input.
- **`timeout 120 dotnet run`.** A scene that never stops would hang the session.

## Reading the screen

- `app.Driver.ToString()` — the whole screen as text, one line per row.
- `app.Driver.Contents[row, col]` — a `Cell`: `Seq.head cell.Runes` for the
  character, `cell.Attribute` for the colours. Which `VisualRole` a view draws
  with is answered by giving every role its own colour and reading back which
  one landed where. That is how selected text turned out to be `Active`.

## Driving it

`app.Keyboard.RaiseKeyDownEvent (Key 'n')` from a timeout types into whatever
holds focus; queue several timeouts for a sequence and read the result after
`app.Run` returns (`PromptDialog.text dialog`, `dialog.Canceled`, a flag set by
an injected port). Keys go through the whole application, so this proves what
no view-level test can: that `Esc` closes a popup and does not quit the app.

## Comparing before and after

Take the shot with the change switched off (skip `DialogStyle.apply`, keep the
old value), then with it on, and put the two blocks of text side by side. That
comparison is what settles a look; paste it into the answer rather than
describing it.
