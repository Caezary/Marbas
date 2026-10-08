module Marbas.Tests.Domain.ProgramSettingsTests

open Xunit
open Marbas.Domain

let private en = EntryName.create

let private ext raw =
    FileExtension.tryCreate raw |> Option.get

let private program raw = ProgramPath.tryCreate raw |> Option.get

let private associated role pairs =
    pairs
    |> List.fold
        (fun settings (extension, path) -> ProgramSettings.associate role (ext extension) (program path) settings)
        ProgramSettings.none

let private editing pairs = associated ForEditing pairs

[<Fact>]
let ``an extension is kept without its dot and in lower case`` () =
    let extension = ext " .TXT "

    Assert.Equal("txt", FileExtension.value extension)

[<Fact>]
let ``a blank extension is no extension`` () =
    Assert.Equal(None, FileExtension.tryCreate "  ")
    Assert.Equal(None, FileExtension.tryCreate ".")

[<Fact>]
let ``the extension of a name is what follows its last dot`` () =
    Assert.Equal(Some(ext "fs"), FileExtension.ofName (en "Program.fs"))

[<Fact>]
let ``a name without an extension has none`` () =
    Assert.Equal(None, FileExtension.ofName (en "README"))
    Assert.Equal(None, FileExtension.ofName (en ".gitignore"))

[<Fact>]
let ``a program path is trimmed`` () =
    Assert.Equal("code.exe", ProgramPath.value (program "  code.exe  "))

[<Fact>]
let ``a blank program path is no program`` () =
    Assert.Equal(None, ProgramPath.tryCreate "   ")

[<Fact>]
let ``nothing configured names no program`` () =
    Assert.Equal(None, ProgramSettings.programFor ForEditing (en "notes.txt") ProgramSettings.none)
    Assert.Equal(None, ProgramSettings.programFor ForOpening (en "notes.txt") ProgramSettings.none)

[<Fact>]
let ``a name is edited by the program its extension is associated with`` () =
    let settings = editing [ "txt", "notepad.exe"; "fs", "rider.exe" ]

    Assert.Equal(Some(program "rider.exe"), ProgramSettings.programFor ForEditing (en "Program.fs") settings)

[<Fact>]
let ``a name is opened by the program its extension is associated with`` () =
    let settings = associated ForOpening [ "html", "firefox.exe" ]

    Assert.Equal(Some(program "firefox.exe"), ProgramSettings.programFor ForOpening (en "page.html") settings)

[<Fact>]
let ``the same extension carries one program per role`` () =
    let settings =
        editing [ "html", "code.exe" ]
        |> ProgramSettings.associate ForOpening (ext "html") (program "firefox.exe")

    Assert.Equal(Some(program "code.exe"), ProgramSettings.programFor ForEditing (en "page.html") settings)
    Assert.Equal(Some(program "firefox.exe"), ProgramSettings.programFor ForOpening (en "page.html") settings)

[<Fact>]
let ``a role sees only the associations made in it`` () =
    let settings = editing [ "txt", "notepad.exe" ]

    Assert.Equal(None, ProgramSettings.programFor ForOpening (en "notes.txt") settings)
    Assert.Equal(None, ProgramSettings.associated ForOpening (ext "txt") settings)

[<Fact>]
let ``the extension is matched whatever the case of the name`` () =
    let settings = editing [ "txt", "notepad.exe" ]

    Assert.Equal(Some(program "notepad.exe"), ProgramSettings.programFor ForEditing (en "NOTES.TXT") settings)

[<Fact>]
let ``an extension nothing is associated with falls back to the default editor`` () =
    let settings =
        { editing [ "txt", "notepad.exe" ] with
            DefaultEditor = Some(program "code.exe") }

    Assert.Equal(Some(program "code.exe"), ProgramSettings.programFor ForEditing (en "Program.fs") settings)

[<Fact>]
let ``a name without an extension falls back to the default editor`` () =
    let settings =
        { ProgramSettings.none with
            DefaultEditor = Some(program "code.exe") }

    Assert.Equal(Some(program "code.exe"), ProgramSettings.programFor ForEditing (en "README") settings)

[<Fact>]
let ``nothing opens a file the default editor would have edited`` () =
    let settings =
        { ProgramSettings.none with
            DefaultEditor = Some(program "code.exe") }

    Assert.Equal(None, ProgramSettings.programFor ForOpening (en "notes.txt") settings)

[<Fact>]
let ``associating an extension that is already known in the role replaces it`` () =
    let settings = editing [ "txt", "notepad.exe"; "TXT", "code.exe" ]

    Assert.Equal(Some(program "code.exe"), ProgramSettings.programFor ForEditing (en "notes.txt") settings)
    Assert.Equal(1, settings.Associations.Length)

[<Fact>]
let ``associations are kept in role and extension order`` () =
    let settings =
        editing [ "txt", "notepad.exe"; "fs", "rider.exe" ]
        |> ProgramSettings.associate ForOpening (ext "md") (program "typora.exe")
        |> ProgramSettings.associate ForOpening (ext "html") (program "firefox.exe")

    let listed =
        settings.Associations
        |> List.map (fun association -> association.Role, FileExtension.value association.Extension)

    Assert.Equal<(AssociationRole * string) list>(
        [ ForEditing, "fs"; ForEditing, "txt"; ForOpening, "html"; ForOpening, "md" ],
        listed
    )

[<Fact>]
let ``one role at a time is what a list of a role holds`` () =
    let settings =
        editing [ "txt", "notepad.exe" ]
        |> ProgramSettings.associate ForOpening (ext "html") (program "firefox.exe")

    let opening = ProgramSettings.inRole ForOpening settings

    Assert.Equal(ext "html", (Assert.Single opening).Extension)

[<Fact>]
let ``forgetting an extension drops its association`` () =
    let settings = editing [ "txt", "notepad.exe"; "fs", "rider.exe" ]

    let left = ProgramSettings.forget ForEditing (ext "fs") settings

    Assert.Equal(None, ProgramSettings.programFor ForEditing (en "Program.fs") left)
    Assert.Equal(Some(program "notepad.exe"), ProgramSettings.programFor ForEditing (en "notes.txt") left)

[<Fact>]
let ``forgetting an extension leaves the other role alone`` () =
    let settings =
        editing [ "html", "code.exe" ]
        |> ProgramSettings.associate ForOpening (ext "html") (program "firefox.exe")

    let left = ProgramSettings.forget ForOpening (ext "html") settings

    Assert.Equal(Some(program "code.exe"), ProgramSettings.programFor ForEditing (en "page.html") left)
    Assert.Equal(None, ProgramSettings.programFor ForOpening (en "page.html") left)

[<Fact>]
let ``forgetting an extension nothing is associated with changes nothing`` () =
    let settings = editing [ "txt", "notepad.exe" ]

    Assert.Equal(settings, ProgramSettings.forget ForEditing (ext "md") settings)
