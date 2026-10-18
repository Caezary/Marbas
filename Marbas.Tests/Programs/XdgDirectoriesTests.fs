module Marbas.Tests.Programs.XdgDirectoriesTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Programs

let private resolve dataHome dataDirectories configHome configDirectories =
    XdgDirectories.resolve (MockFileSystem()) "/home/user" dataHome dataDirectories configHome configDirectories

[<Fact>]
let ``the variables that are set are the directories, home first`` () =
    let directories =
        resolve (Some "/home/user/data") (Some "/opt/share:/usr/share") (Some "/home/user/conf") (Some "/etc/xdg")

    Assert.Equal<string list>([ "/home/user/data"; "/opt/share"; "/usr/share" ], directories.Data)
    Assert.Equal<string list>([ "/home/user/conf"; "/etc/xdg" ], directories.Config)

[<Fact>]
let ``a variable that is not set falls back to what the spec says`` () =
    let directories = resolve None None None None

    Assert.Equal<string list>([ "/usr/local/share"; "/usr/share" ], List.tail directories.Data)
    Assert.Equal<string list>([ "/etc/xdg" ], List.tail directories.Config)

[<Fact>]
let ``an empty entry in the list is not a directory`` () =
    let directories =
        resolve (Some "/home/user/data") (Some "/opt/share::/usr/share:") (Some "/home/user/conf") (Some "/etc/xdg")

    Assert.Equal<string list>([ "/home/user/data"; "/opt/share"; "/usr/share" ], directories.Data)

[<Fact>]
let ``a blank variable falls back as one that is not set`` () =
    let directories = resolve (Some " ") (Some " ") (Some " ") (Some " ")

    Assert.Equal<string list>([ "/usr/local/share"; "/usr/share" ], List.tail directories.Data)
    Assert.Equal<string list>([ "/etc/xdg" ], List.tail directories.Config)
