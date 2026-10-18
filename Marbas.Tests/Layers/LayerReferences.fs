module Marbas.Tests.Layers.LayerReferences

open System
open System.IO
open Mono.Cecil

type private Layer =
    | Collections
    | Domain
    | FileSystem
    | Programs
    | Profile
    | Locations
    | Viewing
    | Find
    | Messages
    | State
    | Ui
    | Adapters
    | Program

let private mayUse =
    [ Collections, []
      Domain, [ Collections ]
      FileSystem, [ Domain ]
      Programs, [ FileSystem; Domain ]
      Profile, [ Programs; FileSystem; Domain; Collections ]
      Locations, [ FileSystem; Domain ]
      Viewing, [ FileSystem; Domain ]
      Find, [ Locations; Viewing; FileSystem; Domain ]
      Messages, [ Profile; Programs; FileSystem; Domain ]
      State,
      [ Messages
        Find
        Viewing
        Locations
        Profile
        Programs
        FileSystem
        Domain
        Collections ]
      Ui,
      [ State
        Messages
        Find
        Viewing
        Locations
        Profile
        Programs
        FileSystem
        Domain
        Collections ]
      Adapters,
      [ Ui
        State
        Messages
        Find
        Viewing
        Locations
        Profile
        Programs
        FileSystem
        Domain
        Collections ]
      Program, [ Adapters ] ]
    |> Map.ofList

let private layersByName =
    mayUse |> Map.keys |> Seq.map (fun layer -> string layer, layer) |> Map.ofSeq

type private Placed =
    { Name: string
      Layer: Result<Layer, string> }

type private Scanned = { Type: Placed; Uses: Placed list }

let private instrumentationNamespaces = [ "Coverlet" ]

let private startupCodePrefix = "<StartupCode$"

let rec private outermost (declaring: TypeReference) =
    match declaring.DeclaringType with
    | null -> declaring
    | outer -> outermost outer

let private isStartupCode (declaring: TypeReference) =
    (outermost declaring).FullName.StartsWith(startupCodePrefix, StringComparison.Ordinal)

let private isGenerated (declaring: TypeReference) =
    let outer = outermost declaring

    outer.FullName.StartsWith('<')
    || instrumentationNamespaces
       |> List.exists (fun name ->
           outer.Namespace = name
           || outer.Namespace.StartsWith(name + ".", StringComparison.Ordinal))

let private layerNamedBy (name: string) =
    match Map.tryFind name layersByName with
    | Some layer -> Ok layer
    | None -> Error name

let private layerOfNamespace (fullName: string) =
    match fullName.Split('.') |> List.ofArray with
    | "Marbas" :: name :: _ -> name
    | _ -> fullName

let private layerOfSource (url: string) =
    let segments = url.Replace('\\', '/').Split('/')
    let project = Array.findIndexBack ((=) "Marbas") segments

    Path.GetFileNameWithoutExtension segments[project + 1]

let private placed (declaring: TypeReference) =
    let name = (outermost declaring).FullName

    { Name = name
      Layer = layerOfNamespace name |> layerNamedBy }

let private sourceOf (declaring: TypeDefinition) =
    declaring.Methods
    |> Seq.collect _.DebugInformation.SequencePoints
    |> Seq.tryHead
    |> Option.map _.Document.Url

let private placedBySource (declaring: TypeDefinition) =
    sourceOf declaring
    |> Option.map (fun url ->
        { Name = declaring.FullName
          Layer = layerOfSource url |> layerNamedBy })

let rec private named (reference: TypeReference) : TypeReference seq =
    match reference with
    | :? GenericInstanceType as generic ->
        Seq.append (named generic.ElementType) (Seq.collect named generic.GenericArguments)
    | :? GenericParameter -> Seq.empty
    | :? TypeSpecification as specification -> named specification.ElementType
    | _ -> Seq.singleton reference

let private namedByMethod (callee: MethodReference) =
    seq {
        callee.DeclaringType
        callee.ReturnType
        yield! callee.Parameters |> Seq.map _.ParameterType

        match callee with
        | :? GenericInstanceMethod as generic -> yield! generic.GenericArguments
        | _ -> ()
    }

let private namedByInstruction (operand: obj) =
    match operand with
    | :? TypeReference as reference -> Seq.singleton reference
    | :? MethodReference as callee -> namedByMethod callee
    | :? FieldReference as field ->
        seq {
            field.DeclaringType
            field.FieldType
        }
    | _ -> Seq.empty

let private namedByBody (method: MethodDefinition) =
    match method.HasBody with
    | true ->
        Seq.append
            (method.Body.Variables |> Seq.map _.VariableType)
            (method.Body.Instructions
             |> Seq.collect (fun instruction -> namedByInstruction instruction.Operand))
    | false -> Seq.empty

let private namedByType (declaring: TypeDefinition) =
    seq {
        yield! Option.ofObj declaring.BaseType |> Option.toList
        yield! declaring.Interfaces |> Seq.map _.InterfaceType
        yield! declaring.Fields |> Seq.map _.FieldType

        for method in declaring.Methods do
            yield! namedByMethod method
            yield! namedByBody method
    }

let private isDeclaredInMarbas (reference: TypeReference) =
    match reference.Scope with
    | :? ModuleDefinition -> not (isGenerated reference)
    | _ -> false

let private usedBy (declaring: TypeDefinition) =
    namedByType declaring
    |> Seq.collect named
    |> Seq.filter isDeclaredInMarbas
    |> Seq.map placed
    |> Seq.distinct
    |> Seq.toList

let private scanned (declaring: TypeDefinition) =
    match isStartupCode declaring, isGenerated declaring with
    | true, _ ->
        placedBySource declaring
        |> Option.map (fun place ->
            { Type = place
              Uses = usedBy declaring })
    | false, true -> None
    | false, false ->
        Some
            { Type = placed declaring
              Uses = usedBy declaring }

let private types =
    lazy
        (let location = typeof<Marbas.Collections.Zipper<int>>.Assembly.Location

         use assembly =
             AssemblyDefinition.ReadAssembly(location, ReaderParameters(ReadSymbols = true))

         assembly.MainModule.GetTypes() |> Seq.choose scanned |> Seq.toList)

let outsideEveryLayer () =
    types.Force()
    |> List.choose (fun scan ->
        match scan.Type.Layer with
        | Error layer -> Some $"{scan.Type.Name} is in {layer}, which is no layer of the table"
        | Ok _ -> None)
    |> List.distinct

let private isAllowed user used =
    user = used || Map.find user mayUse |> List.contains used

let private forbiddenUse (user: Placed) (used: Placed) =
    match user.Layer, used.Layer with
    | Ok userLayer, Ok usedLayer when not (isAllowed userLayer usedLayer) ->
        Some $"{user.Name} ({userLayer}) uses {used.Name} ({usedLayer})"
    | _ -> None

let private allowedAcross (user: Placed) (used: Placed) =
    match user.Layer, used.Layer with
    | Ok userLayer, Ok usedLayer when userLayer <> usedLayer && isAllowed userLayer usedLayer ->
        Some $"{user.Name} ({userLayer}) uses {used.Name} ({usedLayer})"
    | _ -> None

let private usesThat judge =
    types.Force()
    |> List.collect (fun scan -> scan.Uses |> List.choose (judge scan.Type))
    |> List.distinct

let forbidden () = usesThat forbiddenUse

let acrossLayers () = usesThat allowedAcross

let withoutTypes () =
    let held =
        types.Force()
        |> List.choose (fun scan -> scan.Type.Layer |> Result.toOption)
        |> Set.ofList

    mayUse
    |> Map.keys
    |> Seq.filter (fun layer -> not (Set.contains layer held))
    |> Seq.map (fun layer -> $"{layer} holds no type")
    |> Seq.toList
