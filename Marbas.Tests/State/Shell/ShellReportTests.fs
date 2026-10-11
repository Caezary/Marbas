module Marbas.Tests.State.Shell.ShellReportTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Messages
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture
open Marbas.Tests.State.Shell.ShellArchiveFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``a listing error is reported under the listing title`` () =
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (Report(Problem.Listing(DirectoryListing.DirectoryNotFound(fp (at "gone")))))

    Assert.Equal(ListingErrorMessage.Title, fst reported)
    Assert.Contains(at "gone", snd reported)

[<Fact>]
let ``archives that cannot be read are reported in one message under the archive title`` () =
    let reported = ResizeArray<string * string>()

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported.Add(report.Title, report.Message) }

    let other = fp (at "start/other.zip")

    Shell.interpret
        deps
        ignore
        (Report(Problem.UnverifiedHits [ ArchiveDamaged(dataZip, "bad header"); ArchiveDamaged(other, "bad header") ]))

    let title, message = Assert.Single reported
    Assert.Equal(ArchiveErrorMessage.Title, title)
    Assert.Contains(at "start/data.zip", message)
    Assert.Contains(at "start/other.zip", message)

[<Fact>]
let ``an operation error is reported under the operation title`` () =
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    Shell.interpret deps ignore (Report(Problem.Operation(TargetExists(fp (at "start/docs")))))

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("docs", snd reported)

[<Fact>]
let ``item failures are reported under the operation title`` () =
    let mutable reported = ("", "")

    let deps =
        { noDeps () with
            Prompts.ReportError = fun report -> reported <- (report.Title, report.Message) }

    let failed =
        [ { Source = ItemOnDisk(fp (at "start/gone.txt"))
            Outcome = Error(SourceNotFound(fp (at "start/gone.txt"))) } ]

    Shell.interpret deps ignore (Report(Problem.ItemFailures failed))

    Assert.Equal(OperationErrorMessage.Title, fst reported)
    Assert.Contains("gone.txt", snd reported)
