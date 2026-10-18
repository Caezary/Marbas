module Marbas.Tests.Integration.Find.ResultsWindowTests

open System
open System.Drawing
open System.IO.Abstractions.TestingHelpers
open Xunit
open Terminal.Gui.Input
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Find
open Marbas.FileSystem.DirectoryListing
open Marbas.Find.FileSearch
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
      LinkTarget = None
      Verification = Verified }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = file name }

let private counts files =
    { Files = files
      Directories = 0
      Skipped = 0 }

let private arriving hits =
    hits
    |> List.mapi (fun index hit ->
        { Hit = hit
          Counts = counts (index + 1) })

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private ground =
    { Roots = [ OnDisk(fp (at "start")) ]
      OtherRoots = [] }

let private running view stop close =
    let window =
        ResultsWindow.create (MockFileSystem()) (Walk(query, ground)) view stop close

    window.BeginInit()
    window.EndInit()
    window.Layout(Size(80, 24)) |> ignore
    window

let private reopening results gone =
    let window =
        ResultsWindow.create (MockFileSystem()) (Reopen(results, gone)) ignore ignore ignore

    window.BeginInit()
    window.EndInit()
    window.Layout(Size(80, 24)) |> ignore
    window

let private watching view stop close = running view stop close

let private showing stop close = running ignore stop close

let private laidOut () = showing ignore ignore

let private lines window =
    let list = ResultsWindow.list window
    [ for item in (Expect.notNull list.Source).ToList() -> (string item).TrimEnd() ]

let private formatted window =
    let list = ResultsWindow.list window
    let source = (Expect.notNull list.Source).ToList()
    [ for index in 0 .. (Expect.notNull list.Source).Count - 1 -> source[index] ]

let private tickingOneByOne window hits =
    hits |> List.iter (fun hit -> ResultsWindow.add window (arriving [ hit ]))

let private nineMore =
    [ "b.txt"
      "c.txt"
      "d.txt"
      "e.txt"
      "f.txt"
      "g.txt"
      "h.txt"
      "i.txt"
      "j.txt" ]
    |> List.map (hitIn "start")

let private summary window = (ResultsWindow.summary window).Text

[<Fact>]
let ``a window with nothing in it yet shows where the walk is`` () =
    use window = laidOut ()

    ResultsWindow.walking window (at "start")

    Assert.Equal(FindSummary.describe (FindSummary.Walking(at "start", counts 0)), summary window)

[<Fact>]
let ``the hits show grouped under their directory while the walk runs`` () =
    use window = laidOut ()

    ResultsWindow.add window (arriving [ hitIn "start" "a.txt"; hitIn "start/docs" "b.txt" ])

    let shown = lines window

    Assert.Equal(at "start", shown |> List.item 0)
    Assert.StartsWith("  a.txt", shown |> List.item 1)
    Assert.Equal(at "start/docs", shown |> List.item 2)
    Assert.StartsWith("  b.txt", shown |> List.item 3)

[<Fact>]
let ``the summary counts the hits as they arrive`` () =
    use window = laidOut ()
    ResultsWindow.walking window (at "start")

    ResultsWindow.add window (arriving [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ])

    Assert.Equal(FindSummary.describe (FindSummary.Walking(at "start", counts 2)), summary window)

[<Fact>]
let ``a finished walk says so with the counts it answered`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])

    ResultsWindow.finish
        window
        (Finished
            { Files = 1
              Directories = 0
              Skipped = 2 })

    Assert.Equal("Finished — 1 file, 0 directories, 2 skipped", summary window)

[<Fact>]
let ``a stopped walk says so`` () =
    use window = laidOut ()

    ResultsWindow.finish window (Stopped(counts 0))

    Assert.Equal("Stopped — nothing found", summary window)

[<Fact>]
let ``the row under the cursor stays there while more hits arrive`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt"; hitIn "start/docs" "b.txt" ])
    (ResultsWindow.list window).SelectedItem <- 3

    ResultsWindow.add window (arriving [ hitIn "start" "c.txt" ])

    Assert.StartsWith("  b.txt", lines window |> List.item (ResultsWindow.list window).SelectedItem.Value)

[<Fact>]
let ``a walk coming back to a directory heads the run it comes back with`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt"; hitIn "start/docs" "b.txt" ])

    ResultsWindow.add window (arriving [ hitIn "start" "c.txt" ])

    let shown = lines window

    Assert.Equal(at "start", shown |> List.item 0)
    Assert.StartsWith("  a.txt", shown |> List.item 1)
    Assert.Equal(at "start/docs", shown |> List.item 2)
    Assert.StartsWith("  b.txt", shown |> List.item 3)
    Assert.Equal(at "start", shown |> List.item 4)
    Assert.StartsWith("  c.txt", shown |> List.item 5)

[<Fact>]
let ``the ticks after a hit do not format it again`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    let painted = formatted window |> List.item 1

    tickingOneByOne window nineMore

    Assert.Same(painted, formatted window |> List.item 1)
    Assert.Equal(11, (Expect.notNull (ResultsWindow.list window).Source).Count)

[<Fact>]
let ``a tick adds to the rows already shown rather than replacing them`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    let source = (ResultsWindow.list window).Source

    ResultsWindow.add window (arriving [ hitIn "start" "b.txt" ])

    Assert.Same(source, (ResultsWindow.list window).Source)

[<Fact>]
let ``a step of the walk leaves the rows alone`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    let source = (ResultsWindow.list window).Source

    ResultsWindow.walking window (at "start/docs")

    Assert.Same(source, (ResultsWindow.list window).Source)
    Assert.Equal(FindSummary.describe (FindSummary.Walking(at "start/docs", counts 1)), summary window)

[<Fact>]
let ``Esc stops a running walk and leaves the window up`` () =
    let stopped = ref 0
    let closed = ref 0

    use window =
        showing (fun () -> stopped.Value <- stopped.Value + 1) (fun _ -> closed.Value <- closed.Value + 1)

    ResultsWindow.walking window (at "start")

    press (ResultsWindow.list window) Key.Esc

    Assert.Equal(1, stopped.Value)
    Assert.Equal(0, closed.Value)

[<Fact>]
let ``a second Esc closes the window over what was found`` () =
    let stopped = ref 0
    let closed = ref 0

    use window =
        showing (fun () -> stopped.Value <- stopped.Value + 1) (fun _ -> closed.Value <- closed.Value + 1)

    ResultsWindow.walking window (at "start")
    press (ResultsWindow.list window) Key.Esc

    press (ResultsWindow.list window) Key.Esc

    Assert.Equal(1, stopped.Value)
    Assert.Equal(1, closed.Value)

[<Fact>]
let ``Esc closes a walk that finished on its own at once`` () =
    let stopped = ref 0
    let closed = ref 0

    use window =
        showing (fun () -> stopped.Value <- stopped.Value + 1) (fun _ -> closed.Value <- closed.Value + 1)

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

    let labels =
        Marbas.Ui.Keys.FunctionKeyBar.slots (ResultsWindow.keyBar window)
        |> List.map _.CommandName.Text

    Assert.Equal<string list>(FindKeys.bar, labels)
    Assert.Equal("Quit", labels |> List.item 9)

let private viewing (seen: ResizeArray<FindHit>) = watching seen.Add ignore ignore

let private directoryIn where name =
    { Location = OnDisk(fp (at where))
      Entry =
        { file name with
            Kind = Directory
            Size = None } }

[<Fact>]
let ``F3 on a file hit views it`` () =
    let seen = ResizeArray<FindHit>()
    use window = viewing seen
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    (ResultsWindow.list window).SelectedItem <- 1

    press (ResultsWindow.list window) Key.F3

    Assert.Equal<FindHit list>([ hitIn "start" "a.txt" ], List.ofSeq seen)

[<Fact>]
let ``F3 on a directory hit views nothing`` () =
    let seen = ResizeArray<FindHit>()
    use window = viewing seen
    ResultsWindow.add window (arriving [ directoryIn "start" "docs" ])
    (ResultsWindow.list window).SelectedItem <- 1

    press (ResultsWindow.list window) Key.F3

    Assert.Empty seen

[<Fact>]
let ``F3 on the header of a group views nothing`` () =
    let seen = ResizeArray<FindHit>()
    use window = viewing seen
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    (ResultsWindow.list window).SelectedItem <- 0

    press (ResultsWindow.list window) Key.F3

    Assert.Empty seen

[<Fact>]
let ``Enter on a hit answers it and closes the window`` () =
    let closed = ref 0
    use window = showing ignore (fun _ -> closed.Value <- closed.Value + 1)
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    (ResultsWindow.list window).SelectedItem <- 1

    press (ResultsWindow.list window) Key.Enter

    Assert.Equal(Some(AtHit(hitIn "start" "a.txt")), (ResultsWindow.answer window).Landing)
    Assert.Equal(1, closed.Value)

[<Fact>]
let ``Enter on the header of a group answers the directory it names`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start/docs" "a.txt" ])
    (ResultsWindow.list window).SelectedItem <- 0

    press (ResultsWindow.list window) Key.Enter

    Assert.Equal(Some(At(Into(OnDisk(fp (at "start/docs"))))), (ResultsWindow.answer window).Landing)

[<Fact>]
let ``the panel key answers with the whole list and closes the window`` () =
    let closed = ref 0
    use window = showing ignore (fun _ -> closed.Value <- closed.Value + 1)
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    (ResultsWindow.list window).SelectedItem <- 1

    press (ResultsWindow.list window) Key.F2

    Assert.Equal(Some InPanel, (ResultsWindow.answer window).Landing)
    Assert.Equal(1, closed.Value)

[<Fact>]
let ``the panel key names itself in the bar`` () = Assert.Equal("Panel", FindKeys.bar[1])

[<Fact>]
let ``a window left by Esc answers no landing at all`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])
    ResultsWindow.finish window (Finished(counts 1))

    press (ResultsWindow.list window) Key.Esc

    Assert.Equal(None, (ResultsWindow.answer window).Landing)

[<Fact>]
let ``the answer carries what was found and the row it was left on`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ])
    (ResultsWindow.list window).SelectedItem <- 2

    let answer = ResultsWindow.answer window

    Assert.Equal<FindHit list>([ hitIn "start" "a.txt"; hitIn "start" "b.txt" ], answer.Results.Hits)
    Assert.Equal(2, answer.Results.Cursor)

let private lastResults hits cursor =
    { Query = query
      Ground = ground
      Hits = hits
      Outcome = Finished(counts (List.length hits))
      Cursor = cursor }

[<Fact>]
let ``a reopened window shows what was found before`` () =
    use window =
        reopening (lastResults [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ] 0) 0

    let shown = lines window

    Assert.Equal(at "start", shown |> List.item 0)
    Assert.StartsWith("  a.txt", shown |> List.item 1)
    Assert.StartsWith("  b.txt", shown |> List.item 2)

[<Fact>]
let ``a reopened window names the query and where it ran`` () =
    use window = reopening (lastResults [ hitIn "start" "a.txt" ] 0) 0

    let start = at "start"

    Assert.Equal($"Last results: *.txt in %s{start} — 1 file, 0 directories", summary window)

[<Fact>]
let ``a reopened window counts the hits that are gone`` () =
    use window = reopening (lastResults [ hitIn "start" "a.txt" ] 0) 2

    let start = at "start"

    Assert.Equal($"Last results: *.txt in %s{start} — 1 file, 0 directories, 2 gone", summary window)

[<Fact>]
let ``a reopened window comes back on the row the cursor left`` () =
    use window =
        reopening (lastResults [ hitIn "start" "a.txt"; hitIn "start" "b.txt" ] 2) 0

    Assert.Equal(Nullable 2, (ResultsWindow.list window).SelectedItem)

[<Fact>]
let ``a cursor past what is left comes back on the nearest row still there`` () =
    use window = reopening (lastResults [ hitIn "start" "a.txt" ] 7) 3

    Assert.Equal(Nullable 1, (ResultsWindow.list window).SelectedItem)

[<Fact>]
let ``a reopened window answers the walk it was told about rather than a new one`` () =
    use window = reopening (lastResults [ hitIn "start" "a.txt" ] 1) 0

    let answer = ResultsWindow.answer window

    Assert.Equal(query, answer.Results.Query)
    Assert.Equal(Finished(counts 1), answer.Results.Outcome)
    Assert.Equal(1, answer.Results.Cursor)

[<Fact>]
let ``a window closed while the walk still ran answers a stopped walk`` () =
    use window = laidOut ()
    ResultsWindow.add window (arriving [ hitIn "start" "a.txt" ])

    let answer = ResultsWindow.answer window

    Assert.Equal(Stopped(counts 1), answer.Results.Outcome)
