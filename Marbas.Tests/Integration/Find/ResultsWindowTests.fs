module Marbas.Tests.Integration.Find.ResultsWindowTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.FileSystem.FileSearch
open Marbas.Ui.Find
open Marbas.Tests
open Marbas.Tests.Integration.Gui

let private at = Root.at

let private fp = FileSystemPath.create

let private moment = DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.Zero)

let private file name =
    { Name = EntryName.create name
      Kind = File
      Size = Some 1024L
      Measured = None
      Modified = moment
      Visibility = Plain
      Executability = NotExecutable
      LinkTarget = None }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let private counts files = { Files = files; Directories = 0; Skipped = 0 }

let private showing stop close =
    let window = ResultsWindow.create (MockFileSystem()) stop close
    window.BeginInit()
    window.EndInit()
    window.Layout(Size(80, 24)) |> ignore
    window

let private laidOut () = showing ignore ignore

let private lines window =
    let list = ResultsWindow.list window
    let source = list.Source.ToList()
    [ for index in 0 .. list.Source.Count - 1 -> source[index].ToString().TrimEnd() ]

let private summary window = (ResultsWindow.summary window).Text

[<Fact>]
let ``a window with nothing in it yet shows where the walk is`` () =
    use window = laidOut ()

    ResultsWindow.walking window (at "start")

    Assert.Equal(FindSummary.describe (FindSummary.Walking(at "start", counts 0)), summary window)

[<Fact>]
let ``the hits show grouped under their directory while the walk runs`` () =
    use window = laidOut ()

    ResultsWindow.add window [ hitIn "start" "a.txt"; hitIn "start/docs" "b.txt" ]

    let shown = lines window

    Assert.Equal(at "start", shown |> List.item 0)
    Assert.StartsWith("  a.txt", shown |> List.item 1)
    Assert.Equal(at "start/docs", shown |> List.item 2)
    Assert.StartsWith("  b.txt", shown |> List.item 3)

[<Fact>]
let ``the summary counts the hits as they arrive`` () =
    use window = laidOut ()
    ResultsWindow.walking window (at "start")

    ResultsWindow.add window [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ]

    Assert.Equal(FindSummary.describe (FindSummary.Walking(at "start", counts 2)), summary window)

[<Fact>]
let ``a finished walk says so with the counts it answered`` () =
    use window = laidOut ()
    ResultsWindow.add window [ hitIn "start" "a.txt" ]

    ResultsWindow.finish window (Finished { Files = 1; Directories = 0; Skipped = 2 })

    Assert.Equal("Finished — 1 file, 0 directories, 2 skipped", summary window)

[<Fact>]
let ``a stopped walk says so`` () =
    use window = laidOut ()

    ResultsWindow.finish window (Stopped(counts 0))

    Assert.Equal("Stopped — nothing found", summary window)

[<Fact>]
let ``the row under the cursor stays there when a hit joins an earlier group`` () =
    use window = laidOut ()
    ResultsWindow.add window [ hitIn "start" "a.txt"; hitIn "start/docs" "b.txt" ]
    (ResultsWindow.list window).SelectedItem <- 3

    ResultsWindow.add window [ hitIn "start" "c.txt" ]

    Assert.StartsWith("  b.txt", lines window |> List.item (ResultsWindow.list window).SelectedItem.Value)

[<Fact>]
let ``Esc stops a running walk and leaves the window up`` () =
    let stopped = ref 0
    let closed = ref 0
    use window = showing (fun () -> stopped.Value <- stopped.Value + 1) (fun _ -> closed.Value <- closed.Value + 1)
    ResultsWindow.walking window (at "start")

    press (ResultsWindow.list window) Key.Esc

    Assert.Equal(1, stopped.Value)
    Assert.Equal(0, closed.Value)

[<Fact>]
let ``a second Esc closes the window over what was found`` () =
    let stopped = ref 0
    let closed = ref 0
    use window = showing (fun () -> stopped.Value <- stopped.Value + 1) (fun _ -> closed.Value <- closed.Value + 1)
    ResultsWindow.walking window (at "start")
    press (ResultsWindow.list window) Key.Esc

    press (ResultsWindow.list window) Key.Esc

    Assert.Equal(1, stopped.Value)
    Assert.Equal(1, closed.Value)

[<Fact>]
let ``Esc closes a walk that finished on its own at once`` () =
    let stopped = ref 0
    let closed = ref 0
    use window = showing (fun () -> stopped.Value <- stopped.Value + 1) (fun _ -> closed.Value <- closed.Value + 1)
    ResultsWindow.finish window (Finished(counts 1))

    press (ResultsWindow.list window) Key.Esc

    Assert.Equal(0, stopped.Value)
    Assert.Equal(1, closed.Value)

[<Fact>]
let ``the list, the summary and the key bar divide the screen between them`` () =
    use window = laidOut ()

    let list = ResultsWindow.list window
    let line = ResultsWindow.summary window
    let bar = ResultsWindow.keyBar window

    Assert.Equal(0, list.Frame.Y)
    Assert.Equal(22, list.Frame.Height)
    Assert.Equal(22, line.Frame.Y)
    Assert.Equal(23, bar.Frame.Y)

[<Fact>]
let ``the key bar says which key leaves the results`` () =
    use window = laidOut ()

    let labels = Marbas.Ui.Keys.FunctionKeyBar.slots (ResultsWindow.keyBar window) |> List.map _.CommandName.Text

    Assert.Equal<string list>(FindKeys.bar, labels)
    Assert.Equal("Quit", labels |> List.item 9)
