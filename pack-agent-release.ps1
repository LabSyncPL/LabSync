<#
.SYNOPSIS
    Builds a deployable LabSync Agent folder (no repo needed on target machines).

.DESCRIPTION
    On dev machine / CI:
    1. dotnet publish agent
    2. dotnet build all LabSync.Modules.* projects
    3. copy *.dll from each module bin/Release/net9.0 into <output>/Modules
    4. copy install-agent.ps1 and install-linux.sh

    Output: folder (optional .zip) you copy to the target and run:
    install-agent.ps1 -SourcePath <folder> -ServerUrl ...

.PARAMETER RuntimeIdentifier
    e.g. win-x64 (default), linux-x64, linux-arm64

.PARAMETER SelfContained
    If true, bundles .NET runtime (larger output; no dotnet install on host).
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$RepoRoot = "",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "",

    [Parameter(Mandatory = $false)]
    [string]$RuntimeIdentifier = "win-x64",

    [Parameter(Mandatory = $false)]
    [switch]$SelfContained = $false,

    [Parameter(Mandatory = $false)]
    [switch]$Zip = $false
)

$ErrorActionPreference = "Stop"

function Write-Info([string]$Message) {
    Write-Host "[pack-agent] $Message" -ForegroundColor Cyan
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
}

$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$agentProject = Join-Path $RepoRoot "src\LabSync.Agent\LabSync.Agent.csproj"
if (-not (Test-Path -LiteralPath $agentProject)) {
    throw "Agent project not found: $agentProject"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $suffix = if ($SelfContained) { "self-contained" } else { "fx-dep" }
    $OutputDir = Join-Path $RepoRoot "dist\LabSync.Agent-release-$RuntimeIdentifier-$suffix"
}

$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$publishArgs = @(
    "publish", $agentProject,
    "-c", "Release",
    "-o", $OutputDir,
    "-r", $RuntimeIdentifier
)
if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
} else {
    $publishArgs += "--self-contained", "false"
}

Write-Info "Publishing agent: dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish agent failed."
}

$modulesRoot = Join-Path $RepoRoot "src\Modules"
if (-not (Test-Path -LiteralPath $modulesRoot)) {
    Write-Warning "Modules folder missing: $modulesRoot"
} else {
    $modulesOut = Join-Path $OutputDir "Modules"
    New-Item -ItemType Directory -Path $modulesOut -Force | Out-Null

    $moduleDirs = Get-ChildItem -LiteralPath $modulesRoot -Directory -Filter "LabSync.Modules.*"
    foreach ($moduleDir in $moduleDirs) {
        $proj = Join-Path $moduleDir.FullName "$($moduleDir.Name).csproj"
        if (-not (Test-Path -LiteralPath $proj)) {
            continue
        }
        Write-Info "Building module: $($moduleDir.Name)"
        dotnet build $proj -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for module $($moduleDir.Name)."
        }

        $releaseDir = Join-Path $moduleDir.FullName "bin\Release\net9.0"
        if (-not (Test-Path -LiteralPath $releaseDir)) {
            Write-Warning "Build output not found: $releaseDir"
            continue
        }
        Get-ChildItem -LiteralPath $releaseDir -File -Filter "*.dll" | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $modulesOut $_.Name) -Force
        }
    }

    $dllCount = (Get-ChildItem -LiteralPath $modulesOut -File -Filter "*.dll" -ErrorAction SilentlyContinue).Count
    Write-Info "Modules: copied $dllCount DLL file(s) to $modulesOut"
}

$installer = Join-Path $RepoRoot "install-agent.ps1"
if (Test-Path -LiteralPath $installer) {
    Copy-Item -LiteralPath $installer -Destination (Join-Path $OutputDir "install-agent.ps1") -Force
    Write-Info "Included install-agent.ps1"
}

$linuxInstall = Join-Path $RepoRoot "src\LabSync.Agent\scripts\install-linux.sh"
if (Test-Path -LiteralPath $linuxInstall) {
    Copy-Item -LiteralPath $linuxInstall -Destination (Join-Path $OutputDir "install-linux.sh") -Force
    Write-Info "Included install-linux.sh"
}

$reqLine = if (-not $SelfContained) {
    '- Host needs .NET 9 runtime (Windows: Hosting Bundle / desktop runtime; Linux: dotnet-runtime). Framework-dependent package.'
} else {
    '- No separate dotnet install (self-contained package; larger size).'
}

$readme = @"
LabSync Agent - deployment bundle (no git repo on target)

Target machine requirements:
$reqLine

Windows (PowerShell as Administrator):
  cd <extracted_folder>
  .\install-agent.ps1 -ServerUrl "http://your-server:5000" -SourcePath "."

If scripts are blocked (Execution Policy), use:
  powershell -ExecutionPolicy Bypass -File ".\install-agent.ps1" -ServerUrl "http://your-server:5000" -SourcePath "."

Linux (build with -RuntimeIdentifier linux-x64):
  sudo chmod +x install-linux.sh
  sudo ./install-linux.sh --server-url "http://your-server:5000" --source-path "/full/path/to/extracted/folder"

Installer sets AGENT_SERVER_URL.
"@

Set-Content -LiteralPath (Join-Path $OutputDir "README-DEPLOY.txt") -Value $readme -Encoding UTF8

if ($Zip) {
    $zipPath = "$OutputDir.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -Force
    Write-Info "Created archive: $zipPath"
}

Write-Host "[pack-agent] Done: $OutputDir" -ForegroundColor Green
