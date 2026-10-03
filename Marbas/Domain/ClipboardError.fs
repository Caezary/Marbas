namespace Marbas.Domain

type ClipboardError =
    | ClipboardUnavailable
    | ClipboardFailed of reason: string
