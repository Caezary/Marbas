module Marbas.FileSystem.DesktopEntry

[<Literal>]
let Group = "[Desktop Entry]"

[<Literal>]
let Extension = "desktop"

[<Literal>]
let private NameKey = "Name"

[<Literal>]
let private ExecKey = "Exec"

[<Literal>]
let private TypeKey = "Type"

[<Literal>]
let private Application = "Application"

let private hiddenKeys = [ "NoDisplay"; "Hidden" ]

let private inGroup (lines: string list) =
    match lines |> List.map _.Trim() |> List.skipWhile (fun line -> line <> Group) with
    | [] -> []
    | _ :: rest -> rest |> List.takeWhile (fun line -> not (line.StartsWith '['))

let private valueOf key lines =
    lines
    |> List.tryPick (fun (line: string) ->
        match line.Split('=', 2) with
        | [| named; value |] when named.Trim() = key -> Some(value.Trim())
        | _ -> None)

let private concealed lines =
    hiddenKeys
    |> List.exists (fun key -> valueOf key lines = Some "true")

let private ofKind lines =
    match valueOf TypeKey lines with
    | Some kind -> kind = Application
    | None -> true

let parse (lines: string list) =
    let entry = inGroup lines

    if concealed entry || not (ofKind entry) then
        None
    else
        valueOf ExecKey entry
        |> Option.bind ProgramCommand.programOf
        |> Handler.tryCreate (valueOf NameKey entry |> Option.defaultValue "")
