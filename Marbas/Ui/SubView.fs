module Marbas.Ui.SubView

open Terminal.Gui.ViewBase

let private picked<'view when 'view :> View> (wanted: 'view -> bool) (parent: View) =
    parent.SubViews
    |> Seq.pick (function
        | :? 'view as view when wanted view -> Some view
        | _ -> None)

let byId<'view when 'view :> View> (id: string) (parent: View) =
    picked<'view> (fun view -> view.Id = id) parent

let byType<'view when 'view :> View> (parent: View) = picked<'view> (fun _ -> true) parent
