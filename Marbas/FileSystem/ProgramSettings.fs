namespace Marbas.FileSystem

type FileExtension = private FileExtension of string

type ProgramPath = private ProgramPath of string

type AssociationRole =
    | ForEditing
    | ForOpening

type ProgramAssociation =
    { Extension: FileExtension
      Role: AssociationRole
      Program: ProgramPath }

type ProgramSettings =
    { DefaultEditor: ProgramPath option
      Associations: ProgramAssociation list }

module FileExtension =

    let tryCreate (raw: string) =
        match raw.Trim().TrimStart('.').ToLowerInvariant() with
        | "" -> None
        | text -> Some(FileExtension text)

    let value (FileExtension text) = text

    let ofName (name: EntryName) = EntryName.extension name |> tryCreate

module ProgramPath =

    let tryCreate (raw: string) =
        match raw.Trim() with
        | "" -> None
        | text -> Some(ProgramPath text)

    let value (ProgramPath text) = text

module ProgramSettings =

    let none =
        { DefaultEditor = None
          Associations = [] }

    let private ordered associations =
        associations
        |> List.sortBy (fun association -> association.Role, FileExtension.value association.Extension)

    let private without role extension associations =
        associations
        |> List.filter (fun association -> association.Role <> role || association.Extension <> extension)

    let associate role extension program settings =
        { settings with
            Associations =
                { Extension = extension
                  Role = role
                  Program = program }
                :: without role extension settings.Associations
                |> ordered }

    let remember association settings =
        associate association.Role association.Extension association.Program settings

    let forget role extension settings =
        { settings with
            Associations = without role extension settings.Associations }

    let associated role extension settings =
        settings.Associations
        |> List.tryFind (fun association -> association.Role = role && association.Extension = extension)
        |> Option.map _.Program

    let inRole role settings =
        settings.Associations
        |> List.filter (fun association -> association.Role = role)

    let private fallback role settings =
        match role with
        | ForEditing -> settings.DefaultEditor
        | ForOpening -> None

    let programFor role (name: EntryName) settings =
        FileExtension.ofName name
        |> Option.bind (fun extension -> associated role extension settings)
        |> Option.orElse (fallback role settings)
