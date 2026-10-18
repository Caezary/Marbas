module Marbas.Tests.Messages.ProblemTests

open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Profile
open Marbas.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private fs = MockFileSystem()

let private gone = DirectoryListing.DirectoryNotFound(fp (at "gone"))

let private unreadable =
    DirectoryListing.ArchiveUnreadable(ArchiveNotFound(fp (at "gone.zip")))

let private damaged = [ ArchiveDamaged(fp (at "data.zip"), "bad header") ]

let private taken = Operations.TargetExists(fp (at "start/docs"))

let private failed: Operations.ItemResult list =
    [ { Source = Operations.ItemOnDisk(fp (at "start/gone.txt"))
        Outcome = Error(Operations.SourceNotFound(fp (at "start/gone.txt"))) } ]

let private unopened = NothingAssociated(fp (at "start/notes.odd"))

let private unstarted = LaunchRefused(fp "show", "denied")

let private historyDenied = HistoryAccessDenied(fp (at "config/history.conf"))

let private settingsDenied = SettingsAccessDenied(fp (at "config/marbas.conf"))

let private favoritesDenied = FavoritesAccessDenied(fp (at "config/favorites.conf"))

let private menuDenied = UserMenuAccessDenied(fp (at "config/user-menu.conf"))

let private unseen = FileNotFound(fp (at "start/notes.txt"))

let private locked = PasswordNeeded(fp (at "start/backup.zip"))

[<Fact>]
let ``a directory that could not be listed is titled as a directory`` () =
    let title = Problem.title (Problem.Listing gone)

    Assert.Equal(ListingErrorMessage.Title, title)

[<Fact>]
let ``an archive that could not be listed is titled as an archive`` () =
    let title = Problem.title (Problem.Listing unreadable)

    Assert.Equal(ArchiveErrorMessage.Title, title)

[<Fact>]
let ``a listing that failed is told in the words of a listing error`` () =
    let words = Problem.describe fs (Problem.Listing gone)

    Assert.Equal(ListingErrorMessage.describe gone, words)

[<Fact>]
let ``hits that could not be verified are titled as an archive`` () =
    let title = Problem.title (Problem.UnverifiedHits damaged)

    Assert.Equal(ArchiveErrorMessage.Title, title)

[<Fact>]
let ``hits that could not be verified name each archive and say they stay listed`` () =
    let words = Problem.describe fs (Problem.UnverifiedHits damaged)

    Assert.Equal(ArchiveErrorMessage.describeUnverified damaged, words)

[<Fact>]
let ``an operation that failed is titled as one`` () =
    let title = Problem.title (Problem.Operation taken)

    Assert.Equal(OperationErrorMessage.Title, title)

[<Fact>]
let ``an operation that failed is told in the words of an operation error`` () =
    let words = Problem.describe fs (Problem.Operation taken)

    Assert.Equal(OperationErrorMessage.describe taken, words)

[<Fact>]
let ``the items a batch failed on are titled as a failed operation`` () =
    let title = Problem.title (Problem.ItemFailures failed)

    Assert.Equal(OperationErrorMessage.Title, title)

[<Fact>]
let ``the items a batch failed on are told one to a line, each by its name`` () =
    let words = Problem.describe fs (Problem.ItemFailures failed)

    Assert.Equal(OperationErrorMessage.describeFailures fs failed, words)

[<Fact>]
let ``a file that could not be run is titled as a file`` () =
    let title = Problem.title (Problem.Launch unopened)

    Assert.Equal(LaunchErrorMessage.Title, title)

[<Fact>]
let ``a file that could not be run is told in the words of a launch error`` () =
    let words = Problem.describe fs (Problem.Launch unopened)

    Assert.Equal(LaunchErrorMessage.describe unopened, words)

[<Fact>]
let ``a command that could not be run is titled as a command`` () =
    let title = Problem.title (Problem.Command unstarted)

    Assert.Equal(LaunchErrorMessage.CommandTitle, title)

[<Fact>]
let ``a command that could not be run is told in the words of a launch error`` () =
    let words = Problem.describe fs (Problem.Command unstarted)

    Assert.Equal(LaunchErrorMessage.describe unstarted, words)

[<Fact>]
let ``a command line that was not expanded is titled as a command`` () =
    let title = Problem.title (Problem.Expansion NoProgram)

    Assert.Equal(ExpansionRefusalMessage.Title, title)

[<Fact>]
let ``a command line that was not expanded is told in the words of its refusal`` () =
    let words = Problem.describe fs (Problem.Expansion NoProgram)

    Assert.Equal(ExpansionRefusalMessage.describe NoProgram, words)

[<Fact>]
let ``a history that could not be kept is titled as the history`` () =
    let title = Problem.title (Problem.History historyDenied)

    Assert.Equal(HistoryErrorMessage.Title, title)

[<Fact>]
let ``a history that could not be kept is told in the words of a history error`` () =
    let words = Problem.describe fs (Problem.History historyDenied)

    Assert.Equal(HistoryErrorMessage.describe historyDenied, words)

[<Fact>]
let ``a clipboard that could not be used is titled as the clipboard`` () =
    let title = Problem.title (Problem.Clipboard ClipboardUnavailable)

    Assert.Equal(ClipboardErrorMessage.Title, title)

[<Fact>]
let ``a clipboard that could not be used is told in the words of a clipboard error`` () =
    let words = Problem.describe fs (Problem.Clipboard ClipboardUnavailable)

    Assert.Equal(ClipboardErrorMessage.describe ClipboardUnavailable, words)

[<Fact>]
let ``settings that could not be written are titled as the session`` () =
    let title = Problem.title (Problem.Settings settingsDenied)

    Assert.Equal(SettingsErrorMessage.Title, title)

[<Fact>]
let ``settings that could not be written are told in the words of a settings error`` () =
    let words = Problem.describe fs (Problem.Settings settingsDenied)

    Assert.Equal(SettingsErrorMessage.describe settingsDenied, words)

[<Fact>]
let ``favourites that could not be kept are titled as the favourites`` () =
    let title = Problem.title (Problem.Favorites favoritesDenied)

    Assert.Equal(FavoritesErrorMessage.Title, title)

[<Fact>]
let ``favourites that could not be kept are told in the words of a favourites error`` () =
    let words = Problem.describe fs (Problem.Favorites favoritesDenied)

    Assert.Equal(FavoritesErrorMessage.describe favoritesDenied, words)

[<Fact>]
let ``a user menu that could not be kept is titled as the user menu`` () =
    let title = Problem.title (Problem.UserMenu menuDenied)

    Assert.Equal(UserMenuErrorMessage.Title, title)

[<Fact>]
let ``a user menu that could not be kept is told in the words of a user menu error`` () =
    let words = Problem.describe fs (Problem.UserMenu menuDenied)

    Assert.Equal(UserMenuErrorMessage.describe menuDenied, words)

[<Fact>]
let ``a file that could not be viewed is titled as a file to view`` () =
    let title = Problem.title (Problem.Content unseen)

    Assert.Equal(ContentErrorMessage.Title, title)

[<Fact>]
let ``a file that could not be viewed is told in the words of a content error`` () =
    let words = Problem.describe fs (Problem.Content unseen)

    Assert.Equal(ContentErrorMessage.describe unseen, words)

[<Fact>]
let ``an archive entry that could not be viewed is titled as an archive`` () =
    let title = Problem.title (Problem.Archive locked)

    Assert.Equal(ArchiveErrorMessage.Title, title)

[<Fact>]
let ``an archive entry that could not be viewed is told in the words of an archive error`` () =
    let words = Problem.describe fs (Problem.Archive locked)

    Assert.Equal(ArchiveErrorMessage.describe locked, words)
