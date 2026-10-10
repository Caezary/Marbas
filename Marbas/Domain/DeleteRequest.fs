namespace Marbas.Domain

type DeleteRequest =
    { Targets: EntryName list
      Scope: DeleteScope
      Mode: DeleteMode }
