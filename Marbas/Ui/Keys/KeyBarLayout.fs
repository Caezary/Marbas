module Marbas.Ui.Keys.KeyBarLayout

type Cell = { X: int; Width: int }

let cell count index width =
    let start = index * width / count

    { X = start
      Width = (index + 1) * width / count - start }

let cells count width =
    [ for index in 0 .. count - 1 -> cell count index width ]
