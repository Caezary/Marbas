namespace Marbas.Messages

open System.IO.Abstractions
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Profile

[<RequireQualifiedAccess>]
type Problem =
    | Listing of DirectoryListing.ListingError
    | UnverifiedHits of refused: ArchiveError list
    | Operation of Operations.OperationError
    | ItemFailures of Operations.ItemResult list
    | Launch of LaunchError
    | Command of LaunchError
    | Expansion of ExpansionRefusal
    | History of HistoryError
    | Clipboard of ClipboardError
    | Settings of SettingsError
    | Favorites of FavoritesError
    | UserMenu of UserMenuError
    | Content of ContentError
    | Archive of ArchiveError

module Problem =

    let title problem =
        match problem with
        | Problem.Listing error -> ListingErrorMessage.title error
        | Problem.UnverifiedHits _
        | Problem.Archive _ -> ArchiveErrorMessage.Title
        | Problem.Operation _
        | Problem.ItemFailures _ -> OperationErrorMessage.Title
        | Problem.Launch _ -> LaunchErrorMessage.Title
        | Problem.Command _ -> LaunchErrorMessage.CommandTitle
        | Problem.Expansion _ -> ExpansionRefusalMessage.Title
        | Problem.History _ -> HistoryErrorMessage.Title
        | Problem.Clipboard _ -> ClipboardErrorMessage.Title
        | Problem.Settings _ -> SettingsErrorMessage.Title
        | Problem.Favorites _ -> FavoritesErrorMessage.Title
        | Problem.UserMenu _ -> UserMenuErrorMessage.Title
        | Problem.Content _ -> ContentErrorMessage.Title

    let describe (fs: IFileSystem) problem =
        match problem with
        | Problem.Listing error -> ListingErrorMessage.describe error
        | Problem.UnverifiedHits refused -> ArchiveErrorMessage.describeUnverified refused
        | Problem.Archive error -> ArchiveErrorMessage.describe error
        | Problem.Operation error -> OperationErrorMessage.describe error
        | Problem.ItemFailures failures -> OperationErrorMessage.describeFailures fs failures
        | Problem.Launch error
        | Problem.Command error -> LaunchErrorMessage.describe error
        | Problem.Expansion refusal -> ExpansionRefusalMessage.describe refusal
        | Problem.History error -> HistoryErrorMessage.describe error
        | Problem.Clipboard error -> ClipboardErrorMessage.describe error
        | Problem.Settings error -> SettingsErrorMessage.describe error
        | Problem.Favorites error -> FavoritesErrorMessage.describe error
        | Problem.UserMenu error -> UserMenuErrorMessage.describe error
        | Problem.Content error -> ContentErrorMessage.describe error
