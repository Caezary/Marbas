namespace Marbas.Tests

open System
open Xunit

type UnixFactAttribute(reason: string) as this =
    inherit FactAttribute()

    do
        if OperatingSystem.IsWindows() then
            this.Skip <- reason

    new() = UnixFactAttribute "A path root without a drive letter is a Unix concept"

type UnixTheoryAttribute(reason: string) as this =
    inherit TheoryAttribute()

    do
        if OperatingSystem.IsWindows() then
            this.Skip <- reason
