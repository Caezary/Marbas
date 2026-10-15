module Marbas.Tests.Expect

let ok result =
    match result with
    | Ok value -> value
    | Error err -> failwith $"expected Ok, got Error %A{err}"

let error result =
    match result with
    | Ok value -> failwith $"expected Error, got Ok %A{value}"
    | Error err -> err

let some option =
    match option with
    | Some value -> value
    | None -> failwith "expected Some, got None"

let notNull (value: 'value | null) =
    match value with
    | null -> failwith "expected a value, got null"
    | value -> value
