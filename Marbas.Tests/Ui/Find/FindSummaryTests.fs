module Marbas.Tests.Ui.Find.FindSummaryTests

open Xunit
open Marbas.FileSystem.FileSearch
open Marbas.Ui.Find

let private counts files directories skipped =
    { Files = files
      Directories = directories
      Skipped = skipped }

[<Fact>]
let ``a running walk says where it is and what it has found`` () =
    let line = FindSummary.describe (FindSummary.Walking(@"C:\work\src", counts 12 3 0))

    Assert.Equal(@"Searching: C:\work\src — 12 files, 3 directories", line)

[<Fact>]
let ``a walk that has found nothing yet says so`` () =
    let line = FindSummary.describe (FindSummary.Walking(@"C:\work", counts 0 0 0))

    Assert.Equal(@"Searching: C:\work — nothing found", line)

[<Fact>]
let ``a finished walk says so and counts what it passed over`` () =
    let line = FindSummary.describe (FindSummary.Done(Finished(counts 12 3 2)))

    Assert.Equal("Finished — 12 files, 3 directories, 2 skipped", line)

[<Fact>]
let ``a stopped walk says so and keeps its counts`` () =
    let line = FindSummary.describe (FindSummary.Done(Stopped(counts 4 0 0)))

    Assert.Equal("Stopped — 4 files, 0 directories", line)

[<Fact>]
let ``a finished walk that found nothing says so in words`` () =
    let line = FindSummary.describe (FindSummary.Done(Finished(counts 0 0 0)))

    Assert.Equal("Finished — nothing found", line)

[<Fact>]
let ``what was skipped is counted even where nothing was found`` () =
    let line = FindSummary.describe (FindSummary.Done(Finished(counts 0 0 3)))

    Assert.Equal("Finished — nothing found, 3 skipped", line)

[<Fact>]
let ``one of a thing is spelled without its plural`` () =
    let line = FindSummary.describe (FindSummary.Done(Finished(counts 1 1 1)))

    Assert.Equal("Finished — 1 file, 1 directory, 1 skipped", line)
