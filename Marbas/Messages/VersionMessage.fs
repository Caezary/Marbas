module Marbas.Messages.VersionMessage

open Marbas.Domain

let product version =
    $"Marbas {ProductVersion.value version}"
