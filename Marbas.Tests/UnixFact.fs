namespace Marbas.Tests

open System
open Xunit

type UnixFactAttribute() as this =
    inherit FactAttribute()

    do
        if OperatingSystem.IsWindows() then
            this.Skip <- "A path root without a drive letter is a Unix concept"
