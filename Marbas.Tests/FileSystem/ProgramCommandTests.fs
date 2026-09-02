module Marbas.Tests.FileSystem.ProgramCommandTests

open Xunit
open Marbas.FileSystem

let private programOf command =
    ProgramCommand.programOf command |> Option.map ProgramPath.value

[<Fact>]
let ``a quoted program is what stands between the quotes`` () =
    let program =
        programOf "\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\" -osint -url \"%1\""

    Assert.Equal(Some "C:\\Program Files\\Mozilla Firefox\\firefox.exe", program)

[<Fact>]
let ``an unquoted program without spaces is the first word`` () =
    let program = programOf "%SystemRoot%\\system32\\NOTEPAD.EXE %1"

    Assert.Equal(Some "%SystemRoot%\\system32\\NOTEPAD.EXE", program)

[<Fact>]
let ``an unquoted program runs to the word that ends in a program extension`` () =
    let program = programOf "C:\\Program Files\\App\\app.exe \"%1\""

    Assert.Equal(Some "C:\\Program Files\\App\\app.exe", program)

[<Fact>]
let ``a command naming no program extension keeps its first word`` () =
    let program = programOf "firefox %u"

    Assert.Equal(Some "firefox", program)

[<Fact>]
let ``a path with no arguments at all is the whole of it`` () =
    let program = programOf "/usr/bin/gedit"

    Assert.Equal(Some "/usr/bin/gedit", program)

[<Fact>]
let ``a quote that is never closed still names what follows it`` () =
    let program = programOf "\"C:\\tools\\app.exe"

    Assert.Equal(Some "C:\\tools\\app.exe", program)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``a command that names nothing is no program`` command =
    Assert.Equal(None, programOf command)
