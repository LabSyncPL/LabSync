<#
.SYNOPSIS
    Installs LabSync Agent as a Windows Service.
    
.PARAMETER ServerUrl
    The URL of the LabSync Server (e.g., http://your-server:5038)
    
.PARAMETER InstallDir
    Target installation directory. Default: C:\Program Files\LabSync Agent
#>
param(
    [Parameter(Mandatory=$true)]
    [string]$ServerUrl,
    
    [string]$InstallDir = "C:\Program Files\LabSync Agent",
    
    [string]$SourcePath = ".", # Path to published files or project root
    
    [switch]$IncludeFfmpeg = $true
)

$ErrorActionPreference = "Stop"

function Write-Host-Color($message, $color = "Cyan") {
    Write-Host "[LabSync] $message" -ForegroundColor $color
}

# 1. Check for Admin
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
}

# 1b. Check for .NET 9 Runtime
$dotnetVersion = & dotnet --list-runtimes | Select-String "Microsoft.NETCore.App 9."
if (-not $dotnetVersion) {
    Write-Host-Color "Warning: .NET 9 Runtime not detected. Please install it from https://dotnet.microsoft.com/download/dotnet/9.0" "Yellow"
}

# 2. Prepare Directory
if (-not (Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory -Force
}

# 2b. Copy or Build Binaries
$agentExe = "LabSync.Agent.exe"
$sourceExePath = Join-Path $SourcePath $agentExe

if (-not (Test-Path $sourceExePath)) {
    # If .exe not found in SourcePath, check if it's the project root and try to build
    $projectPath = Join-Path $SourcePath "src\LabSync.Agent\LabSync.Agent.csproj"
    if (Test-Path $projectPath) {
        Write-Host-Color "Binaries not found. Attempting to build from source..."
        dotnet publish $projectPath -c Release -o "$env:TEMP\LabSyncPublish" --self-contained false
        $SourcePath = "$env:TEMP\LabSyncPublish"
    } else {
        Write-Error "Could not find $agentExe in $SourcePath and no project found to build."
    }
}

Write-Host-Color "Copying files to $InstallDir..."
Copy-Item -Path "$SourcePath\*" -Destination $InstallDir -Recurse -Force -Exclude "bootstrap.json", "ffmpeg"

# 3. Handle FFmpeg Dependency
if ($IncludeFfmpeg) {
    $ffmpegDir = Join-Path $InstallDir "ffmpeg"
    if (-not (Test-Path $ffmpegDir)) {
        Write-Host-Color "Downloading FFmpeg..."
        $ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
        $zipPath = "$env:TEMP\ffmpeg.zip"
        
        Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipPath
        Expand-Archive -Path $zipPath -DestinationPath "$env:TEMP\ffmpeg_temp" -Force
        
        $extractedDir = Get-ChildItem "$env:TEMP\ffmpeg_temp\ffmpeg-master*" | Select-Object -First 1
        Move-Item -Path "$($extractedDir.FullName)\bin" -Destination $ffmpegDir -Force
        
        Remove-Item $zipPath -ErrorAction SilentlyContinue
        Remove-Item "$env:TEMP\ffmpeg_temp" -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host-Color "FFmpeg installed to $ffmpegDir"
    }
}

# 4. Configure Bootstrap
$bootstrap = @{
    initialServerUrl = $ServerUrl
}
$bootstrapPath = Join-Path $InstallDir "bootstrap.json"
$bootstrap | ConvertTo-Json | Out-File -FilePath $bootstrapPath -Encoding utf8
Write-Host-Color "Configuration saved to $bootstrapPath"

# 5. Setup Windows Service
$serviceName = "LabSyncAgent"
$exePath = Join-Path $InstallDir "LabSync.Agent.exe"

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Write-Host-Color "Stopping existing service..."
    Stop-Service $serviceName -ErrorAction SilentlyContinue
    Remove-Service $serviceName -ErrorAction SilentlyContinue
}

Write-Host-Color "Registering Windows Service..."
New-Service -Name $serviceName `
            -BinaryPathName "`"$exePath`"" `
            -DisplayName "LabSync Agent" `
            -Description "Provides LabSync background monitoring and management services." `
            -StartupType Automatic

Start-Service $serviceName
Write-Host-Color "LabSync Agent installed and started successfully!" "Green"
