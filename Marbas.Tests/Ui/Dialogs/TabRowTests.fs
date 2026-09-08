module Marbas.Tests.Ui.Dialogs.TabRowTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Collections
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.Ui.Dialogs
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory(at "start/docs")
    fs.AddDirectory(at "backup")
    fs :> IFileSystem

let private tabOn (fs: IFileSystem) left right =
    create (fp (at "start"))
    |> loadPanel fs Left (fp left)
    |> Expect.ok
    |> loadPanel fs Right (fp right)
    |> Expect.ok
    |> _.Tabs
    |> Zipper.current

[<Fact>]
let ``a row carries its number and where both panels stand`` () =
    let fs = mockFs ()
    let tab = tabOn fs (at "start/docs") (at "backup")

    let row = TabRow.describe fs 2 tab

    Assert.StartsWith("2.", row)
    Assert.Contains(at "start/docs", row)
    Assert.Contains(at "backup", row)

[<Fact>]
let ``the left path is padded so the right ones line up`` () =
    let fs = mockFs ()
    let short = TabRow.describe fs 1 (tabOn fs (at "start") (at "backup"))
    let longer = TabRow.describe fs 1 (tabOn fs (at "start/docs") (at "backup"))

    Assert.Equal(short.IndexOf(at "backup"), longer.IndexOf(at "backup"))

[<Fact>]
let ``a panel inside an archive says so in its row`` () =
    let fs = mockFs ()

    let inside =
        { tabOn fs (at "start") (at "backup") with
            Left =
                { (tabOn fs (at "start") (at "backup")).Left with
                    Path = InArchive(fp (at "start/backup.zip"), ArchivePath.root) } }

    let row = TabRow.describe fs 1 inside

    Assert.Contains(at "start/backup.zip", row)
