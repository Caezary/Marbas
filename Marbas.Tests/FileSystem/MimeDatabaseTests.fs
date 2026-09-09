module Marbas.Tests.FileSystem.MimeDatabaseTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private ext raw = Expect.some (FileExtension.tryCreate raw)

let private globs =
    [ "# This file was automatically generated"
      "text/html:*.html"
      "text/html:*.htm"
      "application/pdf:*.pdf"
      "text/plain:*.txt"
      "" ]

let private weighted =
    [ "50:text/html:*.html"; "10:application/xhtml+xml:*.html" ]

let private applications =
    [ "[Default Applications]"
      "text/html=firefox.desktop;chromium.desktop;"
      "application/pdf=evince.desktop;"
      ""
      "[Added Associations]"
      "text/html=falkon.desktop;"
      ""
      "[Removed Associations]"
      "text/html=chromium.desktop;" ]

[<Fact>]
let ``every type a glob gives the extension is answered`` () =
    let types = MimeDatabase.typesFor (ext "html") globs

    Assert.Equal<string list>([ "text/html" ], types)

[<Fact>]
let ``a weighted glob line names its type just the same`` () =
    let types = MimeDatabase.typesFor (ext "html") weighted

    Assert.Equal<string list>([ "text/html"; "application/xhtml+xml" ], types)

[<Fact>]
let ``an extension no glob names has no type`` () =
    Assert.Empty(MimeDatabase.typesFor (ext "zzq") globs)

[<Fact>]
let ``a glob that is not a plain extension is passed over`` () =
    let types = MimeDatabase.typesFor (ext "gz") [ "application/x-tar:*.tar.gz"; "text/x-makefile:Makefile" ]

    Assert.Empty types

[<Fact>]
let ``the default applications come before the added ones`` () =
    let listed = MimeDatabase.applicationsFor "text/html" applications

    Assert.Equal<string list>([ "firefox.desktop"; "falkon.desktop" ], listed)

[<Fact>]
let ``an application the file removes is not listed`` () =
    let listed = MimeDatabase.applicationsFor "text/html" applications

    Assert.DoesNotContain("chromium.desktop", listed)

[<Fact>]
let ``a type nothing is registered for lists nothing`` () =
    Assert.Empty(MimeDatabase.applicationsFor "image/png" applications)

[<Fact>]
let ``a key outside any known section is passed over`` () =
    let listed =
        MimeDatabase.applicationsFor "text/html" [ "text/html=stray.desktop;"; "[Default Applications]" ]

    Assert.Empty listed
