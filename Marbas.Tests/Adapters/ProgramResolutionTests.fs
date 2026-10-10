module Marbas.Tests.Adapters.ProgramResolutionTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Adapters
open Marbas.Adapters.ProgramResolution
open Marbas.Tests
open Marbas.Tests.ProgramImageBytes

let private at = Root.at

let private fp = FileSystemPath.create

let private work = fp (at "work")

let private searching (registered: (string * string) list) =
    { Directories = [ fp (at "one"); fp (at "two") ]
      Extensions = [ ".com"; ".exe"; ".bat"; ".cmd" ]
      AppPath = fun key -> registered |> Map.ofList |> Map.tryFind key }

let private nothingRegistered = searching []

let private holding (paths: string list) =
    let fs = MockFileSystem()
    fs.AddDirectory(at "work")
    paths |> List.iter (fun path -> fs.AddFile(at path, MockFileData windowImage))
    fs

[<Fact>]
let ``a program named by its full path is found where it is written`` () =
    let fs = holding [ "tools/viewer.exe" ]

    let found = resolve fs nothingRegistered work (at "tools/viewer.exe")

    Assert.Equal(Some(fp (at "tools/viewer.exe")), found)

[<Fact>]
let ``a program named relative to the panel is found under the panel's directory`` () =
    let fs = holding [ "work/bin/viewer.exe" ]

    let found = resolve fs nothingRegistered work (Root.relative "bin/viewer.exe")

    Assert.Equal(Some(fp (at "work/bin/viewer.exe")), found)

[<Fact>]
let ``a bare name in the panel's directory comes before the search path`` () =
    let fs = holding [ "work/viewer.exe"; "one/viewer.exe" ]

    let found = resolve fs nothingRegistered work "viewer.exe"

    Assert.Equal(Some(fp (at "work/viewer.exe")), found)

[<Fact>]
let ``a bare name is found in the first directory of the search path that holds it`` () =
    let fs = holding [ "one/viewer.exe"; "two/viewer.exe" ]

    let found = resolve fs nothingRegistered work "viewer.exe"

    Assert.Equal(Some(fp (at "one/viewer.exe")), found)

[<Fact>]
let ``a bare name without an extension takes the extensions in their order and never stands alone`` () =
    let fs = holding [ "one/code"; "one/code.cmd"; "two/code.exe" ]

    let found = resolve fs nothingRegistered work "code"

    Assert.Equal(Some(fp (at "one/code.cmd")), found)

[<Fact>]
let ``a name with an extension is not given another`` () =
    let fs = holding [ "one/viewer.exe.exe" ]

    let found = resolve fs nothingRegistered work "viewer.exe"

    Assert.Equal(None, found)

[<Fact>]
let ``a name found nowhere on the search path is looked up among the App Paths`` () =
    let fs = holding [ "apps/Code.exe" ]

    let found =
        resolve fs (searching [ "code.exe", at "apps/Code.exe" ]) work "code.exe"

    Assert.Equal(Some(fp (at "apps/Code.exe")), found)

[<Fact>]
let ``a name without an extension is looked up among the App Paths as an exe`` () =
    let fs = holding [ "apps/Code.exe" ]

    let found = resolve fs (searching [ "code.exe", at "apps/Code.exe" ]) work "code"

    Assert.Equal(Some(fp (at "apps/Code.exe")), found)

[<Fact>]
let ``an App Paths entry written in quotes names the file inside them`` () =
    let fs = holding [ "apps/Code.exe" ]

    let found =
        resolve fs (searching [ "code.exe", "\"" + at "apps/Code.exe" + "\"" ]) work "code.exe"

    Assert.Equal(Some(fp (at "apps/Code.exe")), found)

[<Fact>]
let ``an App Paths entry naming a file that is not there resolves nothing`` () =
    let fs = holding []

    let found =
        resolve fs (searching [ "code.exe", at "apps/Code.exe" ]) work "code.exe"

    Assert.Equal(None, found)

[<Fact>]
let ``a name found nowhere resolves nothing`` () =
    let fs = holding []

    let found = resolve fs nothingRegistered work "viewer.exe"

    Assert.Equal(None, found)

[<Fact>]
let ``a resolved window program is started as one`` () =
    let fs = holding [ "one/viewer.exe" ]

    let found = windowProgram fs nothingRegistered work "viewer.exe"

    Assert.Equal(Some(fp (at "one/viewer.exe")), found)

[<Fact>]
let ``a resolved console program is not taken for a window program`` () =
    let fs = holding []
    fs.AddFile(at "one/tool.exe", MockFileData consoleImage)

    let found = windowProgram fs nothingRegistered work "tool.exe"

    Assert.Equal(None, found)

[<Fact>]
let ``a script is not taken for a window program`` () =
    let fs = holding []
    fs.AddFile(at "one/code.cmd", MockFileData "@echo off")

    let found = windowProgram fs nothingRegistered work "code"

    Assert.Equal(None, found)
