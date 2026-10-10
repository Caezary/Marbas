module Marbas.Adapters.ModifierWatch

open System
open System.Runtime.InteropServices
open Terminal.Gui.App
open Marbas.Domain
open Marbas.State
open Marbas.Ui.Keys

[<Literal>]
let PollInterval = 100.0

[<Literal>]
let private VkShift = 0x10

[<Literal>]
let private VkControl = 0x11

[<Literal>]
let private VkAlt = 0x12

[<DllImport "user32.dll">]
extern int16 private GetAsyncKeyState(int vKey)

let private isDown vKey = GetAsyncKeyState vKey &&& 0x8000s <> 0s

let private held () =
    [ if isDown VkControl then
          Ctrl
      if isDown VkAlt then
          Alt
      if isDown VkShift then
          Shift ]
    |> Modifiers.held

let poll (app: IApplication) (watch: Ports.ModifiersWatch) =
    let reported = ref Modifiers.none

    app.AddTimeout(
        TimeSpan.FromMilliseconds PollInterval,
        fun () ->
            let current = held ()

            if current <> reported.Value then
                reported.Value <- current
                watch.Report current

            true
    )
    |> ignore

let unsupported (_: Ports.ModifiersWatch) = ()
