namespace Marbas.FileSystem

open Marbas.FileSystem.FileSearch

type FindLanding =
    | AtHit of hit: FindHit
    | AtDirectory of location: PanelLocation

type FindAnswer =
    { Landing: FindLanding option
      Hits: FindHit list
      Cursor: int }
