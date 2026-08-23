namespace Marbas.FileSystem

open System
open System.IO.Abstractions

type PanelSettings =
    { Path: FileSystemPath
      Sort: SortOrder
      Hidden: HiddenEntries }

type Settings =
    { Left: PanelSettings
      Right: PanelSettings
      LastCreatedDirectory: EntryName option }

module Settings =

    [<Literal>]
    let private ConfigDirectory = "Marbas"

    [<Literal>]
    let private ConfigFile = "settings.conf"

    [<Literal>]
    let private ConfigHome = ".config"

    [<Literal>]
    let private LeftPrefix = "left."

    [<Literal>]
    let private RightPrefix = "right."

    [<Literal>]
    let private PathSetting = "path"

    [<Literal>]
    let private SortSetting = "sort"

    [<Literal>]
    let private DirectionSetting = "direction"

    [<Literal>]
    let private HiddenSetting = "hidden"

    [<Literal>]
    let private DirectoryPrefix = "mkdir."

    [<Literal>]
    let private NameSetting = "name"

    let private sortText key =
        match key with
        | ByName -> "name"
        | ByExtension -> "extension"
        | ByDate -> "date"
        | BySize -> "size"

    let private sortOf text =
        match text with
        | "name" -> Some ByName
        | "extension" -> Some ByExtension
        | "date" -> Some ByDate
        | "size" -> Some BySize
        | _ -> None

    let private directionText direction =
        match direction with
        | Ascending -> "ascending"
        | Descending -> "descending"

    let private directionOf text =
        match text with
        | "ascending" -> Some Ascending
        | "descending" -> Some Descending
        | _ -> None

    let private hiddenText hidden =
        match hidden with
        | Shown -> "shown"
        | Concealed -> "concealed"

    let private hiddenOf text =
        match text with
        | "shown" -> Some Shown
        | "concealed" -> Some Concealed
        | _ -> None

    let private panelOn path =
        { Path = path
          Sort = SortOrder.byName
          Hidden = Concealed }

    let forPath path : Settings =
        { Left = panelOn path
          Right = panelOn path
          LastCreatedDirectory = None }

    let location (fs: IFileSystem) (applicationData: string) (userProfile: string) =
        let home =
            if String.IsNullOrWhiteSpace applicationData then
                fs.Path.Combine(userProfile, ConfigHome)
            else
                applicationData

        fs.Path.Combine(home, ConfigDirectory, ConfigFile) |> FileSystemPath.create

    let private line prefix setting value = $"{prefix}{setting}={value}"

    let private panelLines prefix (panel: PanelSettings) =
        [ line prefix PathSetting (FileSystemPath.value panel.Path)
          line prefix SortSetting (sortText panel.Sort.Key)
          line prefix DirectionSetting (directionText panel.Sort.Direction)
          line prefix HiddenSetting (hiddenText panel.Hidden) ]

    let private directoryLines (name: EntryName option) =
        name
        |> Option.map (EntryName.value >> line DirectoryPrefix NameSetting)
        |> Option.toList

    let format (settings: Settings) =
        panelLines LeftPrefix settings.Left
        @ panelLines RightPrefix settings.Right
        @ directoryLines settings.LastCreatedDirectory

    let private entry (text: string) =
        match text.Split('=', 2) with
        | [| key; value |] -> Some(key.Trim().ToLowerInvariant(), value.Trim())
        | _ -> None

    let private entries lines =
        lines
        |> List.choose entry
        |> List.fold (fun read (key, value) -> Map.add key value read) Map.empty

    let private panelFrom (read: Map<string, string>) prefix (fallback: PanelSettings) =
        let lookup setting = Map.tryFind (prefix + setting) read
        let token setting = lookup setting |> Option.map _.ToLowerInvariant()

        { Path =
            lookup PathSetting
            |> Option.filter (String.IsNullOrEmpty >> not)
            |> Option.map FileSystemPath.create
            |> Option.defaultValue fallback.Path
          Sort =
            { Key = token SortSetting |> Option.bind sortOf |> Option.defaultValue fallback.Sort.Key
              Direction =
                token DirectionSetting
                |> Option.bind directionOf
                |> Option.defaultValue fallback.Sort.Direction }
          Hidden = token HiddenSetting |> Option.bind hiddenOf |> Option.defaultValue fallback.Hidden }

    let parse (defaults: Settings) (lines: string list) : Settings =
        let read = entries lines

        { Left = panelFrom read LeftPrefix defaults.Left
          Right = panelFrom read RightPrefix defaults.Right
          LastCreatedDirectory =
            Map.tryFind (DirectoryPrefix + NameSetting) read
            |> Option.bind EntryName.tryCreate
            |> Option.orElse defaults.LastCreatedDirectory }

    let private stored (fs: IFileSystem) defaults path =
        try
            let file = FileSystemPath.value path

            if fs.File.Exists file then
                fs.File.ReadAllLines file |> List.ofArray |> parse defaults
            else
                defaults
        with _ ->
            defaults

    let private reachable (fs: IFileSystem) (fallback: PanelSettings) (panel: PanelSettings) =
        match FileSystemPath.kind fs panel.Path with
        | DirectoryEntry -> panel
        | _ -> { panel with Path = fallback.Path }

    let load (fs: IFileSystem) (defaults: Settings) (path: FileSystemPath) : Settings =
        let settings = stored fs defaults path

        { settings with
            Left = reachable fs defaults.Left settings.Left
            Right = reachable fs defaults.Right settings.Right }

    let save (fs: IFileSystem) (path: FileSystemPath) (settings: Settings) =
        try
            fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs path))
            |> ignore

            fs.File.WriteAllLines(FileSystemPath.value path, format settings)
        with _ ->
            ()
