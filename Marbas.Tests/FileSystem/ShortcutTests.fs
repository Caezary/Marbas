module Marbas.Tests.FileSystem.ShortcutTests

open System
open System.IO
open System.Text
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests
open Marbas.Tests.ShortcutBytes

let private at = Root.at

let private fp = FileSystemPath.create

let private idList =
    Array.append (BitConverter.GetBytes(uint16 6)) [| 0x04uy; 0x00uy; 0xFFuy; 0xFFuy; 0x00uy; 0x00uy |]

let private wideLocalInfo (basePath: string) (suffix: string) =
    let narrowBase = ansi "?"
    let narrowSuffix = ansi ""
    let wideBase = wide basePath
    let wideSuffix = wide suffix
    let narrowSuffixAt = LongInfoHeader + narrowBase.Length
    let wideBaseAt = narrowSuffixAt + narrowSuffix.Length
    let wideSuffixAt = wideBaseAt + wideBase.Length
    let size = wideSuffixAt + wideSuffix.Length

    Array.concat
        [ int32s [ size; LongInfoHeader; 1; 0; LongInfoHeader; 0; narrowSuffixAt; wideBaseAt; wideSuffixAt ]
          narrowBase
          narrowSuffix
          wideBase
          wideSuffix ]

let private networkInfo (share: string) (suffix: string) =
    let shareName = ansi share
    let network = Array.append (int32s [ NetworkHeader + shareName.Length; 0; NetworkHeader; 0; 0 ]) shareName
    let suffixAt = ShortInfoHeader + network.Length
    let suffixBytes = ansi suffix
    let size = suffixAt + suffixBytes.Length

    Array.concat [ int32s [ size; ShortInfoHeader; 2; 0; 0; ShortInfoHeader; suffixAt ]; network; suffixBytes ]

let private parsed bytes = Shortcut.parse bytes

[<Fact>]
let ``a shortcut to a local file names the path it holds`` () =
    let bytes = shortcut HasLinkInfo [ localInfo (ansi @"C:\work\notes.txt") (ansi "") ]

    let target = parsed bytes

    Assert.Equal(Some(FullPath(fp @"C:\work\notes.txt")), target)

[<Fact>]
let ``the common suffix follows the local base path`` () =
    let bytes = shortcut HasLinkInfo [ localInfo (ansi @"C:\work\") (ansi @"docs\notes.txt") ]

    let target = parsed bytes

    Assert.Equal(Some(FullPath(fp @"C:\work\docs\notes.txt")), target)

[<Fact>]
let ``the list of shell items before the link information is stepped over`` () =
    let bytes =
        shortcut (HasIdList ||| HasLinkInfo) [ idList; localInfo (ansi @"C:\work\notes.txt") (ansi "") ]

    let target = parsed bytes

    Assert.Equal(Some(FullPath(fp @"C:\work\notes.txt")), target)

[<Fact>]
let ``a path held in UTF-16 is read in full, whatever its letters`` () =
    let bytes = shortcut HasLinkInfo [ wideLocalInfo @"C:\praca\" "zażółć.txt" ]

    let target = parsed bytes

    Assert.Equal(Some(FullPath(fp @"C:\praca\zażółć.txt")), target)

[<Fact>]
let ``a shortcut to a network share names the share and the path below it`` () =
    let bytes = shortcut HasLinkInfo [ networkInfo @"\\server\share" @"docs\notes.txt" ]

    let target = parsed bytes

    Assert.Equal(Some(FullPath(fp @"\\server\share\docs\notes.txt")), target)

[<Fact>]
let ``a shortcut holding only a relative path names it relative to itself`` () =
    let bytes =
        shortcut (HasName ||| HasRelativePath ||| IsUnicode) [ counted "notes"; counted @"..\work\notes.txt" ]

    let target = parsed bytes

    Assert.Equal(Some(RelativePath @"..\work\notes.txt"), target)

[<Fact>]
let ``a local path in a code page other than ASCII gives way to the relative path`` () =
    let bytes =
        shortcut
            (HasLinkInfo ||| HasRelativePath ||| IsUnicode)
            [ localInfo (latin @"C:\praca\Ärger.txt") (ansi ""); counted @".\Ärger.txt" ]

    let target = parsed bytes

    Assert.Equal(Some(RelativePath @".\Ärger.txt"), target)

[<Fact>]
let ``a shortcut naming no place has no target`` () =
    let bytes = shortcut 0 []

    let target = parsed bytes

    Assert.Equal(None, target)

[<Fact>]
let ``bytes that do not open with the shell link header are no shortcut`` () =
    let bytes = Encoding.ASCII.GetBytes "[InternetShortcut]\r\nURL=https://example.com\r\n"

    let target = parsed bytes

    Assert.Equal(None, target)

[<Fact>]
let ``a shortcut cut off inside its link information has no target`` () =
    let whole = shortcut HasLinkInfo [ localInfo (ansi @"C:\work\notes.txt") (ansi "") ]

    let target = parsed whole[.. whole.Length - 6]

    Assert.Equal(None, target)

[<Theory>]
[<InlineData("start.lnk")>]
[<InlineData("Start.LNK")>]
let ``a name ending in .lnk is a shortcut`` (name: string) =
    let recognised = Shortcut.isShortcut (EntryName.create name)

    Assert.True recognised

[<Theory>]
[<InlineData("start.lnk.txt")>]
[<InlineData("lnk")>]
[<InlineData("start.url")>]
let ``a name not ending in .lnk is no shortcut`` (name: string) =
    let recognised = Shortcut.isShortcut (EntryName.create name)

    Assert.False recognised

[<Fact>]
let ``the target of a shortcut on disk is read from its bytes`` () =
    let fs = MockFileSystem()
    let bytes = shortcut HasLinkInfo [ localInfo (ansi (at "work/notes.txt")) (ansi "") ]
    fs.AddFile(at "start/notes.lnk", MockFileData bytes)

    let target = Shortcut.target fs (fp (at "start/notes.lnk"))

    Assert.Equal(Some(fp (at "work/notes.txt")), target)

[<Fact>]
let ``a relative target is taken from the directory holding the shortcut`` () =
    let fs = MockFileSystem()
    let relative = Path.Combine("..", "work", "notes.txt")
    fs.AddFile(at "start/notes.lnk", MockFileData(shortcut (HasRelativePath ||| IsUnicode) [ counted relative ]))

    let target = Shortcut.target fs (fp (at "start/notes.lnk"))

    Assert.Equal(Some(fp (at "work/notes.txt")), target)

[<Fact>]
let ``a shortcut whose bytes say nothing has no target`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/broken.lnk", MockFileData "not a shortcut")

    let target = Shortcut.target fs (fp (at "start/broken.lnk"))

    Assert.Equal(None, target)

[<Fact>]
let ``a shortcut that is not there has no target`` () =
    let fs = MockFileSystem()

    let target = Shortcut.target fs (fp (at "start/gone.lnk"))

    Assert.Equal(None, target)
