param([string]$Root = (Join-Path $HOME 'marbas-manual'))

$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $Root) {
    throw "'$Root' is already there. Name another directory, or remove this one first."
}

$stamp = [datetime]::new(2001, 2, 3, 4, 5, 6)
$skipped = [System.Collections.Generic.List[string]]::new()

$sevenZip =
    @('7z', (Join-Path $env:ProgramFiles '7-Zip\7z.exe')) |
    Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } |
    Select-Object -First 1

function New-Directory([string]$relative) {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root $relative) | Out-Null
}

function New-TextFile([string]$relative, [string]$text) {
    $path = Join-Path $Root $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    [System.IO.File]::WriteAllText($path, $text)
}

function New-Tree([string]$relative) {
    New-TextFile "$relative\a.txt" "first`r`n"
    New-TextFile "$relative\sub\b.txt" "second`r`n"
    $top = Get-Item -LiteralPath (Join-Path $Root $relative)
    @(Get-ChildItem -LiteralPath $top.FullName -Recurse -Force) + $top |
        ForEach-Object { $_.LastWriteTime = $stamp }
}

function Invoke-Tool([string]$tool, [string[]]$arguments) {
    & $tool @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$tool $arguments ended with $LASTEXITCODE." }
}

New-Directory 'kinds\folder'
New-TextFile 'kinds\plain.txt' "an ordinary file`r`n"
New-TextFile 'kinds\.dotfile' "hidden by its name`r`n"
New-TextFile 'kinds\hidden.txt' "hidden by its attribute`r`n"
(Get-Item -LiteralPath (Join-Path $Root 'kinds\hidden.txt')).Attributes = 'Hidden'
Copy-Item (Join-Path $env:SystemRoot 'System32\whoami.exe') (Join-Path $Root 'kinds\program.exe')
Compress-Archive -LiteralPath (Join-Path $Root 'kinds\plain.txt') -DestinationPath (Join-Path $Root 'kinds\archive.zip')
New-Item -ItemType Junction -Path (Join-Path $Root 'kinds\junction') -Target (Join-Path $Root 'kinds\folder') | Out-Null

try {
    New-Item -ItemType SymbolicLink -Path (Join-Path $Root 'kinds\symlink') -Target (Join-Path $Root 'kinds\folder') | Out-Null
}
catch {
    $skipped.Add('kinds\symlink: Windows makes a symbolic link only for an administrator or with Developer Mode on; the junction shows the link colour instead.')
}

1..120 | ForEach-Object { New-TextFile ('many\entry-{0:d3}.txt' -f $_) "entry $_`r`n" }
New-TextFile 'many\zebra.txt' "the one name that starts with z`r`n"

New-Tree 'pack\tree'
New-Directory 'out'

New-Tree 'foreign\src\from-tar'
New-Tree 'foreign\src\from-7zip-zip'
New-Tree 'foreign\src\from-7zip-7z'

Push-Location (Join-Path $Root 'foreign\src')

try {
    Invoke-Tool 'tar' @('-czf', '..\by-tar.tar.gz', 'from-tar')

    if ($sevenZip) {
        Invoke-Tool $sevenZip @('a', '-tzip', '..\by-7zip.zip', 'from-7zip-zip')
        Invoke-Tool $sevenZip @('a', '..\by-7zip.7z', 'from-7zip-7z')
    }
    else {
        $skipped.Add('foreign\by-7zip.zip and by-7zip.7z: 7-Zip was not found; only the archive made by tar is there.')
    }
}
finally {
    Pop-Location
}

New-TextFile 'trash\to-trash.txt' "sent to the recycle bin and put back`r`n"
New-TextFile 'trash\to-trash-dir\inner.txt' "travels with its directory`r`n"

New-Directory 'attrs\flags-dir'

New-TextFile 'edit\edit-me.log' "one line`r`n"

New-TextFile 'launch\document.txt' "a document the system knows a program for`r`n"
New-TextFile 'launch\unknown.zzqx' "an extension the system knows nothing about`r`n"
New-TextFile 'launch\waits.cmd' "@echo off`r`necho This window belongs to waits.cmd. The panels behind it should still take keys.`r`npause`r`n"
New-TextFile 'launch\elevated.cmd' "@echo off`r`nwhoami /groups | find `"Mandatory Label`"`r`necho started in %cd%`r`npause`r`n"

"The fixture is in $Root"

if ($skipped.Count -gt 0) {
    'Left out:'
    $skipped | ForEach-Object { "  $_" }
}
