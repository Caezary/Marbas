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

let millisecondsPuttingText (text: string) =
    run
        $"$clock = [Diagnostics.Stopwatch]::StartNew(); \
          [Windows.Forms.Clipboard]::SetText({quoted text}); \
          [Console]::Out.Write($clock.ElapsedMilliseconds)"
    |> Int64.Parse

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

let private user32 =
    "Add-Type -Namespace Held -Name User32 -MemberDefinition \
     '[DllImport(\"user32.dll\", SetLastError = true)] public static extern bool OpenClipboard(IntPtr owner); \
      [DllImport(\"user32.dll\")] public static extern bool CloseClipboard(); \
      [DllImport(\"user32.dll\")] public static extern IntPtr GetClipboardOwner(); \
      [DllImport(\"user32.dll\")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process); \
      public static int Refusal(IntPtr owner) \
      { if (!OpenClipboard(owner)) return Marshal.GetLastWin32Error(); CloseClipboard(); return 0; } \
      public static uint OwnerProcess() \
      { uint process; GetWindowThreadProcessId(GetClipboardOwner(), out process); return process; }'; \
     $window = New-Object Windows.Forms.Form; "

let private holding (owner: string) =
    let program =
        started (
            user32
            + $"[void][Held.User32]::OpenClipboard({owner}); \
                [Console]::Out.WriteLine('held'); \
                [void][Console]::In.ReadLine(); \
                [void][Held.User32]::CloseClipboard()"
        )

    program.StandardOutput.ReadLine() |> ignore
    new HeldClipboard(program)

let holdingClipboard () = holding "$window.Handle"

let holdingClipboardWithNoWindow () = holding "[IntPtr]::Zero"

let private refusalOrNone (code: string) =
    match Int32.Parse code with
    | 0 -> Ok()
    | refusal -> Error refusal

let private openingFor (owner: string) =
    run (user32 + $"[Console]::Out.Write([Held.User32]::Refusal({owner}))")
    |> refusalOrNone

let opening () = openingFor "$window.Handle"

let openingWithNoWindow () = openingFor "[IntPtr]::Zero"

let ownerProcess () =
    run (user32 + "[Console]::Out.Write([Held.User32]::OwnerProcess())")
    |> Int32.Parse
