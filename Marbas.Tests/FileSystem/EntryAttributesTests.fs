module Marbas.Tests.FileSystem.EntryAttributesTests

open System.IO
open Xunit
open Marbas.Domain
open Marbas.FileSystem

[<Theory>]
[<InlineData(FileAttributes.Hidden)>]
[<InlineData(FileAttributes.System)>]
let ``the hidden and the system attribute mark an entry hidden`` attributes =
    let visibility = EntryAttributes.visibility attributes

    Assert.Equal(Hidden, visibility)

[<Theory>]
[<InlineData(FileAttributes.Normal)>]
[<InlineData(FileAttributes.ReadOnly)>]
[<InlineData(FileAttributes.Archive)>]
let ``other attributes leave an entry plain`` attributes =
    let visibility = EntryAttributes.visibility attributes

    Assert.Equal(Plain, visibility)

[<Theory>]
[<InlineData(UnixFileMode.UserExecute)>]
[<InlineData(UnixFileMode.GroupExecute)>]
[<InlineData(UnixFileMode.OtherExecute)>]
let ``an execute bit makes an entry executable`` mode =
    let executability = EntryAttributes.executability mode

    Assert.Equal(Executable, executability)

[<Theory>]
[<InlineData(UnixFileMode.None)>]
[<InlineData(UnixFileMode.UserRead)>]
[<InlineData(UnixFileMode.UserWrite)>]
let ``a mode without an execute bit leaves an entry unexecutable`` mode =
    let executability = EntryAttributes.executability mode

    Assert.Equal(NotExecutable, executability)
