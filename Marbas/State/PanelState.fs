namespace Marbas.State

open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.FileSystem.DirectoryListing

type PanelState =
    { Path: PanelLocation
      Entries: Entry array
      Cursor: int
      Remembered: EntryName option
      Selected: Set<EntryName>
      MarksBefore: Set<EntryName> option
      Sort: SortOrder
      Hidden: HiddenEntries
      Search: SearchText option }

type FocusTarget =
    | Top
    | ByName of EntryName

module PanelState =

    let cursorEntry (p: PanelState) = p.Entries |> Array.tryItem p.Cursor

    let selectable entry = entry.Name <> ParentName

    let standingName (p: PanelState) =
        match p.Path with
        | SearchResults found -> found.Returning
        | _ ->
            cursorEntry p
            |> Option.filter selectable
            |> Option.map _.Name
            |> Option.orElse p.Remembered

    let rememberedFocus (p: PanelState) =
        match p.Remembered with
        | Some name -> ByName name
        | None -> Top

    let cursorFocus (p: PanelState) =
        match cursorEntry p with
        | Some entry -> ByName entry.Name
        | None -> rememberedFocus p

    let withCursor index (p: PanelState) = { p with Cursor = index }

    let private withCursorClamped index (p: PanelState) =
        { p with
            Cursor = index |> min (p.Entries.Length - 1) |> max 0 }

    let withoutSearch (p: PanelState) = { p with Search = None }

    let cleared (p: PanelState) =
        { p with
            Selected = Set.empty
            Search = None }

    let private focusFirstMatch (p: PanelState) =
        match p.Search with
        | None -> p
        | Some text ->
            match p.Entries |> Array.tryFindIndex (fun e -> SearchText.matches text e.Name) with
            | Some index -> withCursor index p
            | None -> p

    let private focusingSearch text (p: PanelState) =
        focusFirstMatch { p with Search = text }

    let typeIntoSearch character (p: PanelState) =
        match p.Search with
        | None -> focusingSearch (Some(SearchText.ofChar character)) p
        | Some text -> focusingSearch (Some(SearchText.append character text)) p

    let backspaceSearch (p: PanelState) =
        match p.Search with
        | None -> p
        | Some text -> focusingSearch (SearchText.backspace text) p

    let private reorder (p: PanelState) =
        let entries = DirectoryListing.order p.Sort p.Entries

        let cursor =
            cursorEntry p
            |> Option.bind (fun entry -> entries |> Array.tryFindIndex (fun e -> e.Name = entry.Name))
            |> Option.defaultValue 0

        { p with
            Entries = entries
            Cursor = cursor }

    let sortBy (key: SortKey) (p: PanelState) =
        reorder
            { p with
                Sort = SortOrder.switchTo key p.Sort }

    let withMeasurements (measured: Measurement list) (p: PanelState) =
        let sizes = measured |> List.map (fun one -> one.Name, one.Size) |> Map.ofList

        let measuredEntry entry =
            match Map.tryFind entry.Name sizes with
            | Some size -> { entry with Measured = Some size }
            | None -> entry

        reorder
            { p with
                Entries = p.Entries |> Array.map measuredEntry }

    let withHiddenToggled (p: PanelState) =
        { p with
            Hidden = HiddenEntries.toggle p.Hidden }

    let selectedEntries (p: PanelState) =
        p.Entries |> Array.filter (fun e -> p.Selected.Contains e.Name) |> List.ofArray

    let private markedAtCursor (p: PanelState) =
        match cursorEntry p with
        | Some entry -> p.Selected.Contains entry.Name
        | None -> false

    let private runShortOf cursor target =
        if cursor < target then [ cursor .. target - 1 ]
        elif cursor > target then [ target + 1 .. cursor ]
        else [ cursor ]

    let private namesOf indexes (p: PanelState) =
        indexes
        |> List.choose (fun index -> Array.tryItem index p.Entries)
        |> List.filter selectable
        |> List.map _.Name
        |> Set.ofList

    let private remembering mark (p: PanelState) =
        let marked = mark p

        if marked.Selected = p.Selected then
            marked
        else
            { marked with
                MarksBefore = Some p.Selected }

    let private toggledShortOf target (p: PanelState) =
        let run = namesOf (runShortOf p.Cursor target) p

        let selected =
            if markedAtCursor p then
                Set.difference p.Selected run
            else
                Set.union p.Selected run

        withCursorClamped target { p with Selected = selected }

    let toggleSelectionShortOf target (p: PanelState) = remembering (toggledShortOf target) p

    let toggleSelection (p: PanelState) = toggleSelectionShortOf p.Cursor p

    let private step direction =
        match direction with
        | Upward -> -1
        | Downward -> 1

    let toggleSelectionMoving direction (p: PanelState) =
        toggleSelectionShortOf (p.Cursor + step direction) p

    let private matching pattern (p: PanelState) =
        p.Entries
        |> Array.filter (fun e -> selectable e && NamePattern.matches pattern e.Name)
        |> Array.map _.Name
        |> Set.ofArray

    let private applyNames mode names (p: PanelState) =
        match mode with
        | Select ->
            { p with
                Selected = Set.union p.Selected names }
        | Deselect ->
            { p with
                Selected = Set.difference p.Selected names }

    let applyPattern mode pattern (p: PanelState) =
        remembering (applyNames mode (matching pattern p)) p

    let private alikeTo likeness (anchor: Entry) (p: PanelState) =
        p.Entries
        |> Array.filter (fun e -> selectable e && Likeness.alike likeness anchor.Name e.Name)
        |> Array.map _.Name
        |> Set.ofArray

    let applyLikeness mode likeness (p: PanelState) =
        match cursorEntry p |> Option.filter selectable with
        | Some anchor -> remembering (applyNames mode (alikeTo likeness anchor p)) p
        | None -> p

    let private selectableNames keep (p: PanelState) =
        p.Entries
        |> Array.filter (fun e -> selectable e && keep e)
        |> Array.map _.Name
        |> Set.ofArray

    let applyAllFiles mode (p: PanelState) =
        remembering (applyNames mode (selectableNames (fun e -> e.Kind = File) p)) p

    let private inverted (p: PanelState) =
        { p with
            Selected = Set.difference (selectableNames (fun _ -> true) p) p.Selected }

    let invertSelection (p: PanelState) = remembering inverted p

    let keepSurviving selected (p: PanelState) =
        let listed = p.Entries |> Array.map _.Name |> Set.ofArray

        { p with
            Selected = Set.intersect listed selected }

    let keepSurvivingMarks (previous: PanelState) (p: PanelState) =
        { keepSurviving previous.Selected p with
            MarksBefore = previous.MarksBefore }

    let recallMarks (p: PanelState) =
        match p.MarksBefore with
        | Some before ->
            { keepSurviving before p with
                MarksBefore = Some p.Selected }
        | None -> p

    let forgetMarks (p: PanelState) = { p with MarksBefore = None }

    let operationTargets (p: PanelState) =
        match selectedEntries p with
        | [] -> cursorEntry p |> Option.filter selectable |> Option.toList
        | marked -> marked

    let hitAt (p: PanelState) (entry: Entry) =
        match p.Path with
        | SearchResults found -> SearchListing.hitFor found entry.Name
        | _ -> None

    let private ownName (p: PanelState) (entry: Entry) =
        hitAt p entry |> Option.map _.Entry.Name |> Option.defaultValue entry.Name

    let operationNames (p: PanelState) =
        operationTargets p |> List.map (ownName p)

    let private entryInArchive (p: PanelState) (entry: Entry) =
        match p.Path with
        | SearchResults _ -> hitAt p entry |> Option.bind FindHit.archivePlace
        | location -> PanelLocation.archivePlace location entry.Name

    let cursorEntryInArchive (p: PanelState) =
        cursorEntry p |> Option.filter selectable |> Option.bind (entryInArchive p)

    let cursorFileInArchive (p: PanelState) =
        cursorEntry p
        |> Option.filter (fun entry -> entry.Kind = File)
        |> Option.bind (entryInArchive p)

    let targetsDirectory (p: PanelState) =
        operationTargets p |> List.exists (fun entry -> entry.Kind = Directory)

    let targetsDirectoryInArchive (p: PanelState) =
        let directoryInArchive (entry: Entry) =
            entry.Kind = Directory && entryInArchive p entry |> Option.isSome

        operationTargets p |> List.exists directoryInArchive

    let showsResults (p: PanelState) =
        match p.Path with
        | SearchResults _ -> true
        | _ -> false

    let markedGround (p: PanelState) =
        if selectedEntries p |> List.exists (fun entry -> entry.Kind = Directory) then
            DirectoriesMarked
        else
            NoDirectoryMarked

    let directory (p: PanelState) = PanelLocation.directory p.Path
