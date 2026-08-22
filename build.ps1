Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "              NoSleep - PowerShell Build               " -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

$processes = [System.Diagnostics.Process]::GetProcessesByName("NoSleep")
if ($processes.Count -gt 0) {
    Write-Host "Closing running NoSleep instances..." -ForegroundColor Yellow
    foreach ($p in $processes) {
        try {
            $p.Kill()
            $p.WaitForExit(3000)
        } catch {}
    }
    Start-Sleep -Milliseconds 500
}

$winDir = if ($env:WINDIR) { $env:WINDIR } elseif ($env:SystemRoot) { $env:SystemRoot } else { [Environment]::GetFolderPath([Environment+SpecialFolder]::Windows) }

$candidates = @(
    (Join-Path $winDir "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $winDir "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $null
foreach ($candidate in $candidates) {
    if (Test-Path $candidate) {
        $csc = $candidate
        break
    }
}

if (-not $csc) {
    Write-Error "C# compiler csc.exe was not found in $winDir\Microsoft.NET\Framework"
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = Get-Location }

$icoPath = Join-Path $scriptDir "app.ico"
if (-not (Test-Path $icoPath)) {
    Write-Host "Creating app.ico..." -ForegroundColor Yellow
    $makeIco = Join-Path $scriptDir "make_ico.ps1"
    & $makeIco
}

Write-Host "Compiling NoSleep.exe using $csc..." -ForegroundColor Yellow
$manifestPath = Join-Path $scriptDir "app.manifest"
$outExe = Join-Path $scriptDir "NoSleep.exe"
$srcPattern = Join-Path $scriptDir "src\*.cs"

& $csc /target:winexe /out:$outExe /win32icon:$icoPath /win32manifest:$manifestPath /platform:anycpu /optimize+ /nologo /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll $srcPattern

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCCESS] NoSleep.exe has been built successfully!" -ForegroundColor Green
    $item = Get-Item $outExe
    Write-Host "Binary size: $($item.Length) bytes" -ForegroundColor Gray
} else {
    Write-Host "`n[ERROR] Compilation failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
