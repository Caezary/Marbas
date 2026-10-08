module Marbas.Tests.Ui.Format.TimestampTextTests

open System
open Xunit
open Marbas.Ui.Format
open Marbas.Tests

[<Fact>]
let ``a stamp is written down to the second`` () =
    let text = TimestampText.format (DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7)))

    Assert.Equal("2024-03-01 09:05:07", text)

[<Fact>]
let ``what is written down is read back`` () =
    let stamp = DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7))

    Assert.Equal(Some stamp, TimestampText.tryParse (TimestampText.format stamp))

[<Fact>]
let ``a stamp is read whatever the space around it`` () =
    Assert.Equal(Some(DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7))), TimestampText.tryParse "  2024-03-01 09:05:07  ")

[<Fact>]
let ``a stamp is written in the terms of its own offset`` () =
    let elsewhere =
        DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7), TimeSpan.FromHours 5.0)

    Assert.Equal("2024-03-01 09:05:07", TimestampText.format elsewhere)

[<Fact>]
let ``a stamp with no offset written down is read in the machine's own`` () =
    let read: DateTimeOffset =
        TimestampText.tryParse "2024-03-01 09:05:07" |> Expect.some

    Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(DateTime(2024, 3, 1, 9, 5, 7)), read.Offset)

[<Fact>]
let ``text that is no stamp reads as nothing`` () =
    Assert.Equal(None, TimestampText.tryParse "yesterday")
    Assert.Equal(None, TimestampText.tryParse "2024-13-45 99:99:99")
    Assert.Equal(None, TimestampText.tryParse "")

[<Fact>]
let ``a stamp is read the same way whatever the machine calls a date`` () =
    let culture = Globalization.CultureInfo "de-DE"
    Threading.Thread.CurrentThread.CurrentCulture <- culture

    try
        Assert.Equal("2024-03-01 09:05:07", TimestampText.format (DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7))))
        Assert.Equal(Some(DateTimeOffset(DateTime(2024, 3, 1, 9, 5, 7))), TimestampText.tryParse "2024-03-01 09:05:07")
    finally
        Threading.Thread.CurrentThread.CurrentCulture <- Globalization.CultureInfo.InvariantCulture
