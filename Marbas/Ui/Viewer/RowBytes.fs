namespace Marbas.Ui.Viewer

type RowBytes =
    { Before: byte array
      Bytes: byte array
      After: byte array }

module RowBytes =

    [<Literal>]
    let Context = 3

    let private slice (window: byte array) start stop =
        let first = max 0 start
        let last = min window.Length stop

        if last <= first then
            Array.empty
        else
            Array.sub window first (last - first)

    let within (window: byte array) (start: int) (count: int) =
        { Before = slice window (start - Context) start
          Bytes = slice window start (start + count)
          After = slice window (start + count) (start + count + Context) }
