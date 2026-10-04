module Marbas.Tests.Integration.Clipboard.OtherProgram

open System
open System.Diagnostics
open System.Text

let private prelude =
    "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'; "
    + "[Console]::OutputEncoding = [Text.Encoding]::UTF8; Add-Type -AssemblyName System.Windows.Forms; "

let private encoded (script: string) =
    Encoding.Unicode.GetBytes(prelude + script) |> Convert.ToBase64String

let private started (script: string) =
    let info =
        ProcessStartInfo(
            "powershell.exe",
            $"-NoProfile -NonInteractive -STA -EncodedCommand {encoded script}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        )

    Process.Start info

let private run (script: string) =
    use program = started script
    let output = program.StandardOutput.ReadToEndAsync()
    let errors = program.StandardError.ReadToEnd()
    program.WaitForExit()

    match program.ExitCode with
    | 0 -> output.Result
    | code -> failwith $"The other program failed with {code}: {errors}"

let private quoted (text: string) = "'" + text.Replace("'", "''") + "'"

let private listed (paths: string list) =
    paths |> List.map quoted |> String.concat ", "

let private lines (output: string) =
    output.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

let text () =
    run "[Console]::Out.Write([Windows.Forms.Clipboard]::GetText())"

let putText (text: string) =
    run $"[Windows.Forms.Clipboard]::SetText({quoted text})" |> ignore

let files () =
    run "[Console]::Out.Write(([Windows.Forms.Clipboard]::GetFileDropList() -join \"`n\"))"
    |> lines

let dropEffect () =
    run
        "$held = [Windows.Forms.Clipboard]::GetData('Preferred DropEffect'); \
         if ($held) { [Console]::Out.Write((New-Object IO.BinaryReader $held).ReadUInt32()) }"
    |> lines
    |> List.tryExactlyOne
    |> Option.map UInt32.Parse

let private droppedFiles (paths: string list) =
    $"$files = New-Object Collections.Specialized.StringCollection; \
      foreach ($path in @({listed paths})) {{ [void]$files.Add($path) }}; \
      $offered = New-Object Windows.Forms.DataObject; \
      $offered.SetFileDropList($files); "

let putFiles (paths: string list) =
    run (droppedFiles paths + "[Windows.Forms.Clipboard]::SetDataObject($offered, $true)")
    |> ignore

let putFilesWithEffect (paths: string list) (effect: uint32) =
    run (
        droppedFiles paths
        + $"$effect = [IO.MemoryStream]::new([BitConverter]::GetBytes([uint32]{effect})); \
            $offered.SetData('Preferred DropEffect', $effect); \
            [Windows.Forms.Clipboard]::SetDataObject($offered, $true)"
    )
    |> ignore

let pasteInto (directory: string) (awaited: string) =
    run
        $"(New-Object -ComObject Shell.Application).NameSpace({quoted directory}).Self.InvokeVerb('paste'); \
          $clock = [Diagnostics.Stopwatch]::StartNew(); \
          while (-not (Test-Path -LiteralPath {quoted awaited}) -and $clock.ElapsedMilliseconds -lt 10000) \
          {{ Start-Sleep -Milliseconds 50 }}"
    |> ignore

type HeldClipboard(program: Process) =
    interface IDisposable with
        member _.Dispose() =
            program.StandardInput.WriteLine()
            program.WaitForExit()
            program.Dispose()

let holdingClipboard () =
    let program =
        started
            "Add-Type -Namespace Held -Name User32 -MemberDefinition \
             '[DllImport(\"user32.dll\")] public static extern bool OpenClipboard(IntPtr owner); \
              [DllImport(\"user32.dll\")] public static extern bool CloseClipboard();'; \
             $window = New-Object Windows.Forms.Form; \
             [void][Held.User32]::OpenClipboard($window.Handle); \
             [Console]::Out.WriteLine('held'); \
             [void][Console]::In.ReadLine(); \
             [void][Held.User32]::CloseClipboard()"

    program.StandardOutput.ReadLine() |> ignore
    new HeldClipboard(program)
