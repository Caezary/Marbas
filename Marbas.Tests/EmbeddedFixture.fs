module Marbas.Tests.EmbeddedFixture

open System.IO
open System.Reflection

let bytes (name: string) =
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream name
    use buffer = new MemoryStream()
    stream.CopyTo buffer
    buffer.ToArray()
