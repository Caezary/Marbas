module Marbas.Tests.Integration.Disk.LinkTargetCopyTests

open System
open System.IO
open System.IO.Abstractions
open System.Reflection
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.State.AppState
open Marbas.State.Update
open Marbas.Adapters
open Marbas.Tests

let private disk = FileSystem() :> IFileSystem

let private withData () =
    let scratch = new Scratch()
    scratch.Directory "data/work"
    scratch.File "data/work/report.txt" "report"
    scratch

let private standingIn (scratch: Scratch) directory name =
    let state =
        create (scratch.Path directory)
        |> loadPanel disk Left (scratch.Path directory)
        |> Expect.ok

    let index =
        state.Left.Entries
        |> Array.findIndex (fun entry -> EntryName.value entry.Name = name)

    setCursor Left index state

let private copying lines =
    [ PutOnClipboard(ClipboardText.tryCreate lines |> Expect.some) ]

let private invoke (target: obj) (flags: BindingFlags) (name: string) (arguments: obj array) =
    target.GetType().InvokeMember(name, flags, null, target, arguments)
    |> Option.ofObj

let private shortcutBySystem (location: string) (target: string) =
    let shell =
        Type.GetTypeFromProgID "WScript.Shell"
        |> Expect.notNull
        |> Activator.CreateInstance
        |> Expect.notNull

    let shortcut =
        invoke shell BindingFlags.InvokeMethod "CreateShortcut" [| location |]
        |> Expect.some

    invoke shortcut BindingFlags.SetProperty "TargetPath" [| target |] |> ignore
    invoke shortcut BindingFlags.InvokeMethod "Save" [||] |> ignore

[<WindowsFact("A junction is a Windows concept")>]
let ``a junction copies the directory it leads into`` () =
    use scratch = withData ()
    let make = Links.create (NativeLinks.forPlatform Windows) Windows disk

    make
        { Kind = Junction
          Target = scratch.Path "data/work"
          Location = scratch.Path "data/twin" }
    |> Expect.ok

    let result = update disk LinkTargetsCopyRequested (standingIn scratch "data" "twin")

    Assert.Equal<Effect list>(copying [ scratch.At "data/work" ], result.Effects)

[<SymbolicLinkFact>]
let ``a link below a linked directory is resolved by itself alone`` () =
    use scratch = withData ()

    Directory.CreateSymbolicLink(scratch.At "data/place", scratch.At "data/work")
    |> ignore

    File.CreateSymbolicLink(scratch.At "data/work/twin.txt", "report.txt") |> ignore

    let result =
        update disk LinkTargetsCopyRequested (standingIn scratch "data/place" "twin.txt")

    Assert.Equal<Effect list>(copying [ scratch.At "data/place/report.txt" ], result.Effects)

[<WindowsFact("A junction is a Windows concept")>]
let ``a relative shortcut below a junction is resolved from where it is listed`` () =
    use scratch = withData ()
    let make = Links.create (NativeLinks.forPlatform Windows) Windows disk
    File.WriteAllBytes(scratch.At "data/work/report.lnk", ShortcutBytes.toRelativePath "report.txt")

    make
        { Kind = Junction
          Target = scratch.Path "data/work"
          Location = scratch.Path "data/place" }
    |> Expect.ok

    let result =
        update disk LinkTargetsCopyRequested (standingIn scratch "data/place" "report.lnk")

    Assert.Equal<Effect list>(copying [ scratch.At "data/place/report.txt" ], result.Effects)

[<WindowsFact("A shortcut written by the Windows shell needs Windows")>]
let ``a shortcut written by the Windows shell copies the file it names`` () =
    use scratch = withData ()
    shortcutBySystem (scratch.At "data/report.lnk") (scratch.At "data/work/report.txt")

    let result =
        update disk LinkTargetsCopyRequested (standingIn scratch "data" "report.lnk")

    Assert.Equal<Effect list>(copying [ scratch.At "data/work/report.txt" ], result.Effects)
