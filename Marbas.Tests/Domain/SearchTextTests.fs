module Marbas.Tests.Domain.SearchTextTests

open Xunit
open Marbas.Domain

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
[<InlineData("set", "setup.exe")>]
[<InlineData("up", "setup.exe")>]
[<InlineData("EXE", "setup.exe")>]
[<InlineData("set", "SETUP.EXE")>]
[<InlineData(".", "..")>]
let ``a search matches any name containing it, whatever the case`` raw name =
    let matched = SearchText.matches (text raw) (EntryName.create name)

    Assert.True matched

[<Theory>]
[<InlineData("sup", "setup.exe")>]
[<InlineData("exe.", "setup.exe")>]
let ``a search passes over a name that does not contain it`` raw name =
    let matched = SearchText.matches (text raw) (EntryName.create name)

    Assert.False matched

[<Theory>]
[<InlineData("notes", @"C:\work\notes.md")>]
[<InlineData("WORK", @"C:\work\notes.md")>]
[<InlineData(@"k\n", @"C:\work\notes.md")>]
[<InlineData("my d", "C:/my docs")>]
[<InlineData("łódź", "C:/Łódź")>]
let ``a search is found anywhere in a text, whatever the case`` raw shown =
    let found = SearchText.foundIn shown (text raw)

    Assert.True found

[<Theory>]
[<InlineData("notez", @"C:\work\notes.md")>]
[<InlineData("mydocs", "C:/my docs")>]
let ``a search is not found in a text that does not hold it`` raw shown =
    let found = SearchText.foundIn shown (text raw)

    Assert.False found
