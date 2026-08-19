module Marbas.Tests.FileSystem.SearchTextTests

open Xunit
open Marbas.FileSystem

let private text (raw: string) =
    raw
    |> Seq.skip 1
    |> Seq.fold (fun search character -> SearchText.append character search) (SearchText.ofChar raw[0])

[<Fact>]
let ``a search starts with the character that opened it`` () =
    let search = SearchText.ofChar 'r'

    Assert.Equal("r", SearchText.value search)

[<Fact>]
let ``typing appends to the search`` () =
    let search = SearchText.ofChar 'r' |> SearchText.append 'e' |> SearchText.append 'a'

    Assert.Equal("rea", SearchText.value search)

[<Fact>]
let ``backspace drops the last character`` () =
    let search = SearchText.ofChar 'r' |> SearchText.append 'e' |> SearchText.backspace

    Assert.Equal(Some "r", search |> Option.map SearchText.value)

[<Fact>]
let ``backspace over the last character ends the search`` () =
    let search = SearchText.ofChar 'r' |> SearchText.backspace

    Assert.Equal<SearchText option>(None, search)

[<Theory>]
[<InlineData("set", "setup.exe", true)>]
[<InlineData("up", "setup.exe", true)>]
[<InlineData("EXE", "setup.exe", true)>]
[<InlineData("set", "SETUP.EXE", true)>]
[<InlineData("sup", "setup.exe", false)>]
[<InlineData(".", "..", true)>]
let ``a search matches any name containing it, whatever the case`` raw name expected =
    let matched = SearchText.matches (text raw) (EntryName.create name)

    Assert.Equal(expected, matched)
