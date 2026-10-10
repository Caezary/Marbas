module Marbas.Tests.Adapters.CommandProcessTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private running run elevation : CommandRun =
    { Label = Expect.some (MenuLabel.tryCreate "Show")
      Command =
        { Program = { Text = "show"; Quoting = Bare }
          Arguments =
            [ { Text = "-x"; Quoting = Bare }
              { Text = "it's mine.txt"
                Quoting = Quoted } ]
          ListFile = None }
      Run = run
      Elevation = elevation
      WorkingDirectory = fp (at "work") }

let private refusedAt written =
    match written with
    | Error(LaunchRefused(target, _)) -> Some target
    | _ -> None

[<Fact>]
let ``a direct run on Windows goes through the shell verb with each word an argument of its own`` () =
    let described =
        CommandProcess.describe Windows WindowPrograms.none (running Directly AsUser)

    let start = Expect.ok described
    Assert.Equal("show", start.FileName)
    Assert.Equal<string list>([ "-x"; "it's mine.txt" ], List.ofSeq start.ArgumentList)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a run on Windows starts in the directory of the panel it came from`` () =
    let described =
        CommandProcess.describe Windows WindowPrograms.none (running Directly AsUser)

    Assert.Equal(at "work", (Expect.ok described).WorkingDirectory)

[<Fact>]
let ``a run as administrator on Windows asks for the rights the way Alt+Shift+R does`` () =
    let described =
        CommandProcess.describe Windows WindowPrograms.none (running InShellClosing AsAdministrator)

    Assert.Equal(LaunchProcess.ElevatedVerb, (Expect.ok described).Verb)

[<Fact>]
let ``a run as the user on Windows asks for no rights`` () =
    let described =
        CommandProcess.describe Windows WindowPrograms.none (running Directly AsUser)

    Assert.Equal("", (Expect.ok described).Verb)

[<Fact>]
let ``a shell run on Windows that closes its window is cmd with c, every put-in path quoted`` () =
    let described =
        CommandProcess.describe Windows WindowPrograms.none (running InShellClosing AsUser)

    let start = Expect.ok described
    Assert.Equal(CommandProcess.WindowsShell, start.FileName)
    Assert.Equal("/s /c \"show -x \"it's mine.txt\"\"", start.Arguments)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a shell run on Windows that keeps its window is cmd with k`` () =
    let described =
        CommandProcess.describe Windows WindowPrograms.none (running InShellKeptOpen AsUser)

    Assert.Equal("/s /k \"show -x \"it's mine.txt\"\"", (Expect.ok described).Arguments)

[<Fact>]
let ``a shell run on Windows as administrator changes to its directory first, not trusting the elevation to keep it``
    ()
    =
    let directory = at "work"

    let described =
        CommandProcess.describe Windows WindowPrograms.none (running InShellKeptOpen AsAdministrator)

    Assert.Equal($"/s /k \"cd /d \"{directory}\" && show -x \"it's mine.txt\"\"", (Expect.ok described).Arguments)

[<Fact>]
let ``a direct run of a window program on Windows is started from its file with its streams drained`` () =
    let windowPrograms = WindowPrograms.knowing "show" (at "apps/show.exe")

    let described =
        CommandProcess.describe Windows windowPrograms (running Directly AsUser)

    let start = Expect.ok described
    Assert.Equal(at "apps/show.exe", start.FileName)
    Assert.Equal<string list>([ "-x"; "it's mine.txt" ], List.ofSeq start.ArgumentList)
    Assert.False start.UseShellExecute
    Assert.True start.RedirectStandardOutput
    Assert.Equal(at "work", start.WorkingDirectory)

[<Fact>]
let ``a direct run of a window program as administrator on Windows keeps the shell verb, the only way to ask`` () =
    let windowPrograms = WindowPrograms.knowing "show" (at "apps/show.exe")

    let described =
        CommandProcess.describe Windows windowPrograms (running Directly AsAdministrator)

    let start = Expect.ok described
    Assert.True start.UseShellExecute
    Assert.Equal(LaunchProcess.ElevatedVerb, start.Verb)

[<Fact>]
let ``a shell run on Windows stays a shell run when its first word is a window program`` () =
    let windowPrograms = WindowPrograms.knowing "show" (at "apps/show.exe")

    let described =
        CommandProcess.describe Windows windowPrograms (running InShellClosing AsUser)

    let start = Expect.ok described
    Assert.Equal(CommandProcess.WindowsShell, start.FileName)
    Assert.True start.UseShellExecute

[<Fact>]
let ``a direct run on Unix is started with its streams drained, each word an argument of its own`` () =
    let described =
        CommandProcess.describe Unix WindowPrograms.none (running Directly AsUser)

    let start = Expect.ok described
    Assert.Equal("show", start.FileName)
    Assert.Equal<string list>([ "-x"; "it's mine.txt" ], List.ofSeq start.ArgumentList)
    Assert.False start.UseShellExecute
    Assert.True start.RedirectStandardOutput

[<Theory>]
[<InlineData(0)>]
[<InlineData(1)>]
let ``a shell run on Unix is sh with c, every put-in path quoted for it`` (way: int) =
    let run = List.item way [ InShellClosing; InShellKeptOpen ]

    let described =
        CommandProcess.describe Unix WindowPrograms.none (running run AsUser)

    let start = Expect.ok described
    Assert.Equal(CommandProcess.UnixShell, start.FileName)
    Assert.Equal<string list>([ "-c"; "show -x 'it'\\''s mine.txt'" ], List.ofSeq start.ArgumentList)

[<Fact>]
let ``a run as administrator on Unix is refused by platform`` () =
    let described =
        CommandProcess.describe Unix WindowPrograms.none (running Directly AsAdministrator)

    Assert.Equal(Error(LaunchRefused(fp "show", LaunchProcess.ElevationUnavailable)), described)

[<Fact>]
let ``the list file holds the marked entries one per line, its directory made first`` () =
    let fs = MockFileSystem()

    let listFile =
        { FilePath = fp (at "temp/Marbas/session/marked-1.txt")
          Listed = [ fp (at "work/a.txt"); fp (at "work/b c.txt") ] }

    let written = CommandProcess.writeList fs listFile

    Assert.Equal(Ok(), written)

    Assert.Equal<string list>(
        [ at "work/a.txt"; at "work/b c.txt" ],
        List.ofArray (fs.File.ReadAllLines(at "temp/Marbas/session/marked-1.txt"))
    )

[<Fact>]
let ``a list file that cannot be written refuses the run, naming it`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")

    let listFile =
        { FilePath = fp (at "blocked/marked-1.txt")
          Listed = [ fp (at "work/a.txt") ] }

    let written = CommandProcess.writeList fs listFile

    Assert.Equal(Some(fp (at "blocked/marked-1.txt")), refusedAt written)
