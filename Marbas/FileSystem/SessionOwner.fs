namespace Marbas.FileSystem

open System
open System.Globalization
open FsToolkit.ErrorHandling

type SessionOwner =
    { ProcessId: int
      Started: DateTimeOffset }

module SessionOwner =

    [<Literal>]
    let private MomentFormat = "o"

    let lines (owner: SessionOwner) =
        [ owner.ProcessId.ToString CultureInfo.InvariantCulture
          owner.Started.ToString(MomentFormat, CultureInfo.InvariantCulture) ]

    let private processIdOf (text: string) =
        match Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture) with
        | true, processId -> Some processId
        | false, _ -> None

    let private momentOf (text: string) =
        match
            DateTimeOffset.TryParseExact(text, MomentFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        with
        | true, moment -> Some moment
        | false, _ -> None

    let tryParse (lines: string list) =
        match lines with
        | [ processId; started ] ->
            option {
                let! processId = processIdOf processId
                let! started = momentOf started

                return
                    { ProcessId = processId
                      Started = started }
            }
        | _ -> None
