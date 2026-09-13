namespace Marbas.Tests

open System
open System.IO
open Xunit

module private SymbolicLinks =

    let allowed =
        lazy
            (let probe = Path.Combine(Path.GetTempPath(), $"marbas-link-probe-{Guid.NewGuid():N}")

             try
                 try
                     Directory.CreateSymbolicLink(probe, Path.GetTempPath()) |> ignore
                     true
                 with _ ->
                     false
             finally
                 if Directory.Exists probe then
                     Directory.Delete probe)

type SymbolicLinkFactAttribute() as this =
    inherit FactAttribute()

    do
        if not SymbolicLinks.allowed.Value then
            this.Skip <- "This system refuses to create a symbolic link for this user"
