module Marbas.Tests.State.RefreshTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.State.AppState
open Marbas.Tests

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddDirectory @"C:\start\docs"
    fs.AddFile(@"C:\start\readme.txt", MockFileData "hi")
    fs

let private loaded side path (fs: IFileSystem) state =
    loadPanel fs side path state |> Expect.ok

let private names entries =
    entries |> List.map (fun (e: Entry) -> EntryName.value e.Name)

[<Fact>]
let ``refresh picks up entries created after the panel was loaded`` () =
    let fs = mockFs ()
    let state = create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs
    fs.AddFile(@"C:\start\added.txt", MockFileData "new")

    let updated = refresh fs state |> Expect.ok

    Assert.Contains("added.txt", names updated.Left.Entries)

[<Fact>]
let ``refresh keeps the cursor on the same entry when its index shifts`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 2

    let cursorName = state.Left.Entries[state.Left.Cursor].Name
    fs.AddDirectory @"C:\start\aaa"

    let updated = refresh fs state |> Expect.ok

    Assert.Equal<string list>([ ".."; "aaa"; "docs"; "readme.txt" ], names updated.Left.Entries)
    Assert.Equal(cursorName, updated.Left.Entries[updated.Left.Cursor].Name)
    Assert.Equal(3, updated.Left.Cursor)

[<Fact>]
let ``refresh falls back to the first entry when the cursor entry is gone`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start") |> loaded Left (fp @"C:\start") fs |> setCursor Left 2

    fs.File.Delete @"C:\start\readme.txt"

    let updated = refresh fs state |> Expect.ok

    Assert.Equal(0, updated.Left.Cursor)

[<Fact>]
let ``refresh only reloads the active panel`` () =
    let fs = mockFs ()

    let state =
        create (fp @"C:\start")
        |> loaded Left (fp @"C:\start") fs
        |> loaded Right (fp @"C:\start") fs
        |> activate Right

    fs.AddFile(@"C:\start\added.txt", MockFileData "new")

    let updated = refresh fs state |> Expect.ok

    Assert.Contains("added.txt", names updated.Right.Entries)
    Assert.DoesNotContain("added.txt", names updated.Left.Entries)

[<Fact>]
let ``refresh leaves the cursor at the top of a panel with nothing in it`` () =
    let fs = MockFileSystem()
    fs.AddDirectory @"D:\"
    let state = create (fp @"D:\") |> loaded Left (fp @"D:\") fs

    let updated = refresh fs state |> Expect.ok

    Assert.Empty updated.Left.Entries
    Assert.Equal(0, updated.Left.Cursor)

[<Fact>]
let ``refreshing a directory that vanished reports an error`` () =
    let fs = mockFs ()
    let state = create (fp @"C:\start\docs") |> loaded Left (fp @"C:\start\docs") fs
    fs.Directory.Delete @"C:\start\docs"

    let result = refresh fs state

    Assert.Equal(Error(DirectoryNotFound(fp @"C:\start\docs")), result)
