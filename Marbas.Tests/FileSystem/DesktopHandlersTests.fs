module Marbas.Tests.FileSystem.DesktopHandlersTests

open System.IO.Abstractions
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Tests

let private at = Root.at

let private en = EntryName.create

let private text lines = MockFileData(String.concat "\n" lines)

let private directories =
    { Data = [ at "home/.local/share"; at "usr/share" ]
      Config = [ at "home/.config" ] }

let private desktop name program =
    text [ "[Desktop Entry]"; "Type=Application"; $"Name={name}"; $"Exec={program} %%u" ]

let private tree () =
    let fs = MockFileSystem()

    fs.AddFile(at "usr/share/mime/globs", text [ "text/html:*.html"; "text/html:*.htm"; "application/pdf:*.pdf" ])

    fs.AddFile(
        at "home/.config/mimeapps.list",
        text [ "[Default Applications]"; "text/html=firefox.desktop;falkon.desktop;" ]
    )

    fs.AddFile(at "usr/share/applications/firefox.desktop", desktop "Firefox" "/usr/bin/firefox")
    fs.AddFile(at "usr/share/applications/falkon.desktop", desktop "Falkon" "/usr/bin/falkon")
    fs

let private read (fs: IFileSystem) name =
    DesktopHandlers.read fs directories (en name)

[<Fact>]
let ``the handlers of an extension are the applications its type names`` () =
    let handlers = read (tree ()) "page.html"

    Assert.Equal<string list>([ "Firefox"; "Falkon" ], handlers |> List.map _.Name)
    Assert.Equal(ProgramPath.tryCreate "/usr/bin/firefox", Some handlers.Head.Program)

[<Fact>]
let ``an extension the mime database does not know has no handlers`` () =
    Assert.Empty(read (tree ()) "notes.zzq")

[<Fact>]
let ``a name with no extension has no handlers`` () = Assert.Empty(read (tree ()) "README")

[<Fact>]
let ``a type nothing is registered for has no handlers`` () =
    Assert.Empty(read (tree ()) "report.pdf")

[<Fact>]
let ``a desktop file the list names but no directory holds is passed over`` () =
    let fs = tree ()
    fs.RemoveFile(at "usr/share/applications/falkon.desktop")

    let handlers = read fs "page.html"

    Assert.Equal<string list>([ "Firefox" ], handlers |> List.map _.Name)

[<Fact>]
let ``the user's own applications directory is searched before the system one`` () =
    let fs = tree ()
    fs.AddFile(at "home/.local/share/applications/firefox.desktop", desktop "Firefox Nightly" "/opt/firefox/firefox")

    let handlers = read fs "page.html"

    Assert.Equal("Firefox Nightly", handlers.Head.Name)

[<Fact>]
let ``one program listed twice is one handler`` () =
    let fs = tree ()

    fs.AddFile(
        at "home/.config/mimeapps.list",
        text
            [ "[Default Applications]"
              "text/html=firefox.desktop;"
              "[Added Associations]"
              "text/html=firefox.desktop;falkon.desktop;" ]
    )

    Assert.Equal(2, (read fs "page.html").Length)

[<Fact>]
let ``a file system that holds none of it answers an empty list`` () =
    Assert.Empty(read (MockFileSystem()) "page.html")
