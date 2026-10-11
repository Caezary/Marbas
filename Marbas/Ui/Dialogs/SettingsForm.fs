namespace Marbas.Ui.Dialogs

open FsToolkit.ErrorHandling
open Marbas.Domain

type CandidateTarget =
    | IntoDefaultEditor
    | IntoProgram

type ListsWriting =
    | ListsWritten
    | WritingLists

type SettingsForm =
    { Programs: ProgramSettings
      Target: CandidateTarget
      Lists: ListsWriting }

module SettingsForm =

    let start (programs: ProgramSettings) =
        { Programs = programs
          Target = IntoDefaultEditor
          Lists = ListsWritten }

    let associating role (extension: string) (program: string) (form: SettingsForm) =
        option {
            let! named = FileExtension.tryCreate extension
            let! picked = ProgramPath.tryCreate program

            return
                { form with
                    Programs = ProgramSettings.associate role named picked form.Programs }
        }

    let forgetting role (extension: string) (form: SettingsForm) =
        FileExtension.tryCreate extension
        |> Option.map (fun named ->
            { form with
                Programs = ProgramSettings.forget role named form.Programs })

    let targeting (target: CandidateTarget) (form: SettingsForm) = { form with Target = target }

    let refreshing (form: SettingsForm) = { form with Lists = WritingLists }

    let settled (form: SettingsForm) = { form with Lists = ListsWritten }

    let rows role (form: SettingsForm) =
        ProgramSettings.inRole role form.Programs |> List.map AssociationRow.describe

    let chosen role index (form: SettingsForm) =
        match form.Lists with
        | WritingLists -> None
        | ListsWritten -> ProgramSettings.inRole role form.Programs |> List.tryItem index

    let answer (defaultEditor: string) (form: SettingsForm) =
        { form.Programs with
            DefaultEditor = ProgramPath.tryCreate defaultEditor }
