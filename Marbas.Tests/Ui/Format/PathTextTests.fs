module Marbas.Tests.Ui.Format.PathTextTests

open Xunit
open Marbas.Ui.Format

let private shorten width text = PathText.shorten '\\' width text

[<Fact>]
let ``a path that fits is left as it was written`` () =
    let text = shorten 40 @"C:\projects\Marbas\_documentation"

    Assert.Equal(@"C:\projects\Marbas\_documentation", text)

[<Fact>]
let ``a path that does not fit keeps the root, the directory above and the one it stands in`` () =
    let text = shorten 20 @"C:\projects\work\Marbas\src\Ui"

    Assert.Equal(@"C:\...\src\Ui", text)

[<Fact>]
let ``a path too long even for the directory above drops it`` () =
    let text = shorten 16 @"C:\projects\a very long name\here"

    Assert.Equal(@"C:\...\here", text)

[<Fact>]
let ``a name too long for the width at all shows its end`` () =
    let text = shorten 8 @"C:\projects\an extraordinarily long name"

    Assert.Equal("ong name", text)

[<Fact>]
let ``a unix path keeps the separator its root is written as`` () =
    let text = PathText.shorten '/' 20 "/home/user/projects/marbas/src"

    Assert.Equal("/.../marbas/src", text)

[<Fact>]
let ``a shortening that would be longer than the path is not made`` () =
    let text = shorten 6 @"C:\work"

    Assert.Equal(":\\work", text)

[<Theory>]
[<InlineData(0)>]
[<InlineData(-3)>]
let ``a width of nothing leaves nothing`` (width: int) =
    let text = shorten width @"C:\projects\Marbas"

    Assert.Equal("", text)

[<Fact>]
let ``a width too narrow even for the root keeps the directory the panel stands in`` () =
    let text = shorten 12 @"C:\projects\marbas\source\Ui\Panels"

    Assert.Equal(@"...\Panels", text)
