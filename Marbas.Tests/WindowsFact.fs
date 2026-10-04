namespace Marbas.Tests

open System
open Xunit

type WindowsFactAttribute(reason: string) as this =
    inherit FactAttribute()

    do
        if not (OperatingSystem.IsWindows()) then
            this.Skip <- reason

    new() = WindowsFactAttribute "Drive letters are a Windows concept"

type WindowsTheoryAttribute(reason: string) as this =
    inherit TheoryAttribute()

    do
        if not (OperatingSystem.IsWindows()) then
            this.Skip <- reason
