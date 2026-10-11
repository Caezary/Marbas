module Marbas.Tests.Ui.Dialogs.SettingsFormTests

open Xunit
open Marbas.Domain
open Marbas.Ui.Dialogs
open Marbas.Tests

let private ext raw =
    Expect.some (FileExtension.tryCreate raw)

let private program raw = Expect.some (ProgramPath.tryCreate raw)

let private stored =
    { DefaultEditor = Some(program "code.exe")
      Associations =
        [ { Extension = ext "fs"
            Role = ForEditing
            Program = program "rider.exe" }
          { Extension = ext "txt"
            Role = ForEditing
            Program = program "notepad.exe" }
          { Extension = ext "html"
            Role = ForOpening
            Program = program "firefox.exe" } ] }

let private form = SettingsForm.start stored

[<Fact>]
let ``the form starts as a copy of the programs it was given`` () =
    let started = SettingsForm.start stored

    Assert.Equal(stored, started.Programs)

[<Fact>]
let ``before any field was entered the default editor is the one to fill`` () =
    let started = SettingsForm.start stored

    Assert.Equal(IntoDefaultEditor, started.Target)

[<Fact>]
let ``an extension and a program that are typed become an association in the role`` () =
    let changed = SettingsForm.associating ForEditing " .MD " " typora.exe " form

    let programs = (Expect.some changed).Programs

    Assert.Equal(Some(program "typora.exe"), ProgramSettings.associated ForEditing (ext "md") programs)
    Assert.Equal(None, ProgramSettings.associated ForOpening (ext "md") programs)

[<Fact>]
let ``an extension already known in the role takes the new program`` () =
    let changed = SettingsForm.associating ForEditing "txt" "vim" form

    let programs = (Expect.some changed).Programs

    Assert.Equal(Some(program "vim"), ProgramSettings.associated ForEditing (ext "txt") programs)
    Assert.Equal(3, programs.Associations.Length)

[<Theory>]
[<InlineData("", "typora.exe")>]
[<InlineData("   ", "typora.exe")>]
[<InlineData("md", "")>]
[<InlineData("md", "   ")>]
[<InlineData("", "")>]
let ``with nothing on either side there is nothing to associate`` (extension: string, typed: string) =
    let changed = SettingsForm.associating ForEditing extension typed form

    Assert.Equal(None, changed)

[<Fact>]
let ``the extension typed is taken out of the role chosen and no other`` () =
    let both =
        SettingsForm.associating ForOpening "txt" "reader.exe" form |> Expect.some

    let changed = SettingsForm.forgetting ForEditing "txt" both

    let programs = (Expect.some changed).Programs

    Assert.Equal(None, ProgramSettings.associated ForEditing (ext "txt") programs)
    Assert.Equal(Some(program "reader.exe"), ProgramSettings.associated ForOpening (ext "txt") programs)

[<Fact>]
let ``an extension nobody associated leaves every association where it was`` () =
    let changed = SettingsForm.forgetting ForEditing "md" form

    Assert.Equal(Some form, changed)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData(".")>]
let ``with no extension typed there is nothing to take out`` (extension: string) =
    let changed = SettingsForm.forgetting ForEditing extension form

    Assert.Equal(None, changed)

[<Fact>]
let ``the field entered last is the one the executable under the cursor fills`` () =
    let entered = SettingsForm.targeting IntoProgram form

    Assert.Equal(IntoProgram, entered.Target)

[<Fact>]
let ``entering the default editor again turns the filling back to it`` () =
    let entered =
        form
        |> SettingsForm.targeting IntoProgram
        |> SettingsForm.targeting IntoDefaultEditor

    Assert.Equal(IntoDefaultEditor, entered.Target)

[<Fact>]
let ``the rows of a role are its associations, each as its row`` () =
    let rows = SettingsForm.rows ForEditing form

    Assert.Equal<string list>(
        [ AssociationRow.describe stored.Associations[0]
          AssociationRow.describe stored.Associations[1] ],
        rows
    )

[<Fact>]
let ``the rows of the other role are its own`` () =
    let rows = SettingsForm.rows ForOpening form

    Assert.Equal<string list>([ AssociationRow.describe stored.Associations[2] ], rows)

[<Fact>]
let ``the row the cursor comes to is the association that fills the fields`` () =
    let chosen = SettingsForm.chosen ForEditing 1 form

    Assert.Equal(Some stored.Associations[1], chosen)

[<Fact>]
let ``a row is counted within its own role`` () =
    let chosen = SettingsForm.chosen ForOpening 0 form

    Assert.Equal(Some stored.Associations[2], chosen)

[<Theory>]
[<InlineData(-1)>]
[<InlineData(2)>]
let ``a cursor on no row fills nothing`` (index: int) =
    let chosen = SettingsForm.chosen ForEditing index form

    Assert.Equal(None, chosen)

[<Fact>]
let ``while the lists are written anew a cursor that moves fills nothing`` () =
    let refreshing = SettingsForm.refreshing form

    let chosen = SettingsForm.chosen ForEditing 0 refreshing

    Assert.Equal(None, chosen)

[<Fact>]
let ``once the lists are written the cursor fills the fields again`` () =
    let settled = form |> SettingsForm.refreshing |> SettingsForm.settled

    let chosen = SettingsForm.chosen ForEditing 0 settled

    Assert.Equal(Some stored.Associations[0], chosen)

[<Fact>]
let ``the answer carries the default editor that was typed`` () =
    let answer = SettingsForm.answer " vim " form

    Assert.Equal(Some(program "vim"), answer.DefaultEditor)

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
let ``a default editor left blank names no editor`` (typed: string) =
    let answer = SettingsForm.answer typed form

    Assert.Equal(None, answer.DefaultEditor)

[<Fact>]
let ``the answer carries the associations as the form left them`` () =
    let changed =
        SettingsForm.associating ForOpening "pdf" "reader.exe" form |> Expect.some

    let answer = SettingsForm.answer "code.exe" changed

    Assert.Equal<ProgramAssociation list>(changed.Programs.Associations, answer.Associations)
