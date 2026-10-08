module Marbas.FileSystem.EntryAttributes

open System.IO
open Marbas.Domain

let private markedHidden (attributes: FileAttributes) =
    attributes.HasFlag FileAttributes.Hidden
    || attributes.HasFlag FileAttributes.System

let private markedExecutable (mode: UnixFileMode) =
    mode.HasFlag UnixFileMode.UserExecute
    || mode.HasFlag UnixFileMode.GroupExecute
    || mode.HasFlag UnixFileMode.OtherExecute

let visibility attributes =
    if markedHidden attributes then Hidden else Plain

let executability mode =
    if markedExecutable mode then Executable else NotExecutable
