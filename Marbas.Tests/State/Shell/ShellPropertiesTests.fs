module Marbas.Tests.State.Shell.ShellPropertiesTests

open System
open Xunit
open Marbas.FileSystem
open Marbas.State
open Marbas.State.Update
open Marbas.Tests
open Marbas.Tests.State.Shell.ShellFixture

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``changed properties are dispatched back`` () =
    let sent, dispatch = capture ()

    let shown =
        { Path = fp (at "start/docs")
          Flags = Set.empty
          Created = DateTimeOffset(DateTime(2024, 1, 1))
          Modified = DateTimeOffset(DateTime(2024, 1, 1))
          Accessed = DateTimeOffset(DateTime(2024, 1, 1)) }

    let changed = EntryProperties.withFlag HiddenFlag shown

    let deps =
        { noDeps () with
            Prompts.EditProperties = fun _ -> Some changed }

    Shell.interpret deps dispatch (ShowProperties shown)

    Assert.Equal<Message list>([ PropertiesEntered changed ], List.ofSeq sent)

[<Fact>]
let ``a dismissed properties dialog dispatches nothing`` () =
    let sent, dispatch = capture ()

    let shown =
        { Path = fp (at "start/docs")
          Flags = Set.empty
          Created = DateTimeOffset(DateTime(2024, 1, 1))
          Modified = DateTimeOffset(DateTime(2024, 1, 1))
          Accessed = DateTimeOffset(DateTime(2024, 1, 1)) }

    Shell.interpret (noDeps ()) dispatch (ShowProperties shown)

    Assert.Empty sent
