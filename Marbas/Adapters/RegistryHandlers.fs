module Marbas.Adapters.RegistryHandlers

open System
open System.Diagnostics
open Microsoft.Win32
open Marbas.Domain
open Marbas.FileSystem

[<Literal>]
let private OpenCommand = @"shell\open\command"

[<Literal>]
let private OpenWithProgids = "OpenWithProgids"

[<Literal>]
let private OpenWithList = "OpenWithList"

[<Literal>]
let private UserChoice = "UserChoice"

[<Literal>]
let private ProgIdValue = "ProgId"

[<Literal>]
let private MostRecentlyUsed = "MRUList"

[<Literal>]
let private FileExtensions = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts"

[<Literal>]
let private Applications = "Applications"

[<Literal>]
let private FriendlyName = "FriendlyAppName"

[<Literal>]
let private IndirectString = '@'

let private opened (root: RegistryKey) (path: string) =
    try
        root.OpenSubKey path |> Option.ofObj
    with _ ->
        None

let private textOf (key: RegistryKey) (name: string) =
    match key.GetValue name with
    | :? string as text when text.Trim() <> "" -> Some text
    | _ -> None

let private reading root path pick =
    opened root path
    |> Option.map (fun key ->
        use key = key
        pick key)

let private valueIn root path name =
    reading root path (fun key -> textOf key name) |> Option.flatten

let private namesIn root path =
    reading root path (fun key -> key.GetValueNames() |> List.ofArray)
    |> Option.defaultValue []

let private valuesIn root path =
    reading root path (fun key ->
        key.GetValueNames()
        |> List.ofArray
        |> List.filter (fun name -> name <> MostRecentlyUsed)
        |> List.choose (textOf key))
    |> Option.defaultValue []

let private commandUnder root path =
    valueIn root path null
    |> Option.map Environment.ExpandEnvironmentVariables
    |> Option.bind ProgramCommand.programOf

let private ofProgId (progId: string) =
    commandUnder Registry.ClassesRoot $@"{progId}\{OpenCommand}"

let private ofApplication (application: string) =
    commandUnder Registry.ClassesRoot $@"{Applications}\{application}\{OpenCommand}"

let private described (program: ProgramPath) =
    try
        match FileVersionInfo.GetVersionInfo(ProgramPath.value program).FileDescription with
        | null -> ""
        | text -> text
    with _ ->
        ""

let private friendly (program: ProgramPath) =
    let application = ProgramPath.value program |> Handler.fileName

    valueIn Registry.ClassesRoot $@"{Applications}\{application}" FriendlyName
    |> Option.filter (fun text -> not (text.StartsWith IndirectString))
    |> Option.defaultWith (fun () -> described program)

let private progIds (extension: string) =
    let classes = $".{extension}"
    let user = $@"{FileExtensions}\{classes}"

    (valueIn Registry.CurrentUser $@"{user}\{UserChoice}" ProgIdValue |> Option.toList)
    @ (valueIn Registry.ClassesRoot classes null |> Option.toList)
    @ namesIn Registry.ClassesRoot $@"{classes}\{OpenWithProgids}"
    @ namesIn Registry.CurrentUser $@"{user}\{OpenWithProgids}"
    |> List.filter (fun progId -> progId.Trim() <> "")
    |> List.distinct

let private applications (extension: string) =
    valuesIn Registry.CurrentUser $@"{FileExtensions}\.{extension}\{OpenWithList}"

let read (name: EntryName) =
    match FileExtension.ofName name with
    | None -> []
    | Some extension ->
        try
            let spelled = FileExtension.value extension

            (progIds spelled |> List.map ofProgId)
            @ (applications spelled |> List.map ofApplication)
            |> List.choose id
            |> List.choose (fun program -> Handler.tryCreate (friendly program) (Some program))
            |> Handler.distinct
        with _ ->
            []
