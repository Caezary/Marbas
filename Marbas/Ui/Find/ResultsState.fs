namespace Marbas.Ui.Find

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Locations
open Marbas.Find
open Marbas.Find.FileSearch
open Marbas.Ui.Format

type StopRequest =
    | NotAsked
    | StopAsked

type Leave =
    | StopsWalk
    | ClosesWindow

type ResultsState =
    private
        { Run: SearchRun
          HitsNewestFirst: FindHit list
          RowsNewestFirst: ResultRow.ResultRow list
          Counts: FindCounts
          Directory: string
          Ended: FindOutcome option
          Stop: StopRequest
          Landing: FindLanding option
          Room: int }

module ResultsState =

    let private nothing =
        { Files = 0
          Directories = 0
          Skipped = 0 }

    let private countsOf (hits: FindHit list) =
        let kind wanted =
            hits |> List.filter (fun hit -> hit.Entry.Kind = wanted) |> List.length

        { Files = kind DirectoryListing.File
          Directories = kind DirectoryListing.Directory
          Skipped = 0 }

    let private nothingFound run =
        { Run = run
          HitsNewestFirst = []
          RowsNewestFirst = []
          Counts = nothing
          Directory = ""
          Ended = None
          Stop = NotAsked
          Landing = None
          Room = 0 }

    let start (run: SearchRun) =
        match run with
        | Walk _ -> nothingFound run
        | Reopen(results, _) ->
            { nothingFound run with
                HitsNewestFirst = List.rev results.Hits
                RowsNewestFirst = ResultRow.following None results.Hits |> List.rev
                Counts = countsOf results.Hits
                Ended = Some results.Outcome }

    let hits (state: ResultsState) = List.rev state.HitsNewestFirst

    let rows (state: ResultsState) = List.rev state.RowsNewestFirst

    let rowAt index (state: ResultsState) = rows state |> List.tryItem index

    let running (state: ResultsState) = Option.isNone state.Ended

    let room (state: ResultsState) = state.Room

    let private lastLocation (state: ResultsState) =
        state.HitsNewestFirst |> List.tryHead |> Option.map _.Location

    let arrived (reports: FindReport list) (state: ResultsState) =
        let found = reports |> List.map _.Hit
        let added = ResultRow.following (lastLocation state) found

        added,
        { state with
            HitsNewestFirst = List.rev found @ state.HitsNewestFirst
            RowsNewestFirst = List.rev added @ state.RowsNewestFirst
            Counts =
                reports
                |> List.tryLast
                |> Option.map _.Counts
                |> Option.defaultValue state.Counts }

    let walking (directory: string) (state: ResultsState) = { state with Directory = directory }

    let ended (outcome: FindOutcome) (state: ResultsState) = { state with Ended = Some outcome }

    let laidOut room (state: ResultsState) = { state with Room = room }

    let leaving (state: ResultsState) =
        match state.Ended, state.Stop with
        | None, NotAsked -> StopsWalk, { state with Stop = StopAsked }
        | None, StopAsked
        | Some _, _ -> ClosesWindow, state

    let viewableAt index (state: ResultsState) =
        match rowAt index state with
        | Some(ResultRow.Hit hit) when hit.Entry.Kind = DirectoryListing.File -> Some hit
        | _ -> None

    let private landingOn row =
        match row with
        | ResultRow.Hit hit -> AtHit hit
        | ResultRow.GroupHeader location -> At(Into location)

    let landingAt index (state: ResultsState) =
        rowAt index state |> Option.map landingOn

    let landed (landing: FindLanding) (state: ResultsState) = { state with Landing = Some landing }

    let private outcome (state: ResultsState) =
        state.Ended |> Option.defaultWith (fun () -> Stopped state.Counts)

    let progress (fs: IFileSystem) (state: ResultsState) =
        match state.Run, state.Ended with
        | Reopen(results, gone), _ ->
            FindSummary.Reopened(
                QueryText.describe results.Query,
                QueryText.where fs results.Ground,
                state.Counts,
                gone
            )
        | Walk _, Some outcome -> FindSummary.Done outcome
        | Walk _, None -> FindSummary.Walking(state.Directory, state.Counts)

    let lines (fs: IFileSystem) (state: ResultsState) =
        rows state |> List.map (ResultRow.describe fs state.Room)

    let answer cursor (state: ResultsState) : FindAnswer =
        { Landing = state.Landing
          Results =
            { Query = SearchRun.query state.Run
              Ground = SearchRun.ground state.Run
              Hits = hits state
              Outcome = outcome state
              Cursor = cursor } }
