namespace Marbas.FileSystem

type Handler =
    { Name: string
      Program: ProgramPath }

module Handler =

    let fileName (text: string) = text.Split([| '\\'; '/' |]) |> Array.last

    let private named (raw: string) (program: ProgramPath) =
        match raw.Trim() with
        | "" -> ProgramPath.value program |> fileName
        | text -> text

    let tryCreate raw program =
        program |> Option.map (fun path -> { Name = named raw path; Program = path })

    let distinct handlers =
        handlers
        |> List.fold
            (fun kept handler ->
                if kept |> List.exists (fun known -> known.Program = handler.Program) then
                    kept
                else
                    handler :: kept)
            []
        |> List.rev
