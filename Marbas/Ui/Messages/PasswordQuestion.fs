module Marbas.Ui.Messages.PasswordQuestion

open Marbas.FileSystem

[<Literal>]
let Title = "Password"

let describe (archive: EntryName) = $"Password for '{EntryName.value archive}':"
