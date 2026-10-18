namespace Marbas.Find

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Find.FileSearch

type FindLanding =
    | AtHit of hit: FindHit
    | At of landing: Landing
    | InPanel

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

    let surviving (fs: IFileSystem) (results: FindResults) =
        let kept = results.Hits |> List.filter (FindHit.exists fs)

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
