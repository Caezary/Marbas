namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas.Collections
open Marbas.Domain

type PanelSettings =
    { Path: FileSystemPath
      Sort: SortOrder
      Hidden: HiddenEntries
      Cursor: EntryName option }

type TabSettings =
    { Left: PanelSettings
      Right: PanelSettings
      Active: PanelSide }

type Settings =
    { Tabs: Zipper<TabSettings>
      Programs: ProgramSettings }

    member private this.Focused = Zipper.current this.Tabs

    member this.Left = this.Focused.Left

    member this.Right = this.Focused.Right

    member this.Active = this.Focused.Active

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
    let private TabPrefix = "tab."

    [<Literal>]
    let private CurrentSetting = "tabs.current"

    [<Literal>]
    let private ActiveSetting = "active"

    [<Literal>]
    let private PathSetting = "path"

    [<Literal>]
    let private SortSetting = "sort"

    [<Literal>]
    let private DirectionSetting = "direction"

    [<Literal>]
    let private HiddenSetting = "hidden"

    [<Literal>]
    let private CursorSetting = "cursor"

    [<Literal>]
    let private EditorPrefix = "editor."

    [<Literal>]
    let private DefaultSetting = "default"

    [<Literal>]
    let private ProgramPrefix = "program."

    [<Literal>]
    let private RolelessPrefix = "editor.extension."

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

    let private roleText role =
        match role with
        | ForEditing -> "editing"
        | ForOpening -> "opening"

    let private roleOf text =
        match text with
        | "editing" -> Some ForEditing
        | "opening" -> Some ForOpening
        | _ -> None

    let private sideText side =
        match side with
        | Left -> "left"
        | Right -> "right"

    let private sideOf text =
        match text with
        | "left" -> Some Left
        | "right" -> Some Right
        | _ -> None

    let private defaultPanelAt path =
        { Path = path
          Sort = SortOrder.byName
          Hidden = Concealed
          Cursor = None }

    let forPath path : Settings =
        { Tabs =
            Zipper.single
                { Left = defaultPanelAt path
                  Right = defaultPanelAt path
                  Active = Left }
          Programs = ProgramSettings.none }

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
        @ (panel.Cursor
           |> Option.map (EntryName.value >> line prefix CursorSetting)
           |> Option.toList)

    let private roleSetting (association: ProgramAssociation) =
        $"{roleText association.Role}.{FileExtension.value association.Extension}"

    let private programLines (programs: ProgramSettings) =
        (programs.DefaultEditor
         |> Option.map (ProgramPath.value >> line EditorPrefix DefaultSetting)
         |> Option.toList)
        @ (programs.Associations
           |> List.map (fun association ->
               line ProgramPrefix (roleSetting association) (ProgramPath.value association.Program)))

    let private tabPrefix number = $"{TabPrefix}{number}."

    let private tabLines number (tab: TabSettings) =
        let prefix = tabPrefix number

        panelLines (prefix + LeftPrefix) tab.Left
        @ panelLines (prefix + RightPrefix) tab.Right
        @ [ line prefix ActiveSetting (sideText tab.Active) ]

    let format (settings: Settings) =
        (settings.Tabs
         |> Zipper.items
         |> List.mapi (fun index -> tabLines (index + 1))
         |> List.concat)
        @ [ $"{CurrentSetting}={Zipper.position settings.Tabs + 1}" ]
        @ programLines settings.Programs

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

        let token setting =
            lookup setting |> Option.map _.ToLowerInvariant()

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
          Hidden =
            token HiddenSetting
            |> Option.bind hiddenOf
            |> Option.defaultValue fallback.Hidden
          Cursor = lookup CursorSetting |> Option.bind EntryName.tryCreate }

    let private parsedNumber (raw: string) =
        match Int32.TryParse raw with
        | true, number -> Some number
        | _ -> None

    let private tabNumberInKey (key: string) =
        (key.Substring TabPrefix.Length).Split('.')[0] |> parsedNumber

    let private tabNumbers (read: Map<string, string>) =
        read
        |> Map.toList
        |> List.map fst
        |> List.filter _.StartsWith(TabPrefix)
        |> List.choose tabNumberInKey
        |> List.distinct
        |> List.sort

    let private tabFrom (read: Map<string, string>) prefix (fallback: TabSettings) =
        { Left = panelFrom read (prefix + LeftPrefix) fallback.Left
          Right = panelFrom read (prefix + RightPrefix) fallback.Right
          Active =
            Map.tryFind (prefix + ActiveSetting) read
            |> Option.map _.ToLowerInvariant()
            |> Option.bind sideOf
            |> Option.defaultValue fallback.Active }

    let private tabsFrom (read: Map<string, string>) (fallback: TabSettings) =
        let numbered =
            tabNumbers read
            |> List.map (fun number -> tabFrom read (tabPrefix number) fallback)

        let tabs =
            match numbered with
            | head :: rest -> Zipper.ofNonEmpty head rest
            | [] -> Zipper.single (tabFrom read "" fallback)

        let current =
            Map.tryFind CurrentSetting read
            |> Option.bind parsedNumber
            |> Option.defaultValue 1

        tabs |> Zipper.refocus (current - 1)

    let private validAssociation role extension (value: string) =
        match FileExtension.tryCreate extension, ProgramPath.tryCreate value with
        | Some extension, Some program ->
            Some
                { Extension = extension
                  Role = role
                  Program = program }
        | _ -> None

    let private associationFrom (key: string) (value: string) =
        if key.StartsWith ProgramPrefix then
            match key.Substring(ProgramPrefix.Length).Split('.', 2) with
            | [| role; extension |] -> roleOf role |> Option.bind (fun role -> validAssociation role extension value)
            | _ -> None
        elif key.StartsWith RolelessPrefix then
            validAssociation ForEditing (key.Substring RolelessPrefix.Length) value
        else
            None

    let private programsFrom (read: Map<string, string>) (fallback: ProgramSettings) =
        let named =
            { ProgramSettings.none with
                DefaultEditor =
                    Map.tryFind (EditorPrefix + DefaultSetting) read
                    |> Option.bind ProgramPath.tryCreate }

        let programs =
            read
            |> Map.toList
            |> List.choose (fun (key, value) -> associationFrom key value)
            |> List.fold (fun settings association -> ProgramSettings.remember association settings) named

        if programs = ProgramSettings.none then
            fallback
        else
            programs

    let parse (defaults: Settings) (lines: string list) : Settings =
        let read = entries lines

        { Tabs = defaults.Tabs |> Zipper.current |> tabsFrom read
          Programs = programsFrom read defaults.Programs }

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
        let fallback = Zipper.current defaults.Tabs

        { settings with
            Tabs =
                settings.Tabs
                |> Zipper.map (fun tab ->
                    { tab with
                        Left = reachable fs fallback.Left tab.Left
                        Right = reachable fs fallback.Right tab.Right }) }

    let save (fs: IFileSystem) (path: FileSystemPath) (settings: Settings) =
        try
            fs.Directory.CreateDirectory(FileSystemPath.value (FileSystemPath.directory fs path))
            |> ignore

            fs.File.WriteAllLines(FileSystemPath.value path, format settings)
            Ok()
        with ex ->
            Error(SettingsError.mapException path ex)
