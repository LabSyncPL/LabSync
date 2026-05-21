---
sidebar_position: 1
---

# Installation Guide

This guide covers installing and configuring LabSync for both server and agent deployment.

## System Requirements

### Server Requirements

- Docker and Docker Compose (for containerized deployment) OR .NET 9 Runtime (for manual installation)
- PostgreSQL 15 or later
- 2GB RAM minimum (4GB recommended)
- 10GB disk space (for database and logs)
- Port 5000 (or configurable) for API
- Port 443 for HTTPS (production)
- Port 3000 for Frontend (with Nginx)

### Agent Requirements

#### Windows

- Windows 10 or Windows 11
- .NET 9 Runtime (for framework-dependent bundle) or self-contained bundle
- Administrator privileges for installation
- ffmpeg (for RemoteDesktop module) - `winget install ffmpeg`

#### Linux

- Ubuntu 20.04 LTS or later (or equivalent)
- .NET 9 Runtime
- sudo access for installation
- ffmpeg - `sudo apt install ffmpeg`

## Server Installation

### 1. Using Docker Compose (Recommended)

The easiest way to run LabSync server is with Docker:

```bash
cd LabSync
docker compose up --build
```

This starts:

- API Server on port 5000
- PostgreSQL database (TimescaleDB) on port 5432
- PgAdmin 4 on port 8080 (for database management)
- Frontend (React + Nginx) on port 3000

Access the application at: `http://your-server-address:3000` (or `http://localhost:3000` if running locally)

### 2. Manual Installation

#### Prerequisites

```bash
# Install .NET 9 SDK
# https://dotnet.microsoft.com/download/dotnet/9.0

# Install PostgreSQL
# https://www.postgresql.org/download/
```

#### Build and Run

```bash
cd src
dotnet build LabSync.sln
dotnet publish LabSync.Server -c Release -o dist

# Set environment variables
export DB_HOST=localhost
export DB_PORT=5432
export DB_NAME=LabSyncDb
export DB_USER=postgres
export DB_PASSWORD=your_secure_password

cd dist
dotnet LabSync.Server.dll
```

### 3. Configuration

Create a `.env` file in the repository root:

```env
# Database
DB_HOST=postgres
DB_PORT=5432
DB_NAME=LabSyncDb
DB_USER=postgres
DB_PASSWORD=your_secure_password

# CORS (for frontend access)
CORS_ALLOWED_ORIGINS=http://localhost:5173;http://localhost:3000;http://your-server-address:3000

# Server URLs
ASPNETCORE_URLS=http://0.0.0.0:5000;https://0.0.0.0:5001

# Frontend URL (for React app)
REACT_APP_API_URL=http://your-server-address:5000
```

### 4. Initial Setup

When the server starts:

1. Navigate to `http://your-server-address:5000` (or `http://localhost:5000` if running locally)
2. You will see the setup wizard
3. Create the first administrator account
4. Log in with your credentials

## Agent Installation

### Windows Installation

#### Using PowerShell (Recommended)

```powershell
# Download the release bundle
# From: https://github.com/LabSyncPL/LabSync/releases

# Extract the ZIP file
# Navigate to the extracted folder

# Run the installation script as Administrator
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
.\install-agent.ps1 -ServerUrl "http://your-server-address:5000"
```

Optional parameters:

```powershell
.\install-agent.ps1 `
  -ServerUrl "http://your-server-address:5000" `
  -InstallDir "C:\Program Files\LabSync.Agent" `
  -SourcePath "." `
  -ServiceName "LabSyncAgent"
```

The script:

- Copies binaries to `C:\Program Files\LabSync.Agent`
- Sets environment variable `AGENT_SERVER_URL`
- Creates Windows service "LabSyncAgent" (starts automatically)
- Starts the service

#### Verification

```powershell
# Check service status
Get-Service LabSyncAgent

# View logs
Get-EventLog -LogName Application -Source "LabSync*" -Newest 50
```

### Linux Installation

#### Using Bash

```bash
# Download the release bundle
# From: https://github.com/LabSyncPL/LabSync/releases

# Extract the TAR/ZIP file
tar -xzf LabSync.Agent-release-linux-x64.tar.gz

# Navigate to the extracted folder
cd LabSync.Agent-release-linux-x64

# Run the installation script
sudo chmod +x install-linux.sh
sudo ./install-linux.sh --server-url "http://your-server-address:5000"
```

Optional parameters:

```bash
sudo ./install-linux.sh \
  --server-url "http://your-server-address:5000" \
  --install-dir "/opt/labsync-agent" \
  --service-name "labsync-agent"
```

The script:

- Copies binaries to `/opt/labsync-agent`
- Creates environment file at `/etc/labsync-agent/labsync-agent.env`
- Creates systemd service `labsync-agent.service`
- Enables and starts the service

#### Verification

```bash
# Check service status
sudo systemctl status labsync-agent

# View logs
sudo journalctl -u labsync-agent -f

# Stop/start service
sudo systemctl stop labsync-agent
sudo systemctl start labsync-agent
```

## Agent Configuration

### Environment Variables

The agent looks for `AGENT_SERVER_URL` in this order:

1. Environment variable `AGENT_SERVER_URL`
2. Configuration file key `ServerUrl` in `appsettings.json`

### Changing Server URL

#### Windows

```powershell
# Update environment variable
[Environment]::SetEnvironmentVariable("AGENT_SERVER_URL", "http://your-server-address:5000", "Machine")

# Restart service
Restart-Service LabSyncAgent
```

#### Linux

```bash
# Edit environment file
sudo nano /etc/labsync-agent/labsync-agent.env

# Update or add
AGENT_SERVER_URL=http://your-server-address:5000

# Restart service
sudo systemctl restart labsync-agent
```

## Frontend Installation

### Docker Deployment (Recommended)

The frontend is automatically deployed with Docker Compose:

```bash
cd LabSync
docker compose up --build
```

Access the frontend at: `http://your-server-address:3000` (or `http://localhost:3000` if running locally)

The frontend runs behind Nginx reverse proxy in a container.

### Development

```bash
cd src/client

# Install dependencies
npm install

# Start development server
npm run dev

# Open browser to http://localhost:5173
```

### Production Build

```bash
cd src/client

# Build optimized bundle
npm run build

# Output in dist/ folder - ready for deployment
```

## Troubleshooting

### Agent Cannot Connect to Server

**Check 1: Network Connectivity**

```bash
# Verify server is reachable
ping your-server-address

# Check port
telnet your-server-address 5000
```

**Check 2: Server URL Configuration**

```powershell
# Windows - verify environment variable
$env:AGENT_SERVER_URL

# View appsettings.json
cat "C:\Program Files\LabSync.Agent\appsettings.json"
```

**Check 3: Firewall Rules**

- Ensure firewall allows outbound connections on port 5038
- Windows: Check Windows Defender Firewall
- Linux: Check iptables/firewalld

### Database Connection Failed

```bash
# Verify PostgreSQL is running
docker ps | grep postgres

# Check connection string in environment
echo $DB_HOST
echo $DB_PORT
```

## Uninstalling the Agent

### Linux Uninstallation

#### 1. Stop and Disable the Service

```bash
# Stop the service
sudo systemctl stop labsync-agent

# Disable automatic startup
sudo systemctl disable labsync-agent
```

#### 2. Remove Systemd Service File

```bash
# Remove the service file
sudo rm -f /etc/systemd/system/labsync-agent.service

# Reload systemd configuration
sudo systemctl daemon-reload
sudo systemctl reset-failed
```

#### 3. Remove Agent Files and Configuration

```bash
# Remove agent installation directory
sudo rm -rf /opt/labsync-agent

# Remove configuration directory
sudo rm -rf /etc/labsync-agent
```

#### 4. Clean Up (Optional)

```bash
# View remaining processes (if any)
ps aux | grep labsync

# Remove any remaining logs
sudo rm -rf /var/log/labsync-agent
```

### Windows Uninstallation

#### 1. Stop and Remove the Service

```powershell
# Stop the service
Stop-Service -Name LabSyncAgent -Force

# Remove the service
sc.exe delete LabSyncAgent
```

#### 2. Remove Agent Files

```powershell
# Remove installation directory
Remove-Item -Path "C:\Program Files\LabSync.Agent" -Recurse -Force
```

#### 3. Remove Environment Variable

```powershell
# Remove the AGENT_SERVER_URL environment variable
[Environment]::SetEnvironmentVariable("AGENT_SERVER_URL", $null, "Machine")
```

#### 4. Clean Up Registry (Optional)

```powershell
# View installed applications (optional verification)
Get-WmiObject -Class Win32_Product | Where-Object {$_.Name -like "*LabSync*"}
```

## Agent Configuration for Remote Desktop Display

When using the RemoteDesktop module on Linux systems with X11, you may need to configure display environment variables:

### Linux Display Configuration

Edit the systemd service environment:

```bash
sudo nano /etc/labsync-agent/labsync-agent.env
```

Add the following lines:

```env
# Display server for remote desktop capture
DISPLAY=:0
XAUTHORITY=/home/username/.Xauthority
```

Replace `username` with the actual user running the service.

Then restart the service:

```bash
sudo systemctl restart labsync-agent
```

**Note:** The user running the service must have permission to access the X11 display.

## Next Steps

After installation, proceed to:

- [Getting Started Guide](./quick-start)
- [User Guide](../features/overview)
