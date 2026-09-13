module Marbas.Tests.Ui.Format.CompactSizeTests

open Xunit
open Marbas.Ui.Format

[<Theory>]
[<InlineData(0L, "0 B")>]
[<InlineData(1L, "1 B")>]
[<InlineData(1023L, "1023 B")>]
let ``a size below a kilobyte is spelled in bytes`` (bytes: int64) (expected: string) =
    let text = CompactSize.describe bytes

    Assert.Equal(expected, text)

[<Theory>]
[<InlineData(1024L, "1.0 KB")>]
[<InlineData(1048576L, "1.0 MB")>]
[<InlineData(1073741824L, "1.0 GB")>]
[<InlineData(1099511627776L, "1.0 TB")>]
[<InlineData(1125899906842624L, "1.0 PB")>]
[<InlineData(1152921504606846976L, "1.0 EB")>]
let ``a size is spelled in the largest unit it reaches`` (bytes: int64) (expected: string) =
    let text = CompactSize.describe bytes

    Assert.Equal(expected, text)

[<Theory>]
[<InlineData(1536L, "1.5 KB")>]
[<InlineData(10485760L, "10.0 MB")>]
[<InlineData(64023257088L, "59.6 GB")>]
[<InlineData(1000204886016L, "931.5 GB")>]
[<InlineData(2251799813685248L, "2.0 PB")>]
[<InlineData(5764607523034234880L, "5.0 EB")>]
let ``the whole part and the tenth come from the unit reached`` (bytes: int64) (expected: string) =
    let text = CompactSize.describe bytes

    Assert.Equal(expected, text)

[<Theory>]
[<InlineData(1075L, "1.0 KB")>]
[<InlineData(1076L, "1.1 KB")>]
[<InlineData(1280L, "1.3 KB")>]
[<InlineData(1279L, "1.2 KB")>]
[<InlineData(1127L, "1.1 KB")>]
[<InlineData(1638L, "1.6 KB")>]
let ``the tenth is rounded to the nearest, a half upwards`` (bytes: int64) (expected: string) =
    let text = CompactSize.describe bytes

    Assert.Equal(expected, text)

[<Theory>]
[<InlineData(2047L, "2.0 KB")>]
[<InlineData(10239L, "10.0 KB")>]
[<InlineData(1047551L, "1023.0 KB")>]
let ``a tenth rounded up to ten carries into the whole part`` (bytes: int64) (expected: string) =
    let text = CompactSize.describe bytes

    Assert.Equal(expected, text)

[<Theory>]
[<InlineData(1048575L, "1.0 MB")>]
[<InlineData(1073741823L, "1.0 GB")>]
[<InlineData(1125899906842623L, "1.0 PB")>]
[<InlineData(1152921504606846975L, "1.0 EB")>]
let ``a size rounded up to a thousand and twenty-four carries into the next unit`` (bytes: int64) (expected: string) =
    let text = CompactSize.describe bytes

    Assert.Equal(expected, text)

[<Fact>]
let ``the largest size there is rounds without overflowing`` () =
    let text = CompactSize.describe System.Int64.MaxValue

    Assert.Equal("8.0 EB", text)
