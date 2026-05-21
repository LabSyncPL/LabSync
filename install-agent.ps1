<#
.SYNOPSIS
    Installs LabSync Agent as a Windows service.

.DESCRIPTION
    The script can install from:
    - Published binaries folder (contains LabSync.Agent.exe or LabSync.Agent.dll), or
    - Repository root (contains src/LabSync.Agent/LabSync.Agent.csproj), where it will publish automatically.

    It also:
    - configures AGENT_SERVER_URL (machine environment variable),
    - updates appsettings.json ServerUrl for visibility,
    - copies module DLL files to <InstallDir>\Modules.

    If you see "running scripts is disabled", run as Administrator from this folder:
      powershell -ExecutionPolicy Bypass -File ".\install-agent.ps1" -ServerUrl "http://SERVER:5000" -SourcePath "."
#>
param(
    [Parameter(Mandatory = $false)]
    [string]$ServerUrl,

    [Parameter(Mandatory = $false)]
    [string]$InstallDir = "C:\Program Files\LabSync.Agent",

    [Parameter(Mandatory = $false)]
    [string]$SourcePath = ".",

    [Parameter(Mandatory = $false)]
    [string]$ServiceName = "LabSyncAgent"
)

$ErrorActionPreference = "Stop"

function Write-Info([string]$Message) {
    Write-Host "[LabSync] $Message" -ForegroundColor Cyan
}

function Write-Warn([string]$Message) {
    Write-Host "[LabSync] $Message" -ForegroundColor Yellow
}

function Assert-Admin {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        throw "Run this script as Administrator."
    }
}

function Resolve-PathSafe([string]$PathValue) {
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $PathValue).Path)
}

function Build-AgentAndModulesFromRepo([string]$RepoRoot, [string]$PublishDir) {
    $agentProject = Join-Path $RepoRoot "src\LabSync.Agent\LabSync.Agent.csproj"
    if (-not (Test-Path -LiteralPath $agentProject)) {
        throw "Could not find LabSync.Agent.csproj under $RepoRoot"
    }

    Write-Info "Publishing agent from source..."
    dotnet publish "$agentProject" -c Release -o "$PublishDir" --self-contained false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }

    $modulesRoot = Join-Path $RepoRoot "src\Modules"
    if (Test-Path -LiteralPath $modulesRoot) {
        $moduleProjects = Get-ChildItem -LiteralPath $modulesRoot -Directory -Filter "LabSync.Modules.*"
        foreach ($moduleDir in $moduleProjects) {
            $proj = Join-Path $moduleDir.FullName "$($moduleDir.Name).csproj"
            if (Test-Path -LiteralPath $proj) {
                Write-Info "Building module: $($moduleDir.Name)"
                dotnet build "$proj" -c Release
                if ($LASTEXITCODE -ne 0) {
                    throw "Module build failed: $($moduleDir.Name)"
                }
            }
        }
    }
}

function Copy-ModuleDlls([string]$SourceRoot, [string]$InstallModulesDir) {
    New-Item -ItemType Directory -Path $InstallModulesDir -Force | Out-Null

    $copied = 0

    $sourceModulesDir = Join-Path $SourceRoot "Modules"
    if (Test-Path -LiteralPath $sourceModulesDir) {
        Get-ChildItem -LiteralPath $sourceModulesDir -File -Filter "*.dll" | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $InstallModulesDir $_.Name) -Force
            $copied++
        }
    }

    $repoModules = Join-Path $SourceRoot "src\Modules"
    if (Test-Path -LiteralPath $repoModules) {
        $moduleDirs = Get-ChildItem -LiteralPath $repoModules -Directory -Filter "LabSync.Modules.*"
        foreach ($moduleDir in $moduleDirs) {
            $releaseDir = Join-Path $moduleDir.FullName "bin\Release\net9.0"
            if (Test-Path -LiteralPath $releaseDir) {
                Get-ChildItem -LiteralPath $releaseDir -File -Filter "*.dll" | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $InstallModulesDir $_.Name) -Force
                    $copied++
                }
            }
        }
    }

    if ($copied -eq 0) {
        Write-Warn "No module DLLs copied. Agent will start, but dynamic modules may be unavailable."
    } else {
        Write-Info "Copied $copied module DLL files into $InstallModulesDir"
    }
}

if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    $ServerUrl = Read-Host "Enter LabSync server URL (e.g. https://labsync.example.com or http://192.168.1.10:5000)"
}

if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    throw "Server URL is required."
}

$ServerUrl = $ServerUrl.Trim().TrimEnd("/")
if ($ServerUrl -notmatch '^(?i)https?://') {
    $ServerUrl = "http://$ServerUrl"
}

Assert-Admin
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

$sourceResolved = Resolve-PathSafe $SourcePath
$tempPublishDir = Join-Path $env:TEMP "LabSync.Agent.publish.$([Guid]::NewGuid().ToString('N'))"
$effectiveSource = $sourceResolved
$cleanupTempPublish = $false

$sourceExe = Join-Path $sourceResolved "LabSync.Agent.exe"
$sourceDll = Join-Path $sourceResolved "LabSync.Agent.dll"
$repoProject = Join-Path $sourceResolved "src\LabSync.Agent\LabSync.Agent.csproj"

if ((-not (Test-Path -LiteralPath $sourceExe)) -and (-not (Test-Path -LiteralPath $sourceDll)) -and (Test-Path -LiteralPath $repoProject)) {
    New-Item -ItemType Directory -Path $tempPublishDir -Force | Out-Null
    Build-AgentAndModulesFromRepo -RepoRoot $sourceResolved -PublishDir $tempPublishDir
    $effectiveSource = $tempPublishDir
    $cleanupTempPublish = $true
}

if ((-not (Test-Path -LiteralPath (Join-Path $effectiveSource "LabSync.Agent.exe"))) -and
    (-not (Test-Path -LiteralPath (Join-Path $effectiveSource "LabSync.Agent.dll")))) {
    throw "No LabSync.Agent executable found in $effectiveSource. Provide published binaries or repository root."
}

Write-Info "Copying agent binaries to $InstallDir"
Copy-Item -Path (Join-Path $effectiveSource "*") -Destination $InstallDir -Recurse -Force

$modulesInstallDir = Join-Path $InstallDir "Modules"
Copy-ModuleDlls -SourceRoot $sourceResolved -InstallModulesDir $modulesInstallDir

[Environment]::SetEnvironmentVariable("AGENT_SERVER_URL", $ServerUrl, "Machine")
Write-Info "Set machine environment variable AGENT_SERVER_URL=$ServerUrl"

$configPath = Join-Path $InstallDir "appsettings.json"
if (Test-Path -LiteralPath $configPath) {
    try {
        $configObj = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        if ($configObj.PSObject.Properties.Name -contains "ServerUrl") {
            $configObj.ServerUrl = $ServerUrl
        } else {
            $configObj | Add-Member -NotePropertyName "ServerUrl" -NotePropertyValue $ServerUrl
        }
        $configObj | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $configPath -Encoding UTF8
        Write-Info "Updated ServerUrl in $configPath"
    } catch {
        Write-Warn "Failed to update appsettings.json automatically: $($_.Exception.Message)"
    }
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Info "Stopping existing service $ServiceName"
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

$exePath = Join-Path $InstallDir "LabSync.Agent.exe"
$dllPath = Join-Path $InstallDir "LabSync.Agent.dll"

if (Test-Path -LiteralPath $exePath) {
    $binaryPath = "`"$exePath`""
} elseif (Test-Path -LiteralPath $dllPath) {
    $dotnetCmd = (Get-Command dotnet).Source
    $binaryPath = "`"$dotnetCmd`" `"$dllPath`""
} else {
    throw "Installed agent executable was not found."
}

Write-Info "Creating Windows service $ServiceName"
New-Service -Name $ServiceName `
    -BinaryPathName $binaryPath `
    -DisplayName "LabSync Agent" `
    -Description "LabSync managed endpoint agent." `
    -StartupType Automatic

try {
    Start-Service -Name $ServiceName
    Write-Info "Service started successfully."
} catch {
    Write-Warn "Start-Service failed: $($_.Exception.Message)"
    Write-Warn "Check Windows Logs -> Application (from LabSync Agent / .NET Runtime)."
    Write-Warn "Framework-dependent builds need .NET 9 runtime installed for this RID."
    Write-Warn "Try running manually: & $binaryPath (quote dotnet path if service uses dotnet.exe)."
    throw
}

if ($cleanupTempPublish -and (Test-Path -LiteralPath $tempPublishDir)) {
    Remove-Item -LiteralPath $tempPublishDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "[LabSync] Installation completed." -ForegroundColor Green
