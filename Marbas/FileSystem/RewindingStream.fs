namespace Marbas.FileSystem

open System
open System.IO

module private Rewinding =

    [<Literal>]
    let SkipLength = 65536

type RewindingStream(length: int64, openSource: unit -> Stream) =
    inherit Stream()

    let mutable current: Stream = null
    let mutable position = 0L
    let scratch = lazy Array.zeroCreate<byte> Rewinding.SkipLength

    let currentStream () =
        if isNull current then
            current <- openSource ()
            position <- 0L

        current

    let closed () =
        if not (isNull current) then
            current.Dispose()
            current <- null

        position <- 0L

    let rec skipping (stream: Stream) (remaining: int64) =
        if remaining > 0L then
            let wanted = int (min remaining (int64 Rewinding.SkipLength))

            match stream.Read(scratch.Value, 0, wanted) with
            | 0 -> ()
            | read ->
                position <- position + int64 read
                skipping stream (remaining - int64 read)

    member private _.Reach(target: int64) =
        if target < position then
            closed ()

        skipping (currentStream ()) (target - position)

    override _.CanRead = true

    override _.CanSeek = true

    override _.CanWrite = false

    override _.Length = length

    override this.Position
        with get () = position
        and set value = this.Seek(value, SeekOrigin.Begin) |> ignore

    override _.Flush() = ()

    override _.SetLength(_: int64) = raise (NotSupportedException())

    override _.Write(_: byte array, _: int, _: int) = raise (NotSupportedException())

    override _.Read(buffer: byte array, offset: int, count: int) =
        let read = currentStream().Read(buffer, offset, count)
        position <- position + int64 read
        read

    override this.Seek(offset: int64, origin: SeekOrigin) =
        let target =
            match origin with
            | SeekOrigin.Current -> position + offset
            | SeekOrigin.End -> length + offset
            | _ -> offset

        this.Reach(max 0L target)
        position

    override _.Dispose(disposing: bool) =
        if disposing then
            closed ()

        base.Dispose disposing
