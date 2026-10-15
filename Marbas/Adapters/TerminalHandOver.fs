module Marbas.Adapters.TerminalHandOver

open System
open System.Drawing
open System.Reflection
open FsToolkit.ErrorHandling
open Terminal.Gui.App

type DriverRestart =
    { Coordinator: PropertyInfo
      CreateDriver: MethodInfo
      SubscribeDriverEvents: MethodInfo }

let private hidden = BindingFlags.Instance ||| BindingFlags.NonPublic

let restartOf (implementation: Type) =
    option {
        let! coordinator = implementation.GetProperty("Coordinator", hidden) |> Option.ofObj
        let! createDriver = implementation.GetMethod("CreateDriver", hidden) |> Option.ofObj

        let! subscribe =
            implementation.GetMethod("SubscribeDriverEvents", hidden) |> Option.ofObj

        return
            { Coordinator = coordinator
              CreateDriver = createDriver
              SubscribeDriverEvents = subscribe }
    }

let private stop (app: IApplication) restart =
    match restart.Coordinator.GetValue app with
    | :? IMainLoopCoordinator as coordinator -> coordinator.Stop()
    | _ -> ()

let private terminalScreen () =
    try
        match Console.WindowWidth, Console.WindowHeight with
        | width, height when width > 0 && height > 0 -> Some(Rectangle(0, 0, width, height))
        | _ -> None
    with _ ->
        None

let private start (app: IApplication) restart driverName =
    restart.CreateDriver.Invoke(app, [| box driverName |]) |> ignore
    restart.SubscribeDriverEvents.Invoke(app, [||]) |> ignore
    terminalScreen () |> Option.iter (fun screen -> app.Screen <- screen)
    app.LayoutAndDraw true

let private sharing (app: IApplication) (work: unit -> 'result) =
    let result = work ()
    app.ClearScreenNextIteration <- true
    result

let private handingOver (app: IApplication) restart driverName (work: unit -> 'result) =
    stop app restart

    try
        work ()
    finally
        start app restart driverName

let lend (app: IApplication) (work: unit -> 'result) =
    match restartOf (app.GetType()), Option.ofObj app.Driver with
    | Some restart, Some driver -> handingOver app restart (driver.GetName()) work
    | _ -> sharing app work
