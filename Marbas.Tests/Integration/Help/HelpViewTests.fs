module Marbas.Tests.Integration.Help.HelpViewTests

open System.Drawing
open Xunit
open Terminal.Gui.Drawing
open Terminal.Gui.Input
open Terminal.Gui.Views
open Marbas.Guide
open Marbas.Ui.Keys
open Marbas.Ui.Help
open Marbas.Tests
open Marbas.Tests.Guide
open Marbas.Tests.Integration.Gui

let private name raw =
    ChapterName.tryCreate raw |> Expect.some

let private quietPorts: HelpView.Ports =
    { Close = ignore
      Read = EmbeddedGuide.read
      Report = ignore }

let private showing ports (size: Size) =
    let window = HelpView.create ports (UserGuide.chapterOf "README.md")
    window.BeginInit()
    window.EndInit()
    window.Layout(size) |> ignore
    window

let private laidOut () = showing quietPorts (Size(80, 24))

let private chapterShown window =
    (HelpView.content window).State.Page.Chapter.Name

[<Fact>]
let ``the help wears no border`` () =
    use window = laidOut ()

    Assert.Equal(LineStyle.None, window.BorderStyle.Value)

[<Fact>]
let ``a header, the content and a key bar divide the whole screen between them`` () =
    use window = laidOut ()

    let header = HelpView.header window
    let content = (HelpView.content window :> Terminal.Gui.ViewBase.View)
    let keyBar = HelpView.keyBar window

    Assert.Equal(0, header.Frame.Y)
    Assert.Equal(1, header.Frame.Height)
    Assert.Equal(1, content.Frame.Y)
    Assert.Equal(22, content.Frame.Height)
    Assert.Equal(23, keyBar.Frame.Y)

[<Fact>]
let ``the header names the chapter shown`` () =
    use window = laidOut ()

    let shown = drawnText (HelpView.header window)

    Assert.Contains("Marbas user guide", shown)

[<Fact>]
let ``the key bar says F10 quits`` () =
    use window = laidOut ()

    let labels =
        FunctionKeyBar.slots (HelpView.keyBar window) |> List.map _.CommandName.Text

    Assert.Equal("Quit", labels[9])

[<Fact>]
let ``the help opens on the contents`` () =
    use window = laidOut ()

    let rows = (HelpView.content window).Rows()

    Assert.Equal("Marbas user guide", List.head rows)
    Assert.Equal(22, List.length rows)

[<Fact>]
let ``the content keeps Tab for itself`` () =
    use window = laidOut ()
    let content = HelpView.content window

    let sent = pressReturning content Key.Tab

    Assert.True(sent.Handled)
    Assert.Equal(Some 0, content.State.Page.Link)

[<Fact>]
let ``Enter follows the chosen link`` () =
    use window = laidOut ()
    let content = HelpView.content window
    press content Key.Tab

    press content Key.Enter

    Assert.Equal(name "16-platforms.md", chapterShown window)

[<Fact>]
let ``the header follows the chapter`` () =
    use window = laidOut ()
    let content = HelpView.content window
    press content Key.Tab

    press content Key.Enter

    Assert.Contains("Differences between platforms", drawnText (HelpView.header window))

[<Fact>]
let ``Backspace goes back to the contents`` () =
    use window = laidOut ()
    let content = HelpView.content window
    press content Key.Tab
    press content Key.Enter

    press content Key.Backspace

    Assert.Equal(name "README.md", chapterShown window)

[<Fact>]
let ``Alt+← goes back to the contents`` () =
    use window = laidOut ()
    let content = HelpView.content window
    press content Key.Tab
    press content Key.Enter

    press content Key.CursorLeft.WithAlt

    Assert.Equal(name "README.md", chapterShown window)

[<Fact>]
let ``Esc closes the help`` () =
    let closed = ResizeArray<Window>()
    use window = showing { quietPorts with Close = closed.Add } (Size(80, 24))

    press (HelpView.content window) Key.Esc

    Assert.Same(window, Assert.Single closed)

[<Fact>]
let ``F10 closes the help`` () =
    let closed = ResizeArray<Window>()
    use window = showing { quietPorts with Close = closed.Add } (Size(80, 24))

    press (HelpView.content window) Key.F10

    Assert.Same(window, Assert.Single closed)

[<Fact>]
let ``a chapter that cannot be read is reported and the help stays`` () =
    let reported = ResizeArray<GuideFault>()

    use window =
        showing
            { quietPorts with
                Read = fun chapter -> Error(Absent chapter)
                Report = reported.Add }
            (Size(80, 24))

    let content = HelpView.content window
    press content Key.Tab

    press content Key.Enter

    Assert.Equal(Absent(name "16-platforms.md"), Assert.Single reported)
    Assert.Equal(name "README.md", chapterShown window)

[<Fact>]
let ``a narrower screen lays the chapter out again`` () =
    use window = laidOut ()

    window.Layout(Size(40, 24)) |> ignore

    Assert.Equal(40, (HelpView.content window).State.Page.Width)
