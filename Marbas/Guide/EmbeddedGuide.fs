module Marbas.Guide.EmbeddedGuide

open System
open System.IO

let private prefix = "user-guide/"

let private assembly = typeof<Chapter>.Assembly

let names () =
    assembly.GetManifestResourceNames()
    |> Array.filter _.StartsWith(prefix, StringComparison.Ordinal)
    |> Array.choose (fun resource -> ChapterName.tryCreate (resource.Substring prefix.Length))
    |> Array.sort
    |> List.ofArray

let private parsed name (stream: Stream) =
    use reader = new StreamReader(stream)

    ChapterMarkdown.parse (reader.ReadToEnd())
    |> Result.map (fun blocks -> { Name = name; Contents = blocks })
    |> Result.mapError (fun refusal -> Refused(name, refusal))

let read name =
    match assembly.GetManifestResourceStream(prefix + ChapterName.value name) with
    | null -> Error(Absent name)
    | stream -> parsed name stream
