module Marbas.Tests.State.Shell.ShellFixture

open System
open System.IO.Abstractions.TestingHelpers
open Marbas.FileSystem
open Marbas.Profile
open Marbas.FileSystem.Operations
open Marbas.Domain
open Marbas.State.Update
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let noDeps () =
    Fakes.dependencies (MockFileSystem()) (at "start")

let withFs fs = Fakes.dependencies fs (at "start")

let capture () =
    let sent = ResizeArray<Message>()
    sent, fun (msg: Message) -> sent.Add msg

let stopped =
    { Report = ignore
      IsCancelled = fun () -> true }

let typed raw =
    EnteredText.tryCreate raw |> Expect.some

let rememberedIn (fs: MockFileSystem) kind =
    History.load fs (fp (at "history.conf"))
    |> Expect.ok
    |> History.inputs kind
    |> List.map (_.Item >> EnteredText.value)

let typedEarlier (fs: MockFileSystem) kind texts =
    texts
    |> List.iter (fun text ->
        History.change fs (fp (at "history.conf")) DateTimeOffset.UnixEpoch (Remember(Typed(kind, typed text)))
        |> ignore)
