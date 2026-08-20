<#
.SYNOPSIS
    Install VaMMCP (and BepInEx, if missing) into a Virt-A-Mate folder.

.DESCRIPTION
    Downloads the prebuilt VaMMCP.dll from GitHub Releases and drops it into
    <VaM>\BepInEx\plugins\. Installs BepInEx 5.4.21 x64 first if the folder does not
    have it yet; existing files are never overwritten by the BepInEx step.

.PARAMETER VamRoot
    The VaM folder (the one containing VaM.exe). Defaults to $env:VAM_ROOT, then to
    the current directory, then to two levels above this script.

.PARAMETER Tag
    Release tag to install, e.g. v1.0.0. Defaults to the latest release.

.PARAMETER SkipBepInEx
    Do not touch BepInEx even if it looks missing.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\install.ps1
    powershell -ExecutionPolicy Bypass -File scripts\install.ps1 -VamRoot "D:\VaM" -Tag v1.0.0
#>
[CmdletBinding()]
param(
    [string]$VamRoot = "",
    [string]$Tag = "latest",
    [switch]$SkipBepInEx
)

$ErrorActionPreference = "Stop"
# Old Windows PowerShell defaults to TLS 1.0, which github.com refuses.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$BepInExVersion = "5.4.21"
$Repo = "Rorical/VaMMCP"

function Resolve-VamRoot {
    param([string]$Candidate)
    $tries = @()
    if ($Candidate) { $tries += $Candidate }
    if ($env:VAM_ROOT) { $tries += $env:VAM_ROOT }
    $tries += (Get-Location).Path
    $tries += (Join-Path $PSScriptRoot "..\..")
    foreach ($t in $tries) {
        if ($t -and (Test-Path (Join-Path $t "VaM.exe"))) {
            return (Resolve-Path $t).Path
        }
    }
    throw "Could not find VaM.exe. Run this from your VaM folder or pass -VamRoot 'D:\path\to\VaM'."
}

function Install-BepInEx {
    param([string]$Root)
    if (Test-Path (Join-Path $Root "winhttp.dll")) {
        Write-Host "BepInEx already installed."
        return
    }
    $url = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_x64_$BepInExVersion.0.zip"
    $zip = Join-Path $env:TEMP "BepInEx_x64_$BepInExVersion.zip"
    $stage = Join-Path $env:TEMP "vammcp-bepinex-$([guid]::NewGuid().ToString('N'))"
    Write-Host "Downloading BepInEx $BepInExVersion ..."
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    try {
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $stage)
        # Additive copy: never clobber something the user already has.
        Get-ChildItem -Path $stage -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($stage.Length).TrimStart('\')
            $dest = Join-Path $Root $rel
            if (Test-Path $dest) {
                Write-Host "  skip (exists): $rel"
            } else {
                New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
                Copy-Item $_.FullName $dest
            }
        }
        Write-Host "BepInEx $BepInExVersion installed."
    } finally {
        Remove-Item $zip -Force -ErrorAction SilentlyContinue
        Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Install-Plugin {
    param([string]$Root, [string]$ReleaseTag)
    $plugins = Join-Path $Root "BepInEx\plugins"
    New-Item -ItemType Directory -Path $plugins -Force | Out-Null
    if ($ReleaseTag -eq "latest") {
        $url = "https://github.com/$Repo/releases/latest/download/VaMMCP.dll"
    } else {
        $url = "https://github.com/$Repo/releases/download/$ReleaseTag/VaMMCP.dll"
    }
    $dest = Join-Path $plugins "VaMMCP.dll"
    Write-Host "Downloading VaMMCP ($ReleaseTag) ..."
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
    Write-Host "Installed: $dest"
}

$root = Resolve-VamRoot -Candidate $VamRoot
Write-Host "VaM root: $root"
if (-not $SkipBepInEx) { Install-BepInEx -Root $root }
Install-Plugin -Root $root -ReleaseTag $Tag

Write-Host ""
Write-Host "Done. Start VaM, then check BepInEx\LogOutput.log for:"
Write-Host "  VaMMCP ready. MCP endpoint: http://127.0.0.1:9837/mcp"
Write-Host ""
Write-Host "Point your MCP client at that URL, e.g.:"
Write-Host "  claude mcp add vam --transport http http://127.0.0.1:9837/mcp"
