namespace Marbas.Ui.Viewer

open Marbas.FileSystem

type CodePage =
    | Automatic
    | Fixed of Charset

type CodePageOffer =
    { Detected: TextEncoding
      Current: CodePage }

module CodePage =

    let offered = Automatic :: List.map Fixed TextEncoding.charsets

    let label (detected: TextEncoding) page =
        match page with
        | Automatic -> $"Auto ({TextEncoding.name detected})"
        | Fixed charset -> TextEncoding.charsetName charset

    let encoding (detected: TextEncoding) (probe: byte array) page =
        match page with
        | Automatic -> detected
        | Fixed charset -> TextEncoding.chosen charset probe
