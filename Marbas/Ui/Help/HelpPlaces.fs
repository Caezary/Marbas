module Marbas.Ui.Help.HelpPlaces

open Marbas.Guide

type Popup =
    | PatternPopup
    | NamePopup
    | TargetPopup
    | PasswordPopup
    | PackPopup
    | FindPopup
    | LinkPopup
    | OpenWithPopup
    | DrivePopup
    | TabsPopup
    | HistoryPopup
    | KeepFavoritePopup
    | FavoritesPopup
    | UserMenuPopup
    | MenuEntryPopup
    | PropertiesPopup
    | SettingsPopup
    | CodePagePopup
    | ViewerSearchPopup
    | ResultsPopup

let viewer = GuidePlace.viewing None

let ofPopup popup =
    match popup with
    | PatternPopup -> GuidePlace.marking (Some "marking-by-pattern")
    | NamePopup -> GuidePlace.fileOperations (Some "making-directories-and-files")
    | TargetPopup -> GuidePlace.fileOperations (Some "copying-and-moving")
    | PasswordPopup -> GuidePlace.archives (Some "browsing-an-archive")
    | PackPopup -> GuidePlace.archives (Some "packing-and-unpacking")
    | FindPopup -> GuidePlace.finding (Some "what-to-look-for")
    | LinkPopup -> GuidePlace.fileOperations (Some "links")
    | OpenWithPopup -> GuidePlace.running (Some "running-a-file")
    | DrivePopup -> GuidePlace.directories (Some "drives")
    | TabsPopup -> GuidePlace.tabs (Some "the-list-of-tabs")
    | HistoryPopup -> GuidePlace.history (Some "the-history-popup")
    | KeepFavoritePopup -> GuidePlace.directories (Some "favourites")
    | FavoritesPopup -> GuidePlace.directories (Some "the-list-of-favourites")
    | UserMenuPopup -> GuidePlace.running (Some "the-user-menu")
    | MenuEntryPopup -> GuidePlace.running (Some "changing-the-menu")
    | PropertiesPopup -> GuidePlace.fileOperations (Some "properties-of-a-directory")
    | SettingsPopup -> GuidePlace.settings (Some "setup")
    | CodePagePopup -> GuidePlace.viewing (Some "code-pages")
    | ViewerSearchPopup -> GuidePlace.viewing (Some "searching")
    | ResultsPopup -> GuidePlace.finding (Some "the-results-window")
