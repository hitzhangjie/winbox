#Requires -Version 5.1
<#
.SYNOPSIS
  Publish WinBox Host as a self-contained win-x64 package and zip it.

.DESCRIPTION
  Supported runtime target: Windows 11 amd64 (RID win-x64).
  Dev builds keep UseAppHost=false; dist forces a native WinBox.Host.exe apphost.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$RepoRoot = ""
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

    # Prefer the tag name when packaging during release.published (ref is the tag).
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

Write-Host "WinBox dist"
Write-Host "  version : $packageVersion (assembly $fourPartVersion)"
Write-Host "  runtime : $Runtime (Windows 11 amd64)"
Write-Host "  config  : $Configuration"
Write-Host "  publish : $publishDir"
Write-Host "  package : $zipPath"

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

# Drop runtime diagnostic helper; not needed for end-user portable runs.
$createDump = Join-Path $publishDir "createdump.exe"
if (Test-Path -LiteralPath $createDump) {
    Remove-Item -LiteralPath $createDump -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# Compress publish folder contents (not the folder itself) for a flat unzip experience.
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$zipItem = Get-Item -LiteralPath $zipPath
Write-Host "Created $($zipItem.FullName) ($([math]::Round($zipItem.Length / 1MB, 2)) MB)"
Write-Host "DIST_ZIP=$($zipItem.FullName)"
