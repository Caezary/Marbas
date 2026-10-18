module Marbas.Tests.Profile.UserMenuTests

open System
open System.IO.Abstractions.TestingHelpers
open Xunit
open Marbas.Domain
open Marbas.FileSystem
open Marbas.Programs
open Marbas.Profile
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

let private menuPath = fp (at "config/user-menu.conf")

let private key raw = Expect.some (MenuHotkey.tryCreate raw)

let private entry hotkey label line =
    { Hotkey = key hotkey
      Label = Expect.some (MenuLabel.tryCreate label)
      Line = Expect.some (CommandLine.tryCreate line)
      Run = Directly
      Elevation = AsUser }

let private edit = CommandItem(entry "E" "Edit" "vim %p")

let private compare =
    CommandItem
        { entry "C" "Compare" "code --diff %Lp %Rp" with
            Run = InShellKeptOpen }

let private pack =
    CommandItem
        { entry "Z" "Pack" "7z a %d.7z %s" with
            Run = InShellClosing
            Elevation = AsAdministrator }

let private extra = CommandItem(entry "X" "Extra" "notepad %p")

let private menuOf items =
    List.foldBack (fun item menu -> Expect.ok (UserMenu.apply (AddItem(item, None)) menu)) items UserMenu.empty

let private sample = menuOf [ edit; compare; SeparatorItem; pack ]

let private applied change menu =
    UserMenu.apply change menu |> Expect.ok |> UserMenu.items

let private write (fs: MockFileSystem) lines =
    fs.AddFile(FileSystemPath.value menuPath, MockFileData(String.concat "\n" lines))

let private unwritten error =
    match error with
    | UserMenuAccessDenied path -> Some path
    | UserMenuFailed(path, _) -> Some path
    | MenuRefused _ -> None

[<Fact>]
let ``an entry added after another stands right below it`` () =
    let items = applied (AddItem(extra, Some(CommandAt(key "E")))) sample

    Assert.Equal<MenuItem list>([ edit; extra; compare; SeparatorItem; pack ], items)

[<Fact>]
let ``an entry added with nothing to follow goes to the top`` () =
    let items = applied (AddItem(extra, None)) sample

    Assert.Equal<MenuItem list>([ extra; edit; compare; SeparatorItem; pack ], items)

[<Fact>]
let ``an entry added after one another copy removed goes to the end`` () =
    let items = applied (AddItem(extra, Some(CommandAt(key "Q")))) sample

    Assert.Equal<MenuItem list>([ edit; compare; SeparatorItem; pack; extra ], items)

[<Fact>]
let ``a separator is added the way an entry is`` () =
    let items = applied (AddItem(SeparatorItem, Some(CommandAt(key "E")))) sample

    Assert.Equal<MenuItem list>([ edit; SeparatorItem; compare; SeparatorItem; pack ], items)

[<Fact>]
let ``an entry whose hotkey the menu already holds is refused, whatever its case`` () =
    let added =
        UserMenu.apply (AddItem(CommandItem(entry "e" "Elsewhere" "nano %p"), None)) sample

    Assert.Equal(Error(HotkeyTaken(key "E")), added)

[<Fact>]
let ``a changed entry takes the place of the one it was`` () =
    let changed = entry "E" "Edit in nano" "nano %p"

    let items = applied (ChangeCommand(key "E", changed)) sample

    Assert.Equal<MenuItem list>([ CommandItem changed; compare; SeparatorItem; pack ], items)

[<Fact>]
let ``a change may give an entry a hotkey nothing else holds`` () =
    let changed = entry "N" "Edit in nano" "nano %p"

    let items = applied (ChangeCommand(key "E", changed)) sample

    Assert.Equal<MenuItem list>([ CommandItem changed; compare; SeparatorItem; pack ], items)

[<Fact>]
let ``a change to a hotkey another entry holds is refused`` () =
    let changed =
        UserMenu.apply (ChangeCommand(key "E", entry "C" "Edit" "vim %p")) sample

    Assert.Equal(Error(HotkeyTaken(key "C")), changed)

[<Fact>]
let ``a change to an entry another copy removed is refused`` () =
    let changed =
        UserMenu.apply (ChangeCommand(key "Q", entry "Q" "Query" "query %p")) sample

    Assert.Equal(Error(CommandGone(key "Q")), changed)

[<Fact>]
let ``a removed entry is gone and the rest keep their order`` () =
    let items = applied (RemoveItem(CommandAt(key "C"))) sample

    Assert.Equal<MenuItem list>([ edit; SeparatorItem; pack ], items)

[<Fact>]
let ``a removed separator is gone`` () =
    let separator = Expect.some (UserMenu.refAt 2 sample)

    let items = applied (RemoveItem separator) sample

    Assert.Equal<MenuItem list>([ edit; compare; pack ], items)

[<Fact>]
let ``removing an entry that is already gone changes nothing`` () =
    let items = applied (RemoveItem(CommandAt(key "Q"))) sample

    Assert.Equal<MenuItem list>(UserMenu.items sample, items)

[<Fact>]
let ``an entry moved up changes places with the one above it`` () =
    let items = applied (MoveItem(CommandAt(key "C"), Earlier)) sample

    Assert.Equal<MenuItem list>([ compare; edit; SeparatorItem; pack ], items)

[<Fact>]
let ``an entry moved down changes places with the one below it`` () =
    let items = applied (MoveItem(CommandAt(key "C"), Later)) sample

    Assert.Equal<MenuItem list>([ edit; SeparatorItem; compare; pack ], items)

[<Fact>]
let ``the first entry moved up stays where it is`` () =
    let items = applied (MoveItem(CommandAt(key "E"), Earlier)) sample

    Assert.Equal<MenuItem list>(UserMenu.items sample, items)

[<Fact>]
let ``the last entry moved down stays where it is`` () =
    let items = applied (MoveItem(CommandAt(key "Z"), Later)) sample

    Assert.Equal<MenuItem list>(UserMenu.items sample, items)

[<Fact>]
let ``of two separators in a row the lower one moves on its own`` () =
    let menu = menuOf [ edit; SeparatorItem; SeparatorItem; pack ]
    let lower = Expect.some (UserMenu.refAt 2 menu)

    let items = applied (MoveItem(lower, Later)) menu

    Assert.Equal<MenuItem list>([ edit; SeparatorItem; pack; SeparatorItem ], items)

[<Fact>]
let ``an entry is found by its hotkey`` () =
    let found = UserMenu.refAt 1 sample

    Assert.Equal(Some(CommandAt(key "C")), found)

[<Fact>]
let ``a separator is found by the entry above it`` () =
    let found = UserMenu.refAt 2 sample

    Assert.Equal(Some(SeparatorAfter(Some(key "C"), 0)), found)

[<Fact>]
let ``a separator in a run is found by the entry above it and the separators between`` () =
    let menu = menuOf [ SeparatorItem; SeparatorItem; edit ]

    let found = UserMenu.refAt 1 menu

    Assert.Equal(Some(SeparatorAfter(None, 1)), found)

[<Theory>]
[<InlineData(-1)>]
[<InlineData(4)>]
let ``a place outside the menu finds nothing`` (index: int) =
    let found = UserMenu.refAt index sample

    Assert.Equal(None, found)

[<Fact>]
let ``what is formatted is what is parsed back`` () =
    let parsed = UserMenu.parse (UserMenu.format sample)

    Assert.Equal(sample, parsed)

[<Fact>]
let ``each entry is written under its number, in the order of the menu`` () =
    let menu =
        menuOf
            [ CommandItem
                  { entry "E" "Edit" "vim %p" with
                      Run = InShellClosing }
              SeparatorItem ]

    let lines = UserMenu.format menu

    Assert.Equal<string list>(
        [ "entry.1=command"
          "entry.1.hotkey=E"
          "entry.1.label=Edit"
          "entry.1.command=vim %p"
          "entry.1.run=shell-close"
          "entry.1.elevation=user"
          "entry.2=separator" ],
        lines
    )

[<Fact>]
let ``nothing to read is an empty menu`` () =
    let parsed = UserMenu.parse []

    Assert.Empty(UserMenu.items parsed)

[<Theory>]
[<InlineData("", "")>]
[<InlineData("entry.1.run=sideways", "entry.1.elevation=root")>]
let ``a way of running or an elevation missing or unreadable falls back to a direct start as the user``
    (run: string, elevation: string)
    =
    let lines =
        [ "entry.1=command"
          "entry.1.hotkey=E"
          "entry.1.label=Edit"
          "entry.1.command=vim %p"
          run
          elevation ]

    let parsed = UserMenu.parse lines

    Assert.Equal<MenuItem list>([ edit ], UserMenu.items parsed)

[<Theory>]
[<InlineData("", "Edit", "vim %p")>]
[<InlineData("EE", "Edit", "vim %p")>]
[<InlineData("E", "", "vim %p")>]
[<InlineData("E", "Edit", "")>]
let ``an entry without a hotkey, a label or a command costs that entry alone``
    (hotkey: string, label: string, command: string)
    =
    let lines =
        [ "entry.1=command"
          $"entry.1.hotkey={hotkey}"
          $"entry.1.label={label}"
          $"entry.1.command={command}"
          "entry.2=command"
          "entry.2.hotkey=X"
          "entry.2.label=Extra"
          "entry.2.command=notepad %p" ]

    let parsed = UserMenu.parse lines

    Assert.Equal<MenuItem list>([ extra ], UserMenu.items parsed)

[<Fact>]
let ``an entry of a kind not known costs that entry alone`` () =
    let lines = [ "entry.1=folder"; "entry.2=separator" ]

    let parsed = UserMenu.parse lines

    Assert.Equal<MenuItem list>([ SeparatorItem ], UserMenu.items parsed)

[<Fact>]
let ``of two entries holding one hotkey the first is kept`` () =
    let lines =
        [ "entry.1=command"
          "entry.1.hotkey=e"
          "entry.1.label=Edit"
          "entry.1.command=vim %p"
          "entry.2=command"
          "entry.2.hotkey=E"
          "entry.2.label=Elsewhere"
          "entry.2.command=nano %p" ]

    let parsed = UserMenu.parse lines

    Assert.Equal<MenuItem list>([ edit ], UserMenu.items parsed)

[<Fact>]
let ``entries are read in the order of their numbers, gaps and all`` () =
    let lines =
        [ "entry.10=command"
          "entry.10.hotkey=X"
          "entry.10.label=Extra"
          "entry.10.command=notepad %p"
          "entry.2=separator" ]

    let parsed = UserMenu.parse lines

    Assert.Equal<MenuItem list>([ SeparatorItem; extra ], UserMenu.items parsed)

[<Fact>]
let ``the user menu sits beside the settings, in a file of its own`` () =
    let fs = MockFileSystem()

    let path = UserMenu.beside fs (fp (at "config/marbas.conf"))

    Assert.Equal(menuPath, path)

[<Fact>]
let ``a menu never written loads as an empty one`` () =
    let fs = MockFileSystem()

    let loaded = UserMenu.load fs menuPath

    Assert.Equal(Ok UserMenu.empty, loaded)

[<Fact>]
let ``a change made on top of a file another copy wrote keeps both`` () =
    let fs = MockFileSystem()
    write fs (UserMenu.format sample)

    let changed = UserMenu.change fs menuPath (AddItem(extra, Some(CommandAt(key "E"))))

    let expected = menuOf [ edit; extra; compare; SeparatorItem; pack ]
    Assert.Equal(Ok expected, changed)
    Assert.Equal(Ok expected, UserMenu.load fs menuPath)

[<Fact>]
let ``a separator removed after another copy added an entry above it is still the one that goes`` () =
    let fs = MockFileSystem()
    let separator = Expect.some (UserMenu.refAt 2 sample)
    write fs (UserMenu.format (menuOf [ extra; edit; compare; SeparatorItem; pack ]))

    let changed = UserMenu.change fs menuPath (RemoveItem separator)

    Assert.Equal(Ok(menuOf [ extra; edit; compare; pack ]), changed)

[<Fact>]
let ``a refused change leaves the file as it was`` () =
    let fs = MockFileSystem()
    write fs (UserMenu.format sample)

    let changed =
        UserMenu.change fs menuPath (AddItem(CommandItem(entry "Z" "Zap" "zap %p"), None))

    let kept = fs.GetFile(FileSystemPath.value menuPath).TextContents
    Assert.Equal(Error(MenuRefused(HotkeyTaken(key "Z"))), changed)
    Assert.Equal(String.concat "\n" (UserMenu.format sample), kept)

[<Fact>]
let ``the first change creates the file and the directory it lives in`` () =
    let fs = MockFileSystem()

    let changed = UserMenu.change fs menuPath (AddItem(edit, None))

    let loaded = UserMenu.load fs menuPath
    Assert.Equal(changed, loaded)
    Assert.Equal<MenuItem list>([ edit ], UserMenu.items (Expect.ok loaded))

[<Fact>]
let ``a menu that cannot be written answers an error naming the file`` () =
    let fs = MockFileSystem()
    fs.AddFile(at "blocked", MockFileData "x")
    let blocked = fp (at "blocked/user-menu.conf")

    let changed = UserMenu.change fs blocked (AddItem(edit, None))

    Assert.Equal(Some blocked, unwritten (Expect.error changed))

[<Fact>]
let ``a menu that cannot be read is answered as an error and left as it was`` () =
    let fs = Fakes.UnreadableFile(FileSystemPath.value menuPath)
    write fs (UserMenu.format sample)

    let changed = UserMenu.change fs menuPath (AddItem(extra, None))

    let kept = fs.GetFile(FileSystemPath.value menuPath).TextContents
    Assert.Equal(Some menuPath, unwritten (Expect.error changed))
    Assert.Equal(String.concat "\n" (UserMenu.format sample), kept)
