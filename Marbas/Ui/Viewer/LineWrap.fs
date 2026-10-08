module Marbas.Ui.Viewer.LineWrap

let segments width (text: string) =
    if width <= 0 || text.Length <= width then
        [ text ]
    else
        [ for start in 0..width .. text.Length - 1 -> text.Substring(start, min width (text.Length - start)) ]

let height width text = List.length (segments width text)

let visible width left (text: string) =
    if width <= 0 || left >= text.Length then
        ""
    else
        let start = max 0 left
        text.Substring(start, min width (text.Length - start))
