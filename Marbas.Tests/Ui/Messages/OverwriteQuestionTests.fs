module Marbas.Tests.Ui.Messages.OverwriteQuestionTests

open Xunit
open Marbas.FileSystem
open Marbas.FileSystem.Operations
open Marbas.Ui.Messages
open Marbas.Tests

let private at = Root.at

let private fp = FileSystemPath.create

[<Fact>]
let ``the question names the entry that would be replaced`` () =
    let message = OverwriteQuestion.describe (fp (at "backup/notes.txt"))

    Assert.Contains(at "backup/notes.txt", message)
    Assert.Contains("already exists", message)

[<Fact>]
let ``every offered answer maps to its own outcome`` () =
    let answers =
        OverwriteQuestion.Answers
        |> List.mapi (fun index _ -> OverwriteQuestion.answerAt index)

    Assert.Equal<OverwriteAnswer list>([ Yes; No; YesToAll; NoToAll; Cancel ], answers)

[<Fact>]
let ``a dismissed question counts as cancel`` () =
    let answer = OverwriteQuestion.answerAt -1

    Assert.Equal(Cancel, answer)
