namespace Marbas.FileSystem

type FileExtension = private FileExtension of string

type EditorPath = private EditorPath of string

type EditorAssociation =
    { Extension: FileExtension
      Editor: EditorPath }

type EditorSettings =
    { DefaultEditor: EditorPath option
      Associations: EditorAssociation list }

module FileExtension =

    let tryCreate (raw: string) =
        match raw.Trim().TrimStart('.').ToLowerInvariant() with
        | "" -> None
        | text -> Some(FileExtension text)

    let value (FileExtension text) = text

    let ofName (name: EntryName) = EntryName.extension name |> tryCreate

module EditorPath =

    let tryCreate (raw: string) =
        match raw.Trim() with
        | "" -> None
        | text -> Some(EditorPath text)

    let value (EditorPath text) = text

module EditorSettings =

    let none =
        { DefaultEditor = None
          Associations = [] }

    let private ordered associations =
        associations |> List.sortBy (_.Extension >> FileExtension.value)

    let private without extension associations =
        associations |> List.filter (fun association -> association.Extension <> extension)

    let associate extension editor settings =
        { settings with
            Associations =
                { Extension = extension; Editor = editor } :: without extension settings.Associations
                |> ordered }

    let forget extension settings =
        { settings with
            Associations = without extension settings.Associations }

    let associated extension settings =
        settings.Associations
        |> List.tryFind (fun association -> association.Extension = extension)
        |> Option.map _.Editor

    let editorFor (name: EntryName) settings =
        FileExtension.ofName name
        |> Option.bind (fun extension -> associated extension settings)
        |> Option.orElse settings.DefaultEditor
