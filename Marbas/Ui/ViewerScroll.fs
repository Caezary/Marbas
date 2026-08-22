module Marbas.Ui.ViewerScroll

let clamped bound value = value |> max 0 |> min (max 0 bound)

let moved bound top delta = clamped bound (top + delta)

let filling height (heights: int seq) =
    let room = max 1 height

    let taken =
        heights
        |> Seq.scan (fun total step -> total + max 1 step) 0
        |> Seq.skip 1
        |> Seq.takeWhile (fun total -> total <= room)
        |> Seq.length

    max 1 taken

let percent (offset: int64) (length: int64) =
    if length <= 0L then
        100
    else
        int (min 100L (max 0L offset * 100L / length))
