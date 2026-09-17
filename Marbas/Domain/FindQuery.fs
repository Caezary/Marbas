namespace Marbas.Domain

open System

type NameMasks = private NameMasks of NamePattern list

module NameMasks =

    [<Literal>]
    let private Separator = ';'

    let private everything = [ "*"; "*.*" ]

    let private wildcards = [| '*'; '?' |]

    let private expand (piece: string) =
        if List.contains piece everything then "*"
        elif piece.IndexOfAny wildcards >= 0 then piece
        else $"*%s{piece}*"

    let read (raw: string) =
        raw.Split Separator
        |> Array.toList
        |> List.map _.Trim()
        |> List.filter (String.IsNullOrEmpty >> not)
        |> List.map expand
        |> List.choose NamePattern.tryCreate
        |> NameMasks

    let matches (NameMasks patterns) name =
        List.isEmpty patterns || patterns |> List.exists (fun pattern -> NamePattern.matches pattern name)

type FindText = private FindText of string

module FindText =

    let tryCreate (raw: string) =
        if String.IsNullOrEmpty raw then None else Some(FindText raw)

    let value (FindText raw) = raw

type LetterCase =
    | IgnoringCase
    | MatchingCase

type WordMatch =
    | Anywhere
    | WholeWords

type TextSense =
    | Containing
    | NotContaining

type BinaryFiles =
    | Skipped
    | Searched

type TextSearch =
    { Text: FindText
      Case: LetterCase
      Words: WordMatch
      Sense: TextSense
      Binary: BinaryFiles }

type ContentAnswer =
    | TextMatched
    | TextDidNotMatch
    | FileSkipped
    | FileUnreadable
    | SearchStopped

type FindTargets =
    | FilesOnly
    | FilesAndDirectories

type FindWhat =
    | NameOnly of NameMasks * FindTargets
    | NameAndText of NameMasks * TextSearch

module FindWhat =

    let create masks text targets =
        match text with
        | Some search -> NameAndText(masks, search)
        | None -> NameOnly(masks, targets)

    let masks what =
        match what with
        | NameOnly(masks, _) -> masks
        | NameAndText(masks, _) -> masks

    let targets what =
        match what with
        | NameOnly(_, targets) -> targets
        | NameAndText _ -> FilesOnly

    let text what =
        match what with
        | NameOnly _ -> None
        | NameAndText(_, search) -> Some search

type FindScope =
    | FromDirectory
    | DirectoryOnly
    | MarkedDirectories
    | FromVolumeRoot
    | AllDrives
    | PathDirectories

type MarkedGround =
    | NoDirectoryMarked
    | DirectoriesMarked

type LinkWalk =
    | StopAtLinks
    | FollowLinks

type ArchiveSearch =
    | SkipArchives
    | SearchArchives

type FindQuery =
    { What: FindWhat
      Scope: FindScope
      Hidden: HiddenEntries
      Links: LinkWalk
      Archives: ArchiveSearch }

module FindQuery =

    let masks query = FindWhat.masks query.What

    let targets query = FindWhat.targets query.What

    let text query = FindWhat.text query.What

    let matchesName query name =
        NameMasks.matches (masks query) name
