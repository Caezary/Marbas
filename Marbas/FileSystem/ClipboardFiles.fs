namespace Marbas.FileSystem

open Marbas.Domain

type ClipboardFiles = private ClipboardFiles of intent: ClipboardIntent * paths: FileSystemPath list

module ClipboardFiles =

    let tryCreate intent (paths: FileSystemPath list) =
        match paths with
        | [] -> None
        | _ -> Some(ClipboardFiles(intent, paths))

    let intent (ClipboardFiles(intent, _)) = intent

    let paths (ClipboardFiles(_, paths)) = paths
