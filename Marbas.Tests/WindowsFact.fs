namespace Marbas.Tests

open System
open Xunit

type WindowsFactAttribute() as this =
    inherit FactAttribute()

    do
        if not (OperatingSystem.IsWindows()) then
            this.Skip <- "Drive letters are a Windows concept"
