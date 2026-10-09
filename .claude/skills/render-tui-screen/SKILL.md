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
P=.claude/skills/render-tui-screen/probe
mkdir -p "$S" && cp "$P/Program.fs" "$P/probe.fsproj" "$S"
cd "$S" && timeout 120 dotnet run -- prompt
```

`probe/Program.fs` is a template — edit the `scene` function for what is being
looked at. It prints the screen as text and, per row, the runs of equal
`Attribute` (foreground, background, style).

## What the harness has to do, and why

- **Reference the real modules.** `probe.fsproj` has a `ProjectReference` to
  `Marbas.fsproj`, so scenes call `PromptDialog.create`, `DialogStyle.apply`,
  `MainWindow.create` — the code that ships, not a copy of it.
- **Keep it out of the repo.** Build it in the scratchpad, and copy the two
  template files by name: a `bin/` or `obj/` left in the skill folder by a build
  there makes `cp probe/*` fail, and the `&&` after it then skips the run.
- **Take the package versions from the repo.** `Marbas.dll` is compiled against
  the repo's `FSharp.Core`; a probe on the SDK's implicit, older one dies at the
  first F# call with `Could not load file or assembly 'FSharp.Core, Version=…'`.
  So `probe.fsproj` imports the SDK by hand, pointing `DirectoryPackagesPropsPath`
  at the repo's `Directory.Packages.props` before `Sdk.props` reads it — an
  `Import` in the project body comes too late for central package management —
  turns off the implicit `FSharp.Core` and references it and `Terminal.Gui`
  without versions. A version bump in the repo reaches the probe on its own.
  `MarbasRoot` at the top is the one path to change on another machine.
- **Set `app.Screen` by reflection.** `Application.Create().Init()` works with a
  piped stdout, but the screen measures 0x0, and anything sized with `Dim.Auto`
  — `MessageBox` above all — then throws `ArgumentOutOfRangeException: width`.
  The template forces 100x24.
- **Set the size again right before the shot.** Once the loop runs, the size
  monitor puts `app.Screen` back to the console's own size (120x30 here) on
  every iteration, and `Driver.SetScreenSize` fares no better. To see a screen
  of a chosen width — an eighty-column terminal, a wide one — set
  `app.Screen <- Rectangle(0, 0, width, height)` and call
  `app.LayoutAndDraw true` in the same timeout that takes the shot.
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

Pitfalls measured in P18.6 — the probe's, not the application's:

- **The same key twice in a row arrives once.** `Ins Ins` from two timeouts
  200 ms apart marks one entry, and so does `Space Space`. Alternate the keys
  (`Ins` then `Space`) or put another key between them.
- **A progress window opened from a timeout never finishes its batch.** A key
  that starts `Runners.Batch` — `F5` out of an archive, `Alt+Shift+C` inside
  one — opens the window, and the work never comes back. Swap only the runner
  for the synchronous one the tests use,
  `Runners.Batch = fun _ work finish -> work Operations.unattended |> finish`,
  and leave the file system and the other ports real. When the real runner is
  the point — timing a batch, watching its window — start it from the loop's
  `app.Iteration` event (once, behind a flag) instead of a timeout: the batch
  then finishes and `BatchRunner.run` returns, as measured in P21.3.
- **`app.RequestStop()` stops the top runnable, not the scene.** With a
  progress window open it closes that window, the batch reports itself
  stopped, and the main window keeps the probe alive. End a scene over
  `MainWindow` with `app.RequestStop window`.
- **The probe needs a console.** Run from PowerShell's `Start-Job` it hangs
  without drawing; call the executable directly, under `timeout`.

## Comparing before and after

Take the shot with the change switched off (skip `DialogStyle.apply`, keep the
old value), then with it on, and put the two blocks of text side by side. That
comparison is what settles a look; paste it into the answer rather than
describing it.
