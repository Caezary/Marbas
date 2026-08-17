module Marbas.Tests.Root

open System
open System.IO

let private separator = Path.DirectorySeparatorChar

let private prefix = if OperatingSystem.IsWindows() then @"C:\" else "/"

let relative (path: string) = path.Replace('/', separator)

let at (path: string) = prefix + relative path
