module Marbas.Tests.Ui.ClassicSchemeTests

open System
open Xunit
open Terminal.Gui.Drawing
open Marbas.Ui

let private attribute (foreground: ColorName16) (background: ColorName16) =
    let mutable foreground = foreground
    let mutable background = background
    Attribute(&foreground, &background)

let private chosen (scheme: Scheme) =
    Enum.GetValues<VisualRole>()
    |> Array.choose (fun role ->
        match scheme.TryGetExplicitlySetAttributeForRole role with
        | true, attribute -> Some attribute.Value
        | _ -> None)
    |> List.ofArray

let private isAnsi16 (colour: Color) =
    let mutable name = colour.GetClosestNamedColor16()
    colour = Color(&name)

[<Fact>]
let ``panel entries are bright cyan on blue`` () =
    let normal = ClassicScheme.panel.Normal

    Assert.Equal(attribute ColorName16.BrightCyan ColorName16.Blue, normal)

[<Fact>]
let ``the cursor bar is black on cyan`` () =
    let focus = ClassicScheme.panel.Focus

    Assert.Equal(attribute ColorName16.Black ColorName16.Cyan, focus)

[<Fact>]
let ``marked entries are bright yellow on blue`` () =
    let highlight = ClassicScheme.panel.Highlight

    Assert.Equal(attribute ColorName16.BrightYellow ColorName16.Blue, highlight)

[<Fact>]
let ``the marked summary is bright yellow on blue`` () =
    let normal = ClassicScheme.summary.Normal

    Assert.Equal(attribute ColorName16.BrightYellow ColorName16.Blue, normal)

[<Fact>]
let ``the screen behind the panels is light grey on black`` () =
    let normal = ClassicScheme.screen.Normal

    Assert.Equal(attribute ColorName16.Gray ColorName16.Black, normal)

[<Fact>]
let ``the function key bar is black on cyan`` () =
    let normal = ClassicScheme.keyBar.Normal

    Assert.Equal(attribute ColorName16.Black ColorName16.Cyan, normal)

[<Fact>]
let ``a key name on the bar is white on black, so it parts from its command`` () =
    let normal = ClassicScheme.keyName.Normal

    Assert.Equal(attribute ColorName16.White ColorName16.Black, normal)

[<Fact>]
let ``a key name and its command do not share a background`` () =
    let name = ClassicScheme.keyName.Normal
    let command = ClassicScheme.keyBar.Normal

    Assert.NotEqual(command.Background, name.Background)

[<Fact>]
let ``every chosen colour is one of the sixteen ansi colours`` () =
    let schemes =
        [ ClassicScheme.screen
          ClassicScheme.panel
          ClassicScheme.summary
          ClassicScheme.keyBar
          ClassicScheme.keyName ]

    let colours =
        schemes
        |> List.collect chosen
        |> List.collect (fun attribute -> [ attribute.Foreground; attribute.Background ])

    Assert.NotEmpty colours
    Assert.All(colours, fun colour -> Assert.True(isAnsi16 colour, string colour))
