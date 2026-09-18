module Marbas.Tests.FileSystem.SearchListingTests

open System
open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.FileSystem.DirectoryListing
open Marbas.Tests

let private at = Root.at

let private relative = Root.relative

let private fp = FileSystemPath.create

let private mockFs () =
    let fs = MockFileSystem()
    fs.AddFile(at "start/notes.txt", MockFileData "hi")
    fs.AddFile(at "start/docs/inner.txt", MockFileData "hi")
    fs.AddFile(at "start/.secret.txt", MockFileData "hi")
    fs.AddFile(at "other/notes.txt", MockFileData "hi")
    fs :> IFileSystem

let private query =
    { What = FindWhat.create (NameMasks.read "*.txt") None FilesOnly
      Scope = FromDirectory
      Hidden = Concealed
      Links = StopAtLinks
      Archives = SkipArchives }

let private entry name visibility =
    { Name = EntryName.create name
      Kind = File
      Size = Some 2L
      Measured = None
      Modified = DateTimeOffset.MinValue
      Visibility = visibility
      Executability = NotExecutable
      LinkTarget = None }

let private hitIn where name =
    { Location = OnDisk(fp (at where))
      Entry = entry name Plain }

let private found roots hits =
    { Query = query
      Ground = { Roots = roots; OtherRoots = [] }
      Hits = hits
      Origin = OnDisk(fp (at "start"))
      Returning = None }

let private fromStart hits =
    found [ OnDisk(fp (at "start")) ] hits

let private named entries =
    entries |> List.map (_.Name >> EntryName.value)

let private listed fs hidden found =
    SearchListing.listForDisplay fs SortOrder.byName hidden found |> Expect.ok

[<Fact>]
let ``a hit is named by its path below where the search started`` () =
    let fs = mockFs ()

    let entries = listed fs Concealed (fromStart [ hitIn "start/docs" "inner.txt" ])

    Assert.Equal<string list>([ ".."; relative "docs/inner.txt" ], named entries)

[<Fact>]
let ``a hit in the directory the search started from is named by itself`` () =
    let fs = mockFs ()

    let entries = listed fs Concealed (fromStart [ hitIn "start" "notes.txt" ])

    Assert.Equal<string list>([ ".."; "notes.txt" ], named entries)

[<Fact>]
let ``hits from several starting places keep their whole paths`` () =
    let fs = mockFs ()
    let places = [ OnDisk(fp (at "start")); OnDisk(fp (at "other")) ]
    let hits = [ hitIn "start" "notes.txt"; hitIn "other" "notes.txt" ]

    let entries = listed fs Concealed (found places hits)

    Assert.Equal<string list>([ ".."; at "other/notes.txt"; at "start/notes.txt" ], named entries)

[<Fact>]
let ``a hit that is gone by now is left out`` () =
    let fs = mockFs ()
    let hits = [ hitIn "start" "notes.txt"; hitIn "start" "gone.txt" ]

    let entries = listed fs Concealed (fromStart hits)

    Assert.Equal<string list>([ ".."; "notes.txt" ], named entries)

[<Fact>]
let ``a concealed hit is listed only where the panel shows hidden entries`` () =
    let fs = mockFs ()

    let hidden =
        { Location = OnDisk(fp (at "start"))
          Entry = entry ".secret.txt" Hidden }

    let concealing = listed fs Concealed (fromStart [ hidden ])
    let showing = listed fs Shown (fromStart [ hidden ])

    Assert.Equal<string list>([ ".." ], named concealing)
    Assert.Equal<string list>([ ".."; ".secret.txt" ], named showing)

[<Fact>]
let ``the name a hit is shown under leads back to that hit`` () =
    let fs = mockFs ()
    let hit = hitIn "start/docs" "inner.txt"

    let back = SearchListing.hitFor fs (fromStart [ hit ]) (EntryName.create (relative "docs/inner.txt"))

    Assert.Equal(Some hit, back)

[<Fact>]
let ``a name that stands for no hit leads nowhere`` () =
    let fs = mockFs ()

    let back = SearchListing.hitFor fs (fromStart [ hitIn "start" "notes.txt" ]) ParentName

    Assert.Equal(None, back)
