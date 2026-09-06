namespace Marbas.State

type Zipper<'a> =
    { Before: 'a list
      Current: 'a
      After: 'a list }

module Zipper =

    let single item =
        { Before = []
          Current = item
          After = [] }

    let current (zipper: Zipper<'a>) = zipper.Current

    let items (zipper: Zipper<'a>) =
        List.rev zipper.Before @ [ zipper.Current ] @ zipper.After

    let count (zipper: Zipper<'a>) =
        List.length zipper.Before + 1 + List.length zipper.After

    let position (zipper: Zipper<'a>) = List.length zipper.Before

    let update f (zipper: Zipper<'a>) = { zipper with Current = f zipper.Current }

    let insert item (zipper: Zipper<'a>) =
        { zipper with
            Before = zipper.Current :: zipper.Before
            Current = item }

    let remove (zipper: Zipper<'a>) =
        match zipper.After, zipper.Before with
        | next :: rest, _ ->
            { zipper with
                Current = next
                After = rest }
        | [], previous :: rest ->
            { Before = rest
              Current = previous
              After = [] }
        | [], [] -> zipper

    let at position (zipper: Zipper<'a>) =
        let all = items zipper
        let index = position |> max 0 |> min (List.length all - 1)

        { Before = all |> List.take index |> List.rev
          Current = all[index]
          After = all |> List.skip (index + 1) }

    let forward (zipper: Zipper<'a>) =
        at ((position zipper + 1) % count zipper) zipper

    let backward (zipper: Zipper<'a>) =
        at ((position zipper - 1 + count zipper) % count zipper) zipper
