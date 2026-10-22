module Marbas.Tests.Ui.Help.HelpColoursTests

open Xunit
open Terminal.Gui.Drawing
open Marbas.Guide
open Marbas.Ui.Help
open Marbas.Ui.Theme
open Marbas.Tests

let private piece look =
    { Text = "x"
      Look = look
      Stress = Set.empty
      Link = None }

let private linked index =
    { piece Plain with
        Link =
            Some
                { Index = index
                  Target = SameChapter(Anchor.create "sorting") } }

[<Fact>]
let ``plain text is the help's text`` () =
    let colour = HelpColours.attributeOf None (piece Plain)

    Assert.Equal(ClassicScheme.helpText, colour)

[<Fact>]
let ``inline code has a colour of its own`` () =
    let colour = HelpColours.attributeOf None (piece Literal)

    Assert.Equal(ClassicScheme.helpCode, colour)

[<Fact>]
let ``a heading is the help's title`` () =
    let colour = HelpColours.attributeOf None (piece (Title Section))

    Assert.Equal(ClassicScheme.helpTitle, colour)

[<Fact>]
let ``the rules of a table are the help's ruling`` () =
    let colour = HelpColours.attributeOf None (piece Ruling)

    Assert.Equal(ClassicScheme.helpRuling, colour)

[<Fact>]
let ``the frame of a note is the note's colour`` () =
    let colour = HelpColours.attributeOf None (piece (Framing Note))

    Assert.Equal(ClassicScheme.helpNote, colour)

[<Fact>]
let ``the frame of a warning is the warning's colour`` () =
    let colour = HelpColours.attributeOf None (piece (Framing Warning))

    Assert.Equal(ClassicScheme.helpWarning, colour)

[<Fact>]
let ``a link is drawn in a colour of its own`` () =
    let colour = HelpColours.attributeOf (Some 1) (linked 0)

    Assert.Equal(ClassicScheme.helpLink, colour)

[<Fact>]
let ``the chosen link is drawn as chosen`` () =
    let colour = HelpColours.attributeOf (Some 1) (linked 1)

    Assert.Equal(ClassicScheme.chosenLink, colour)

[<Fact>]
let ``strong text is bold`` () =
    let colour =
        HelpColours.attributeOf
            None
            { piece Plain with
                Stress = set [ Bold ] }

    Assert.Equal(TextStyle.Bold, colour.Style)

[<Fact>]
let ``emphasised text in a link is italic and underlined`` () =
    let colour =
        HelpColours.attributeOf
            None
            { linked 0 with
                Stress = set [ Italic ] }

    Assert.Equal(TextStyle.Italic ||| TextStyle.Underline, colour.Style)
