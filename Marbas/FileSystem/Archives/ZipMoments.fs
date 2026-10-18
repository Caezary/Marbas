namespace Marbas.FileSystem.Archives

open System
open System.Buffers
open System.IO

type ZipMoments = private ZipMoments of int64 array

module ZipMoments =

    [<Literal>]
    let private UnixTimeTag = 0x5455

    [<Literal>]
    let private UnixTimeSize = 5

    [<Literal>]
    let private ModifiedFlag = 1uy

    [<Literal>]
    let private NtfsTag = 0x000A

    [<Literal>]
    let private NtfsReserved = 4

    [<Literal>]
    let private NtfsTimes = 1

    [<Literal>]
    let private FileTimeSize = 8

    [<Literal>]
    let private LatestFileTime = 2650467743999999999L

    [<Literal>]
    let private TicksAtFileTimeZero = 504911232000000000L

    [<Literal>]
    let private TicksAtUnixTimeZero = 621355968000000000L

    [<Literal>]
    let private NoMoment = 0L

    [<Literal>]
    let private TagAndSize = 4

    [<Literal>]
    let private SizeAfterTag = 2

    [<Literal>]
    let private EntryRecordSignature = 0x02014b50u

    [<Literal>]
    let private EntryRecordSize = 46

    [<Literal>]
    let private NameLengthInEntryRecord = 28

    [<Literal>]
    let private FieldsLengthInEntryRecord = 30

    [<Literal>]
    let private CommentLengthInEntryRecord = 32

    [<Literal>]
    let private EndRecordSignature = 0x06054b50u

    [<Literal>]
    let private EndRecordSize = 22

    [<Literal>]
    let private DirectoryLengthInEndRecord = 12

    [<Literal>]
    let private DirectoryStartInEndRecord = 16

    [<Literal>]
    let private LongestComment = 0xFFFF

    [<Literal>]
    let private LongLocatorSignature = 0x07064b50u

    [<Literal>]
    let private LongLocatorSize = 20

    [<Literal>]
    let private LongEndRecordStartInLocator = 8

    [<Literal>]
    let private LongEndRecordSignature = 0x06064b50u

    [<Literal>]
    let private LongEndRecordSize = 56

    [<Literal>]
    let private DirectoryLengthInLongEndRecord = 40

    [<Literal>]
    let private DirectoryStartInLongEndRecord = 48

    type private Extent = { Start: int64; Length: int64 }

    let none = ZipMoments Array.empty

    let at ordinal (ZipMoments ticks) =
        if ordinal < ticks.Length && ticks[ordinal] <> NoMoment then
            ValueSome(DateTimeOffset(ticks[ordinal], TimeSpan.Zero).ToLocalTime())
        else
            ValueNone

    let fieldFor (moment: DateTimeOffset) =
        match moment.ToUnixTimeSeconds() with
        | seconds when seconds < 0L || seconds > int64 Int32.MaxValue -> None
        | seconds ->
            Some
                [| byte UnixTimeTag
                   byte (UnixTimeTag >>> 8)
                   byte UnixTimeSize
                   0uy
                   ModifiedFlag
                   byte seconds
                   byte (seconds >>> 8)
                   byte (seconds >>> 16)
                   byte (seconds >>> 24) |]

    let private short (bytes: byte array) position =
        int bytes[position] ||| (int bytes[position + 1] <<< 8)

    let private word (bytes: byte array) position =
        uint32 (short bytes position) ||| (uint32 (short bytes (position + 2)) <<< 16)

    let private long (bytes: byte array) position =
        int64 (word bytes position) ||| (int64 (word bytes (position + 4)) <<< 32)

    let private ticksOfFileTime (fileTime: int64) =
        if fileTime > 0L && fileTime <= LatestFileTime then
            fileTime + TicksAtFileTimeZero
        else
            NoMoment

    let private ticksOfUnixTime (seconds: uint32) =
        TicksAtUnixTimeZero + int64 seconds * TimeSpan.TicksPerSecond

    let rec private fieldTagged tag (bytes: byte array) position limit =
        if position + TagAndSize > limit then
            ValueNone
        else
            let data = position + TagAndSize
            let next = data + short bytes (position + SizeAfterTag)

            if next > limit then
                ValueNone
            elif short bytes position = tag then
                ValueSome(struct (data, next))
            else
                fieldTagged tag bytes next limit

    let private ntfsTicks (bytes: byte array) data limit =
        match fieldTagged NtfsTimes bytes (data + NtfsReserved) limit with
        | ValueSome(struct (times, timesLimit)) when timesLimit - times >= FileTimeSize ->
            ticksOfFileTime (long bytes times)
        | _ -> NoMoment

    let private unixTicks (bytes: byte array) data limit =
        if limit - data >= UnixTimeSize && bytes[data] &&& ModifiedFlag <> 0uy then
            ticksOfUnixTime (word bytes (data + 1))
        else
            NoMoment

    let private finerOf finer coarser =
        match finer with
        | NoMoment -> coarser
        | exact -> exact

    let rec private exactAmong (bytes: byte array) position limit finer coarser =
        if position + TagAndSize > limit then
            finerOf finer coarser
        else
            let data = position + TagAndSize
            let next = data + short bytes (position + SizeAfterTag)

            if next > limit then
                finerOf finer coarser
            else
                match short bytes position with
                | NtfsTag -> exactAmong bytes next limit (ntfsTicks bytes data next) coarser
                | UnixTimeTag -> exactAmong bytes next limit finer (unixTicks bytes data next)
                | _ -> exactAmong bytes next limit finer coarser

    let rec private momentsFrom (directory: byte array) limit position found =
        if
            position + EntryRecordSize > limit
            || word directory position <> EntryRecordSignature
        then
            found
        else
            let fields =
                position
                + EntryRecordSize
                + short directory (position + NameLengthInEntryRecord)

            let comment = fields + short directory (position + FieldsLengthInEntryRecord)

            if comment > limit then
                found
            else
                momentsFrom
                    directory
                    limit
                    (comment + short directory (position + CommentLengthInEntryRecord))
                    (exactAmong directory fields comment NoMoment NoMoment :: found)

    let private inRecordOrder (found: int64 list) =
        if List.forall ((=) NoMoment) found then
            none
        else
            found |> List.rev |> Array.ofList |> ZipMoments

    let private readAt (stream: Stream) (start: int64) length =
        let bytes = Array.zeroCreate<byte> length
        stream.Position <- start
        stream.ReadExactly(bytes, 0, length)
        bytes

    let private within (length: int64) (extent: Extent) =
        extent.Start >= 0L
        && extent.Length >= 0L
        && extent.Length <= int64 Array.MaxLength
        && extent.Start <= length - extent.Length

    let rec private endRecordAtOrBefore (tail: byte array) position =
        if position < 0 then
            None
        elif word tail position = EndRecordSignature then
            Some position
        else
            endRecordAtOrBefore tail (position - 1)

    let private longEndRecord (stream: Stream) length (tail: byte array) endRecord =
        let locator = endRecord - LongLocatorSize

        if locator < 0 || word tail locator <> LongLocatorSignature then
            None
        else
            Some
                { Start = long tail (locator + LongEndRecordStartInLocator)
                  Length = int64 LongEndRecordSize }
            |> Option.filter (within length)
            |> Option.map (fun extent -> readAt stream extent.Start LongEndRecordSize)
            |> Option.filter (fun found -> word found 0 = LongEndRecordSignature)

    let private directoryOf (stream: Stream) length (tail: byte array) endRecord =
        match longEndRecord stream length tail endRecord with
        | Some found ->
            { Start = long found DirectoryStartInLongEndRecord
              Length = long found DirectoryLengthInLongEndRecord }
        | None ->
            { Start = int64 (word tail (endRecord + DirectoryStartInEndRecord))
              Length = int64 (word tail (endRecord + DirectoryLengthInEndRecord)) }

    let private momentsWithin (stream: Stream) (extent: Extent) =
        let length = int extent.Length
        let directory = ArrayPool<byte>.Shared.Rent length

        try
            stream.Position <- extent.Start
            stream.ReadExactly(directory, 0, length)
            momentsFrom directory length 0 [] |> inRecordOrder
        finally
            ArrayPool<byte>.Shared.Return directory

    let read (stream: Stream) =
        let before = stream.Position
        let length = stream.Length

        let reach =
            int (min length (int64 (LongLocatorSize + EndRecordSize + LongestComment)))

        let tail = readAt stream (length - int64 reach) reach

        let moments =
            endRecordAtOrBefore tail (reach - EndRecordSize)
            |> Option.map (directoryOf stream length tail)
            |> Option.filter (within length)
            |> Option.map (momentsWithin stream)
            |> Option.defaultValue none

        stream.Position <- before
        moments
