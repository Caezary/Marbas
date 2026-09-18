namespace Marbas.FileSystem

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem.FileSearch

type FindLanding =
    | AtHit of hit: FindHit
    | AtDirectory of location: PanelLocation

type FindResults =
    { Query: FindQuery
      Ground: FindGround
      Hits: FindHit list
      Outcome: FindOutcome
      Cursor: int }

type FindAnswer =
    { Landing: FindLanding option
      Results: FindResults }

type SearchRun =
    | Walk of query: FindQuery * ground: FindGround
    | Reopen of results: FindResults * gone: int

module FindResults =

    let private stands (fs: IFileSystem) (path: FileSystemPath) =
        let spelled = FileSystemPath.value path

        fs.File.Exists spelled || fs.Directory.Exists spelled

    let private survives (fs: IFileSystem) (hit: FindHit) =
        match hit.Location with
        | OnDisk _ -> stands fs (FindHit.spelled fs hit)
        | InArchive(archive, _) -> stands fs archive

    let surviving (fs: IFileSystem) (results: FindResults) =
        let kept = results.Hits |> List.filter (survives fs)

        { results with Hits = kept }, List.length results.Hits - List.length kept

module SearchRun =

    let query run =
        match run with
        | Walk(query, _) -> query
        | Reopen(results, _) -> results.Query

    let ground run =
        match run with
        | Walk(_, ground) -> ground
        | Reopen(results, _) -> results.Ground
