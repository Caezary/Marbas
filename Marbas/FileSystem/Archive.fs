namespace Marbas.FileSystem

open System
open System.IO
open System.IO.Abstractions
open System.IO.Compression
open System.Formats.Tar
open ICSharpCode.SharpZipLib.Zip
open FsToolkit.ErrorHandling

module Archive =

    [<Literal>]
    let private AesKeySize = 256

    type private Sink =
        { AddDirectory: string -> unit
          AddFile: string -> FileSystemPath -> unit }

    let private attempting (path: FileSystemPath) action =
        try
            Ok(action ())
        with ex ->
            Error(Operations.mapException path ex)

    let private attempt path action = attempting path action |> Result.ignore

    let private entryName (fs: IFileSystem) (root: FileSystemPath) (path: FileSystemPath) =
        fs.Path
            .GetRelativePath(FileSystemPath.value root, FileSystemPath.value path)
            .Replace(fs.Path.DirectorySeparatorChar, '/')

    let private modifiedAt (fs: IFileSystem) (path: FileSystemPath) =
        DateTimeOffset(fs.FileInfo.New(FileSystemPath.value path).LastWriteTime)

    let private reading (fs: IFileSystem) (path: FileSystemPath) =
        fs.File.OpenRead(FileSystemPath.value path)

    let private zipLevel compression =
        match compression with
        | Stored -> 0
        | Normal -> 6
        | Maximum -> 9

    let private gzipLevel compression =
        match compression with
        | Stored -> CompressionLevel.NoCompression
        | Normal -> CompressionLevel.Optimal
        | Maximum -> CompressionLevel.SmallestSize

    let private intoZip (fs: IFileSystem) (request: ArchiveRequest) (stream: Stream) pack =
        use zip = new ZipOutputStream(stream)
        zip.SetLevel(zipLevel request.Compression)

        request.Password
        |> Option.iter (fun password -> zip.Password <- ArchivePassword.value password)

        pack
            { AddDirectory =
                fun name ->
                    zip.PutNextEntry(ZipEntry(name + "/"))
                    zip.CloseEntry()
              AddFile =
                fun name path ->
                    let entry =
                        ZipEntry(
                            name,
                            DateTime = (modifiedAt fs path).LocalDateTime,
                            Size = fs.FileInfo.New(FileSystemPath.value path).Length
                        )

                    if request.Password.IsSome then
                        entry.AESKeySize <- AesKeySize

                    zip.PutNextEntry entry
                    use content = reading fs path
                    content.CopyTo zip
                    zip.CloseEntry() }
    let private intoTarGz (fs: IFileSystem) compression (stream: Stream) pack =
        use compressed = new GZipStream(stream, gzipLevel compression)
        use tar = new TarWriter(compressed, TarEntryFormat.Pax, true)

        pack
            { AddDirectory = fun name -> tar.WriteEntry(PaxTarEntry(TarEntryType.Directory, name + "/"))
              AddFile =
                fun name path ->
                    use content = reading fs path

                    PaxTarEntry(
                        TarEntryType.RegularFile,
                        name,
                        DataStream = content,
                        ModificationTime = modifiedAt fs path
                    )
                    |> tar.WriteEntry }

    let rec private add (fs: IFileSystem) (control: Operations.BatchControl) sink root source =
        if control.IsCancelled() then
            Error(Operations.Cancelled source)
        else
            match FileSystemPath.kind fs source with
            | NoEntry -> Error(Operations.SourceNotFound source)
            | DirectoryEntry ->
                result {
                    do! attempt source (fun () -> sink.AddDirectory(entryName fs root source))

                    let! children =
                        attempting source (fun () ->
                            fs.Directory.EnumerateFileSystemEntries(FileSystemPath.value source)
                            |> Seq.map FileSystemPath.create
                            |> List.ofSeq)

                    do!
                        children
                        |> List.traverseResultM (add fs control sink root)
                        |> Result.ignore
                }
            | FileEntry ->
                control.Report source
                attempt source (fun () -> sink.AddFile (entryName fs root source) source)

    let private packAll (fs: IFileSystem) control sources sink =
        sources
        |> Operations.allWatched fs control (fun source -> add fs control sink (FileSystemPath.directory fs source) source)

    let private packInto fs control (request: ArchiveRequest) sources stream =
        match ArchiveFormat.kind request.Format with
        | Zip -> intoZip fs request stream (packAll fs control sources)
        | TarGz -> intoTarGz fs request.Compression stream (packAll fs control sources)
        | uncreatable -> invalidOp $"no writer for {ArchiveKind.extension uncreatable}"

    let write (fs: IFileSystem) control (request: ArchiveRequest) sources =
        match attempting request.Path (fun () -> fs.File.Create(FileSystemPath.value request.Path)) with
        | Error error -> sources |> List.map (fun source -> Operations.itemResult fs source (Error error))
        | Ok created ->
            let packed =
                use stream = created
                packInto fs control request sources stream

            if control.IsCancelled() then
                Operations.delete fs request.Path |> ignore

                sources
                |> List.map (fun source -> Operations.itemResult fs source (Error(Operations.Cancelled source)))
            else
                packed

    let private within (fs: IFileSystem) (directory: FileSystemPath) (path: FileSystemPath) =
        let inside =
            FileSystemPath.value directory + string fs.Path.DirectorySeparatorChar

        (FileSystemPath.value path).StartsWith(inside, StringComparison.OrdinalIgnoreCase)

    let wouldPackItself (fs: IFileSystem) (target: FileSystemPath) sources =
        sources
        |> List.exists (fun source -> source = target || within fs source target)
