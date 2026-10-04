module Marbas.Tests.FileSystem.PastedPathTests

open System
open System.IO
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private relative = Root.relative

let private fp = FileSystemPath.create

let private variables =
    Map.ofList [ "HOME", at "home/user"; "USERPROFILE", at "home/user"; "DATA", at "data" ]

let private lookup name = Map.tryFind name variables

let private read text =
    PastedPath.read (MockFileSystem()) lookup (fp (at "start")) text

[<Fact>]
let ``a full path is read as it stands`` () =
    let path = read (at "work/notes.txt")

    Assert.Equal(Some(fp (at "work/notes.txt")), path)

[<Theory>]
[<InlineData("  ", "  ")>]
[<InlineData("", "\r\n")>]
[<InlineData("\t", " \n")>]
let ``spaces and a line break around the path are stripped`` (before: string, after: string) =
    let path = read (before + at "work/notes.txt" + after)

    Assert.Equal(Some(fp (at "work/notes.txt")), path)

[<Theory>]
[<InlineData("\"", "\"")>]
[<InlineData("'", "'")>]
[<InlineData(" \"", "\" ")>]
let ``quotes around the path are stripped`` (before: string, after: string) =
    let path = read (before + at "work/my notes.txt" + after)

    Assert.Equal(Some(fp (at "work/my notes.txt")), path)

[<Fact>]
let ``a file URI is read as the path it names`` () =
    let uri = Uri(at "work/my notes.txt").AbsoluteUri

    let path = read uri

    Assert.Equal(Some(fp (at "work/my notes.txt")), path)

[<Fact>]
let ``a relative path is taken from the directory given`` () =
    let path = read "docs"

    Assert.Equal(Some(fp (at "start/docs")), path)

[<Fact>]
let ``a relative path may step up out of the directory given`` () =
    let path = read (relative "../work/notes.txt")

    Assert.Equal(Some(fp (at "work/notes.txt")), path)

[<Fact>]
let ``a separator after a directory is dropped`` () =
    let path = read (at "work" + string Path.DirectorySeparatorChar)

    Assert.Equal(Some(fp (at "work")), path)

[<Fact>]
let ``a root keeps its separator`` () =
    let path = read (at "")

    Assert.Equal(Some(fp (at "")), path)

[<Fact>]
let ``a tilde alone is the home directory`` () =
    let path = read "~"

    Assert.Equal(Some(fp (at "home/user")), path)

[<Fact>]
let ``a tilde before a separator is the home directory`` () =
    let path = read (relative "~/docs")

    Assert.Equal(Some(fp (at "home/user/docs")), path)

[<Fact>]
let ``a tilde starting a name is part of the name`` () =
    let path = read "~draft.txt"

    Assert.Equal(Some(fp (at "start/~draft.txt")), path)

[<WindowsFact("Percent signs name a variable on Windows")>]
let ``a variable between percent signs is expanded on Windows`` () =
    let path = read @"%DATA%\notes.txt"

    Assert.Equal(Some(fp (at "data/notes.txt")), path)

[<WindowsFact("Percent signs name a variable on Windows")>]
let ``a variable Windows does not know is left as written`` () =
    let path = read @"%NOWHERE%\notes.txt"

    Assert.Equal(Some(fp (at "start/%NOWHERE%/notes.txt")), path)

[<WindowsFact("A dollar sign is part of a name on Windows")>]
let ``a dollar sign is no variable on Windows`` () =
    let path = read @"C:\$Recycle.Bin"

    Assert.Equal(Some(fp @"C:\$Recycle.Bin"), path)

[<UnixFact>]
let ``a variable after a dollar sign is expanded on Unix`` () =
    let path = read "$DATA/notes.txt"

    Assert.Equal(Some(fp (at "data/notes.txt")), path)

[<UnixFact>]
let ``a variable in braces after a dollar sign is expanded on Unix`` () =
    let path = read "${DATA}/notes.txt"

    Assert.Equal(Some(fp (at "data/notes.txt")), path)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData("\"\"")>]
[<InlineData("first\r\nsecond")>]
[<InlineData("first\nsecond")>]
[<InlineData("work\u0000notes")>]
let ``text that is no single path is no path`` (text: string) =
    let path = read text

    Assert.Equal(None, path)

[<Fact>]
let ``a URI that names no file is no path`` () =
    let path = read "file://"

    Assert.Equal(None, path)
