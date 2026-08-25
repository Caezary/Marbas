module Marbas.Ui.Panels.PageJump

type Page =
    { FirstVisible: int
      Height: int
      Count: int }

let private height page = max 1 page.Height

let private clamp page index =
    index |> max 0 |> min (max 0 (page.Count - 1))

let forward page cursor =
    let lastVisible = clamp page (page.FirstVisible + height page - 1)

    if cursor < lastVisible then
        lastVisible
    else
        clamp page (cursor + height page)

let backward page cursor =
    let firstVisible = clamp page page.FirstVisible

    if cursor > firstVisible then
        firstVisible
    else
        clamp page (cursor - height page)
