module Marbas.Tests.State.PortsTests

open System
open System.Reflection
open Microsoft.FSharp.Reflection
open Xunit
open Marbas.State
open Marbas.Tests

let rec private arguments (shape: Type) =
    if FSharpType.IsFunction shape then
        let argument, answer = FSharpType.GetFunctionElements shape
        argument :: arguments answer
    else
        []

let private takesFunction (field: PropertyInfo) =
    arguments field.PropertyType |> List.exists FSharpType.IsFunction

let private fieldsOf (shape: Type) =
    FSharpType.GetRecordFields shape |> List.ofArray

let private portRecords =
    fieldsOf typeof<Ports.Dependencies>
    |> List.map _.PropertyType
    |> List.filter FSharpType.IsRecord

let private named (field: PropertyInfo) =
    $"{(Expect.notNull field.DeclaringType).Name}.{field.Name}"

let private takingFunctions (records: Type list) =
    records |> List.collect fieldsOf |> List.filter takesFunction |> List.map named

[<Fact>]
let ``the dependencies hold the five records of ports`` () =
    let records = portRecords |> List.map _.Name

    Assert.Equal<string list>([ "Prompts"; "Processes"; "Clipboard"; "Watches"; "Runners" ], records)

[<Fact>]
let ``no port takes a function as an argument`` () =
    let offenders = takingFunctions (typeof<Ports.Dependencies> :: portRecords)

    Assert.Empty offenders
