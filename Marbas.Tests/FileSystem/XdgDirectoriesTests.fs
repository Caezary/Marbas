module Marbas.Tests.FileSystem.XdgDirectoriesTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem

let private resolve dataHome dataDirectories configHome configDirectories =
    XdgDirectories.resolve (MockFileSystem()) "/home/user" dataHome dataDirectories configHome configDirectories

[<Fact>]
let ``the variables that are set are the directories, home first`` () =
    let directories = resolve "/home/user/data" "/opt/share:/usr/share" "/home/user/conf" "/etc/xdg"

    Assert.Equal<string list>([ "/home/user/data"; "/opt/share"; "/usr/share" ], directories.Data)
    Assert.Equal<string list>([ "/home/user/conf"; "/etc/xdg" ], directories.Config)

[<Fact>]
let ``a variable that is not set falls back to what the spec says`` () =
    let directories = resolve "" "" null null

    Assert.Equal<string list>([ "/usr/local/share"; "/usr/share" ], List.tail directories.Data)
    Assert.Equal<string list>([ "/etc/xdg" ], List.tail directories.Config)

[<Fact>]
let ``an empty entry in the list is not a directory`` () =
    let directories = resolve "/home/user/data" "/opt/share::/usr/share:" "/home/user/conf" "/etc/xdg"

    Assert.Equal<string list>([ "/home/user/data"; "/opt/share"; "/usr/share" ], directories.Data)
