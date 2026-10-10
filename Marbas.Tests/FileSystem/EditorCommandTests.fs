module Marbas.Tests.FileSystem.EditorCommandTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem

[<Fact>]
let ``the configured editor is taken as it stands`` () =
    let command = EditorCommand.choose (Some "code --wait") Windows

    Assert.Equal("code --wait", command)

[<Fact>]
let ``a configured editor is trimmed`` () =
    let command = EditorCommand.choose (Some "  nano  ") Unix

    Assert.Equal("nano", command)

[<Fact>]
let ``windows falls back to notepad`` () =
    let command = EditorCommand.choose None Windows

    Assert.Equal(EditorCommand.WindowsDefault, command)

[<Fact>]
let ``unix falls back to vi`` () =
    let command = EditorCommand.choose None Unix

    Assert.Equal(EditorCommand.UnixDefault, command)

[<Fact>]
let ``a blank setting counts as none`` () =
    let command = EditorCommand.choose (Some "   ") Windows

    Assert.Equal(EditorCommand.WindowsDefault, command)
