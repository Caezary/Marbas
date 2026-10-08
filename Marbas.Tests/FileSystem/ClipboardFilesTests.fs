module Marbas.Tests.FileSystem.ClipboardFilesTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``no files are nothing to put on the clipboard`` () =
    let files = ClipboardFiles.tryCreate ForCopying []

    Assert.Equal(None, files)

[<Fact>]
let ``the files are kept in their order`` () =
    let files =
        ClipboardFiles.tryCreate ForCopying [ fp (at "start/b.txt"); fp (at "start/a.txt") ]
        |> Expect.some

    let paths = ClipboardFiles.paths files

    Assert.Equal<FileSystemPath list>([ fp (at "start/b.txt"); fp (at "start/a.txt") ], paths)

[<Fact>]
let ``files put there to be copied say so`` () =
    let files =
        ClipboardFiles.tryCreate ForCopying [ fp (at "start/notes.txt") ] |> Expect.some

    let intent = ClipboardFiles.intent files

    Assert.Equal(ForCopying, intent)

[<Fact>]
let ``files put there to be moved say so`` () =
    let files =
        ClipboardFiles.tryCreate ForMoving [ fp (at "start/notes.txt") ] |> Expect.some

    let intent = ClipboardFiles.intent files

    Assert.Equal(ForMoving, intent)
