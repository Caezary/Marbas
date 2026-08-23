module Marbas.Tests.FileSystem.EditorSettingsTests

open Xunit
open Marbas.FileSystem

let private en = EntryName.create

let private ext raw =
    FileExtension.tryCreate raw |> Option.get

let private editor raw =
    EditorPath.tryCreate raw |> Option.get

let private associated pairs =
    pairs
    |> List.fold (fun settings (extension, path) -> EditorSettings.associate (ext extension) (editor path) settings) EditorSettings.none

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
let ``an editor path is trimmed`` () =
    Assert.Equal("code.exe", EditorPath.value (editor "  code.exe  "))

[<Fact>]
let ``a blank editor path is no editor`` () =
    Assert.Equal(None, EditorPath.tryCreate "   ")

[<Fact>]
let ``nothing configured names no editor`` () =
    Assert.Equal(None, EditorSettings.editorFor (en "notes.txt") EditorSettings.none)

[<Fact>]
let ``a name is edited by the editor its extension is associated with`` () =
    let settings = associated [ "txt", "notepad.exe"; "fs", "rider.exe" ]

    Assert.Equal(Some(editor "rider.exe"), EditorSettings.editorFor (en "Program.fs") settings)

[<Fact>]
let ``the extension is matched whatever the case of the name`` () =
    let settings = associated [ "txt", "notepad.exe" ]

    Assert.Equal(Some(editor "notepad.exe"), EditorSettings.editorFor (en "NOTES.TXT") settings)

[<Fact>]
let ``an extension nothing is associated with falls back to the default editor`` () =
    let settings =
        { associated [ "txt", "notepad.exe" ] with
            DefaultEditor = Some(editor "code.exe") }

    Assert.Equal(Some(editor "code.exe"), EditorSettings.editorFor (en "Program.fs") settings)

[<Fact>]
let ``a name without an extension falls back to the default editor`` () =
    let settings =
        { EditorSettings.none with
            DefaultEditor = Some(editor "code.exe") }

    Assert.Equal(Some(editor "code.exe"), EditorSettings.editorFor (en "README") settings)

[<Fact>]
let ``associating an extension that is already known replaces it`` () =
    let settings = associated [ "txt", "notepad.exe"; "TXT", "code.exe" ]

    Assert.Equal(Some(editor "code.exe"), EditorSettings.editorFor (en "notes.txt") settings)
    Assert.Equal(1, settings.Associations.Length)

[<Fact>]
let ``associations are kept in extension order`` () =
    let settings = associated [ "txt", "notepad.exe"; "fs", "rider.exe"; "md", "code.exe" ]

    let extensions = settings.Associations |> List.map (_.Extension >> FileExtension.value)

    Assert.Equal<string list>([ "fs"; "md"; "txt" ], extensions)

[<Fact>]
let ``forgetting an extension drops its association`` () =
    let settings = associated [ "txt", "notepad.exe"; "fs", "rider.exe" ]

    let left = EditorSettings.forget (ext "fs") settings

    Assert.Equal(None, EditorSettings.editorFor (en "Program.fs") left)
    Assert.Equal(Some(editor "notepad.exe"), EditorSettings.editorFor (en "notes.txt") left)

[<Fact>]
let ``forgetting an extension nothing is associated with changes nothing`` () =
    let settings = associated [ "txt", "notepad.exe" ]

    Assert.Equal(settings, EditorSettings.forget (ext "md") settings)
