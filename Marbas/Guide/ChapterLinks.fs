module Marbas.Guide.ChapterLinks

type BrokenLink = { Name: ChapterName; Target: Target }

let rec private phraseTargets (Phrase spans) = spans |> List.collect spanTargets

and private spanTargets span =
    match span with
    | Text _
    | Code _ -> []
    | Strong phrase
    | Emphasis phrase -> phraseTargets phrase
    | Link(phrase, target) -> target :: phraseTargets phrase

let rec private itemTargets item =
    phraseTargets item.Phrase @ List.collect itemTargets item.Items

let private proseTargets prose =
    match prose with
    | Paragraph phrase -> phraseTargets phrase
    | Bullets items -> List.collect itemTargets items

let private rowTargets (Row cells) =
    cells |> List.collect (fun (Cell phrase) -> phraseTargets phrase)

let private blockTargets block =
    match block with
    | Heading heading -> phraseTargets heading.Title
    | Prose prose -> proseTargets prose
    | Table table -> Table.header table :: Table.body table |> List.collect rowTargets
    | Alert(_, prose) -> List.collect proseTargets prose

let private anchorOf block =
    match block with
    | Heading heading -> Some heading.Anchor
    | _ -> None

let private anchorsByChapter (chapters: Chapter list) =
    chapters
    |> List.map (fun chapter -> chapter.Name, chapter.Contents |> List.choose anchorOf |> Set.ofList)
    |> Map.ofList

let private holds (anchors: Map<ChapterName, Set<Anchor>>) chapter anchor =
    anchors |> Map.tryFind chapter |> Option.exists (Set.contains anchor)

let private leadsSomewhere anchors chapter target =
    match target with
    | SameChapter anchor -> holds anchors chapter anchor
    | OtherChapter(other, None) -> Map.containsKey other anchors
    | OtherChapter(other, Some anchor) -> holds anchors other anchor

let private brokenIn anchors (chapter: Chapter) =
    chapter.Contents
    |> List.collect blockTargets
    |> List.filter (leadsSomewhere anchors chapter.Name >> not)
    |> List.map (fun target -> { Name = chapter.Name; Target = target })

let broken (chapters: Chapter list) =
    let anchors = anchorsByChapter chapters

    chapters |> List.collect (brokenIn anchors)
