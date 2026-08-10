module Marbas.Tests.Expect

let ok result =
    match result with
    | Ok value -> value
    | Error err -> failwith $"expected Ok, got Error %A{err}"

let error result =
    match result with
    | Ok value -> failwith $"expected Error, got Ok %A{value}"
    | Error err -> err
