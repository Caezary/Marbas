namespace Marbas.FileSystem

open System
open System.IO.Abstractions
open Marbas.Domain

type XdgDirectories =
    { Data: string list
      Config: string list }

module XdgDirectories =

    [<Literal>]
    let private DataFallback = "/usr/local/share:/usr/share"

    [<Literal>]
    let private ConfigFallback = "/etc/xdg"

    [<Literal>]
    let private LocalData = ".local/share"

    [<Literal>]
    let private LocalConfig = ".config"

    let private listed (raw: string) =
        raw.Split ':'
        |> List.ofArray
        |> List.map _.Trim()
        |> List.filter (fun part -> part <> "")

    let private orElse fallback (raw: string option) =
        raw
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.defaultValue fallback

    let resolve (fs: IFileSystem) home dataHome dataDirectories configHome configDirectories =
        { Data =
            listed (orElse (fs.Path.Combine(home, LocalData)) dataHome)
            @ listed (orElse DataFallback dataDirectories)
          Config =
            listed (orElse (fs.Path.Combine(home, LocalConfig)) configHome)
            @ listed (orElse ConfigFallback configDirectories) }

module DesktopHandlers =

    [<Literal>]
    let private ApplicationsDirectory = "applications"

    [<Literal>]
    let private MimeDirectory = "mime"

    let private linesOf (fs: IFileSystem) path =
        try
            if fs.File.Exists path then
                fs.File.ReadAllLines path |> List.ofArray
            else
                []
        with _ ->
            []

    let private globsFiles (fs: IFileSystem) data =
        data
        |> List.map (fun directory -> fs.Path.Combine(directory, MimeDirectory, MimeDatabase.GlobsFile))

    let private applicationFiles (fs: IFileSystem) (directories: XdgDirectories) =
        (directories.Config
         |> List.map (fun directory -> fs.Path.Combine(directory, MimeDatabase.ApplicationsFile)))
        @ (directories.Data
           |> List.map (fun directory ->
               fs.Path.Combine(directory, ApplicationsDirectory, MimeDatabase.ApplicationsFile)))

    let private desktopFile (fs: IFileSystem) data (identifier: string) =
        data
        |> List.map (fun directory -> fs.Path.Combine(directory, ApplicationsDirectory, identifier))
        |> List.tryFind fs.File.Exists

    let read (fs: IFileSystem) (directories: XdgDirectories) (name: EntryName) =
        match FileExtension.ofName name with
        | None -> []
        | Some extension ->
            let types =
                globsFiles fs directories.Data
                |> List.collect (linesOf fs >> MimeDatabase.typesFor extension)
                |> List.distinct

            let lists = applicationFiles fs directories |> List.map (linesOf fs)

            types
            |> List.collect (fun kind -> lists |> List.collect (MimeDatabase.applicationsFor kind))
            |> List.distinct
            |> List.choose (desktopFile fs directories.Data)
            |> List.choose (linesOf fs >> DesktopEntry.parse)
            |> Handler.distinct
