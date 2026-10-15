module Marbas.Tests.Layers.SystemCalls

open System
open System.IO
open Mono.Cecil

type private Place =
    | Adapters
    | Program
    | Elsewhere

type private Watched =
    | EveryMember
    | Members of string list

let private watchedTypes =
    [ "System.IO.File", EveryMember
      "System.IO.Directory", EveryMember
      "System.IO.FileInfo", EveryMember
      "System.IO.DirectoryInfo", EveryMember
      "System.IO.FileSystemInfo", EveryMember
      "System.IO.DriveInfo", EveryMember
      "System.IO.FileSystemWatcher", EveryMember
      "System.IO.FileStream", EveryMember
      "System.IO.RandomAccess", EveryMember
      "System.IO.Path", Members [ "GetFullPath"; "GetTempPath"; "GetTempFileName"; "Exists" ]
      "System.IO.Abstractions.FileSystem", EveryMember
      "System.Diagnostics.Process", EveryMember
      "System.Diagnostics.Stopwatch", EveryMember
      "System.Environment", EveryMember
      "System.Console", EveryMember
      "System.OperatingSystem", EveryMember
      "System.Runtime.InteropServices.RuntimeInformation", EveryMember
      "System.Threading.Thread", EveryMember
      "System.Threading.ThreadPool", EveryMember
      "System.Threading.Timer", EveryMember
      "System.Timers.Timer", EveryMember
      "System.Threading.Tasks.Task", Members [ "Run" ]
      "System.DateTime", Members [ "get_Now"; "get_UtcNow"; "get_Today" ]
      "System.DateTimeOffset", Members [ "get_Now"; "get_UtcNow" ]
      "System.Guid", Members [ "NewGuid" ]
      "System.Random", EveryMember ]
    |> Map.ofList

let private watchedNamespaces = [ "Microsoft.Win32"; "System.Net" ]

let rec private outermost (declaring: TypeReference) =
    match declaring.DeclaringType with
    | null -> declaring
    | outer -> outermost outer

let private inWatchedNamespace (declaring: TypeReference) =
    let space = (outermost declaring).Namespace

    watchedNamespaces
    |> List.exists (fun watched -> space = watched || space.StartsWith(watched + ".", StringComparison.Ordinal))

let private isWatched (callee: MethodReference) =
    let declaring = callee.DeclaringType.GetElementType()

    match Map.tryFind declaring.FullName watchedTypes with
    | Some EveryMember -> true
    | Some(Members names) -> List.contains callee.Name names
    | None -> inWatchedNamespace declaring

let private placeOfSource (url: string) =
    let source = url.Replace('\\', '/')

    match Path.GetFileName(Path.GetDirectoryName source), Path.GetFileName source with
    | "Adapters", _ -> Adapters
    | "Marbas", "Program.fs" -> Program
    | _ -> Elsewhere

let private placeOfType (declaring: TypeDefinition) =
    match (outermost declaring).FullName with
    | "Marbas.Program" -> Program
    | name when name.StartsWith("Marbas.Adapters.", StringComparison.Ordinal) -> Adapters
    | _ -> Elsewhere

let private placeOf (method: MethodDefinition) =
    method.DebugInformation.SequencePoints
    |> Seq.tryHead
    |> Option.map (fun point -> placeOfSource point.Document.Url)
    |> Option.defaultWith (fun () -> placeOfType method.DeclaringType)

let private invokedBy (method: MethodDefinition) =
    if method.HasBody then
        method.Body.Instructions
        |> Seq.choose (fun instruction ->
            match instruction.Operand with
            | :? MethodReference as callee when isWatched callee ->
                Some $"{callee.DeclaringType.GetElementType().FullName}::{callee.Name}"
            | _ -> None)
        |> Seq.toList
    else
        []

let private declaredBy (method: MethodDefinition) =
    if method.IsPInvokeImpl then
        [ $"the P/Invoke {method.PInvokeInfo.Module.Name}::{method.PInvokeInfo.EntryPoint}" ]
    else
        []

let private callsOf (method: MethodDefinition) =
    let place = placeOf method

    declaredBy method @ invokedBy method
    |> List.map (fun callee -> place, $"{method.DeclaringType.FullName}::{method.Name} calls {callee}")

let private calls =
    lazy
        (let location = typeof<Marbas.Collections.Zipper<int>>.Assembly.Location

         use assembly =
             AssemblyDefinition.ReadAssembly(location, ReaderParameters(ReadSymbols = true))

         assembly.MainModule.GetTypes()
         |> Seq.collect _.Methods
         |> Seq.collect callsOf
         |> Seq.distinct
         |> Seq.toList)

let private madeAt place =
    calls.Force() |> List.filter (fst >> (=) place) |> List.map snd

let outsideAdapters () = madeAt Elsewhere

let inAdapters () = madeAt Adapters
