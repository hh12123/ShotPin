<#
.SYNOPSIS
    Build ShotPin with the C# compiler that ships with .NET Framework 4.8.
.DESCRIPTION
    Produces dist\ShotPin.exe (+ .config). With -Test or -CoreOnly it also
    compiles and runs the pure-logic test executable.
#>
[CmdletBinding()]
param(
    [switch]$Test,
    [switch]$CoreOnly
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }

$framework64 = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$framework32 = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319'
$fw = $framework64
if (-not (Test-Path (Join-Path $fw 'csc.exe'))) { $fw = $framework32 }
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw 'Cannot find csc.exe in the .NET Framework directory.' }

$buildDir = Join-Path $root 'build'
$distDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $buildDir, $distDir | Out-Null

$iconPath = Join-Path $buildDir 'app.ico'
if (-not (Test-Path $iconPath)) {
    $iconScript = Join-Path $root 'tools\make-icon.ps1'
    if (Test-Path $iconScript) {
        try {
            & $iconScript -Out $iconPath
        }
        catch {
            Write-Warning ("Icon generation failed: " + $_.Exception.Message)
        }
    }
}

function Invoke-Csc {
    param([string[]]$Arguments, [string]$Description)
    Write-Host ("==> " + $Description)
    & $csc @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ("csc failed with exit code " + $LASTEXITCODE + " while " + $Description)
    }
}

$commonRefs = @(
    ('/r:' + $fw + '\System.dll'),
    ('/r:' + $fw + '\System.Core.dll'),
    ('/r:' + $fw + '\System.Drawing.dll')
)

if (-not $CoreOnly) {
    $allSources = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs |
        ForEach-Object { $_.FullName } | Sort-Object -Unique)
    if ($allSources.Count -eq 0) {
        throw 'No application sources found under src\.'
    }

    $appArgs = @(
        '/nologo', '/noconfig', '/target:winexe', '/platform:anycpu', '/optimize+',
        '/codepage:65001',
        ('/out:' + (Join-Path $distDir 'ShotPin.exe')),
        ('/win32manifest:' + (Join-Path $root 'src\app.manifest'))
    )
    if (Test-Path $iconPath) {
        $appArgs += ('/win32icon:' + $iconPath)
    }
    $appArgs += $commonRefs
    $appArgs += ('/r:' + $fw + '\System.Windows.Forms.dll')
    $appArgs += $allSources

    Invoke-Csc -Arguments $appArgs -Description 'compiling dist\ShotPin.exe'

    $configSource = Join-Path $root 'src\app.config'
    if (Test-Path $configSource) {
        Copy-Item $configSource (Join-Path $distDir 'ShotPin.exe.config') -Force
    }
    Write-Host ('Built: ' + (Join-Path $distDir 'ShotPin.exe'))
}

if ($Test -or $CoreOnly) {
    $testSources = @(Get-ChildItem (Join-Path $root 'src\Core') -Filter *.cs | ForEach-Object { $_.FullName })
    $testSources += (Join-Path $root 'tests\CoreTests.cs')
    $testExe = Join-Path $buildDir 'CoreTests.exe'

    $testArgs = @(
        '/nologo', '/noconfig', '/target:exe', '/platform:anycpu', '/optimize+',
        '/codepage:65001',
        ('/out:' + $testExe)
    )
    $testArgs += $commonRefs
    $testArgs += $testSources

    Invoke-Csc -Arguments $testArgs -Description 'compiling build\CoreTests.exe'
    Write-Host '==> running core tests'
    & $testExe
    if ($LASTEXITCODE -ne 0) {
        throw ('Core tests failed with exit code ' + $LASTEXITCODE)
    }
    Write-Host 'Core tests passed.'
}

Write-Host 'Done.'
