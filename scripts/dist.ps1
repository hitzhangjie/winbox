#Requires -Version 5.1
<#
.SYNOPSIS
  Publish WinBox Host and produce portable zip + Windows Setup installer.

.DESCRIPTION
  Supported runtime target: Windows 11 amd64 (RID win-x64).
  Artifacts:
    - WinBox-<ver>-win-x64.zip          (portable / no installer)
    - WinBox-<ver>-win-x64-setup.exe    (Inno Setup → Program Files)
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$RepoRoot = "",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    param([string]$Hint)
    if ($Hint) {
        return (Resolve-Path -LiteralPath $Hint).Path
    }
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Get-NormalizedVersion {
    param([string]$Raw)
    $v = $Raw.Trim()
    if ($v -match '^v(.+)$') {
        $v = $Matches[1]
    }
    return $v
}

function Get-FourPartVersion {
    param([string]$PackageVersion)
    # AssemblyVersion/FileVersion require numeric x.y.z.w (no semver prerelease suffix).
    $core = ($PackageVersion -split '-', 2)[0]
    $parts = @($core -split '\.')
    while ($parts.Count -lt 4) {
        $parts += "0"
    }
    return ($parts[0..3] -join '.')
}

function Resolve-PackageVersion {
    param([string]$Requested, [string]$Root)

    if (-not [string]::IsNullOrWhiteSpace($Requested)) {
        return (Get-NormalizedVersion -Raw $Requested)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:WINBOX_VERSION)) {
        return (Get-NormalizedVersion -Raw $env:WINBOX_VERSION)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_TYPE) -and
        $env:GITHUB_REF_TYPE -eq "tag" -and
        -not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
        return (Get-NormalizedVersion -Raw $env:GITHUB_REF_NAME)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_EVENT_NAME) -and
        $env:GITHUB_EVENT_NAME -eq "release" -and
        -not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
        return (Get-NormalizedVersion -Raw $env:GITHUB_REF_NAME)
    }

    $propsPath = Join-Path $Root "Directory.Build.props"
    if (Test-Path -LiteralPath $propsPath) {
        [xml]$props = Get-Content -LiteralPath $propsPath -Raw
        $fromProps = $props.Project.PropertyGroup.Version | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($fromProps)) {
            return $fromProps.Trim()
        }
    }

    return "0.0.0-dev"
}

function Find-Iscc {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:WINBOX_ISCC)) {
        $candidates += $env:WINBOX_ISCC
    }
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $candidates += $cmd.Source
    }
    $candidates += @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )

    foreach ($path in $candidates) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            return (Resolve-Path -LiteralPath $path).Path
        }
    }
    return $null
}

function Install-InnoSetupLocal {
    $targetDir = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6"
    $isccPath = Join-Path $targetDir "ISCC.exe"
    if (Test-Path -LiteralPath $isccPath) {
        return $isccPath
    }

    # Prefer winget when available (same source CI/docs recommend for interactive installs).
    $winget = Get-Command "winget.exe" -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "Installing Inno Setup 6 via winget ..."
        & winget.exe install --id JRSoftware.InnoSetup -e --source winget `
            --accept-package-agreements --accept-source-agreements --disable-interactivity
        $found = Find-Iscc
        if ($found) {
            return $found
        }
    }

    Write-Host "Bootstrapping Inno Setup 6 to $targetDir ..."
    $bootstrap = Join-Path $env:TEMP "winbox-innosetup-6.7.3.exe"
    # Pinned GitHub release asset (jrsoftware.org/download.php redirects to an HTML page).
    $uri = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe"
    Write-Host "Downloading $uri"
    Invoke-WebRequest -Uri $uri -OutFile $bootstrap -UseBasicParsing

    $item = Get-Item -LiteralPath $bootstrap
    if ($item.Length -lt 1MB) {
        throw "Downloaded Inno Setup installer looks too small ($($item.Length) bytes): $bootstrap"
    }

    $args = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/DIR=`"$targetDir`""
    )
    $proc = Start-Process -FilePath $bootstrap -ArgumentList $args -Wait -PassThru
    if ($proc.ExitCode -ne 0) {
        throw "Inno Setup bootstrap failed with exit code $($proc.ExitCode)"
    }

    $found = Find-Iscc
    if ($found) {
        return $found
    }
    if (-not (Test-Path -LiteralPath $isccPath)) {
        throw "ISCC.exe missing after bootstrap. Install Inno Setup 6 manually, or set WINBOX_ISCC."
    }
    return $isccPath
}

function Ensure-Iscc {
    $found = Find-Iscc
    if ($found) {
        return $found
    }
    return (Install-InnoSetupLocal)
}

$root = Resolve-RepoRoot -Hint $RepoRoot
Set-Location -LiteralPath $root

$hostProject = Join-Path $root "src\WinBox.Host\WinBox.Host.csproj"
if (-not (Test-Path -LiteralPath $hostProject)) {
    throw "Host project not found: $hostProject"
}

$packageVersion = Resolve-PackageVersion -Requested $Version -Root $root
$fourPartVersion = Get-FourPartVersion -PackageVersion $packageVersion
$publishDir = Join-Path $root "artifacts\publish\$Runtime"
$distDir = Join-Path $root "artifacts\dist"
$zipName = "WinBox-$packageVersion-$Runtime.zip"
$zipPath = Join-Path $distDir $zipName
$setupName = "WinBox-$packageVersion-$Runtime-setup.exe"
$setupPath = Join-Path $distDir $setupName
$issPath = Join-Path $root "packaging\winbox.iss"
$iconPath = Join-Path $root "src\WinBox.Host\Assets\winbox.ico"

Write-Host "WinBox dist"
Write-Host "  version : $packageVersion (assembly $fourPartVersion)"
Write-Host "  runtime : $Runtime (Windows 11 amd64)"
Write-Host "  config  : $Configuration"
Write-Host "  publish : $publishDir"
Write-Host "  portable: $zipPath"
if (-not $SkipInstaller) {
    Write-Host "  setup   : $setupPath"
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

$publishArgs = @(
    "publish", $hostProject,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:UseAppHost=true",
    "-p:Version=$packageVersion",
    "-p:AssemblyVersion=$fourPartVersion",
    "-p:FileVersion=$fourPartVersion",
    "-o", $publishDir
)

Write-Host "dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $publishDir "WinBox.Host.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Expected apphost missing after publish: $exePath"
}

# Drop runtime diagnostic helper; not needed for end-user packages.
$createDump = Join-Path $publishDir "createdump.exe"
if (Test-Path -LiteralPath $createDump) {
    Remove-Item -LiteralPath $createDump -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
$zipItem = Get-Item -LiteralPath $zipPath
Write-Host "Created $($zipItem.FullName) ($([math]::Round($zipItem.Length / 1MB, 2)) MB)"
Write-Host "DIST_ZIP=$($zipItem.FullName)"

if ($SkipInstaller) {
    Write-Host "SkipInstaller set; not building Setup.exe"
    return
}

if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Inno Setup script missing: $issPath"
}
if (-not (Test-Path -LiteralPath $iconPath)) {
    throw "Setup icon missing: $iconPath"
}

$iscc = Ensure-Iscc
Write-Host "Using ISCC: $iscc"

if (Test-Path -LiteralPath $setupPath) {
    Remove-Item -LiteralPath $setupPath -Force
}

# Absolute paths for ISCC /D defines (spaces-safe).
$isccArgs = @(
    "/DMyAppVersion=$packageVersion",
    "/DMyAppRuntime=$Runtime",
    "/DSourceDir=$publishDir",
    "/DOutputDir=$distDir",
    "/DSetupIcon=$iconPath",
    $issPath
)

Write-Host "ISCC $($isccArgs -join ' ')"
& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Expected setup missing after ISCC: $setupPath"
}

$setupItem = Get-Item -LiteralPath $setupPath
Write-Host "Created $($setupItem.FullName) ($([math]::Round($setupItem.Length / 1MB, 2)) MB)"
Write-Host "DIST_SETUP=$($setupItem.FullName)"
