module Marbas.Tests.Ui.Dialogs.DriveLineTests

open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Ui.Dialogs

let private fp = FileSystemPath.create

let private system =
    MountedDrive
        { Root = fp "/"
          Label = None
          Kind = Fixed
          Size = 1000204886016L
          Free = 128849018880L }

let private stick =
    MountedDrive
        { Root = fp "/media/user/stick"
          Label = Some "STICK"
          Kind = Removable
          Size = 64023257088L
          Free = 1073741824L }

let private home = HomeDirectory(fp "/home/user")

[<Fact>]
let ``a drive's line carries its root, label, kind, size and free space`` () =
    let lines = DriveLine.describe [ stick ]

    Assert.Equal<string list>([ "/media/user/stick  STICK  removable  59.6 GB  1.0 GB free" ], lines)

[<Fact>]
let ``the columns line up across drives`` () =
    let lines = DriveLine.describe [ system; stick ]

    let expected =
        [ "/                         fixed      931.5 GB  120.0 GB free"
          "/media/user/stick  STICK  removable   59.6 GB    1.0 GB free" ]

    Assert.Equal<string list>(expected, lines)

[<Fact>]
let ``the home directory is a line of its own kind without sizes`` () =
    let lines = DriveLine.describe [ system; home ]

    let expected = [ "/           fixed  931.5 GB  120.0 GB free"; "/home/user  home" ]

    Assert.Equal<string list>(expected, lines)

[<Fact>]
let ``every kind is spelled out`` () =
    let texts = [ Fixed; Removable; Network; Optical ] |> List.map DriveLine.kindText

    Assert.Equal<string list>([ "fixed"; "removable"; "network"; "optical" ], texts)
