---
sidebar_position: 1
---

# Installation Guide

This guide covers installing and configuring LabSync for both server and agent deployment.

## System Requirements

### Server Requirements

- .NET 9 Runtime (or SDK for development)
- PostgreSQL 15 or later
- 2GB RAM minimum (4GB recommended)
- 10GB disk space (for database and logs)
- Port 5038 (or configurable) for API
- Port 443 for HTTPS (production)

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
docker-compose up -d
```

This starts:

- PostgreSQL database (TimescaleDB) on port 5432
- PgAdmin 4 on port 8080 (for database management)

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
CORS_ALLOWED_ORIGINS=http://localhost:5173;http://localhost:3000

# Server URLs
ASPNETCORE_URLS=http://0.0.0.0:5038;https://0.0.0.0:5039

# Frontend URL (for React app)
REACT_APP_API_URL=http://localhost:5038
```

### 4. Initial Setup

When the server starts:

1. Navigate to `http://localhost:5038`
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
.\install-agent.ps1 -ServerUrl "http://192.168.1.100:5038"
```

Optional parameters:

```powershell
.\install-agent.ps1 `
  -ServerUrl "http://192.168.1.100:5038" `
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
sudo ./install-linux.sh --server-url "http://192.168.1.100:5038"
```

Optional parameters:

```bash
sudo ./install-linux.sh \
  --server-url "http://192.168.1.100:5038" \
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
[Environment]::SetEnvironmentVariable("AGENT_SERVER_URL", "http://new-server:5038", "Machine")

# Restart service
Restart-Service LabSyncAgent
```

#### Linux

```bash
# Edit environment file
sudo nano /etc/labsync-agent/labsync-agent.env

# Update or add
AGENT_SERVER_URL=http://new-server:5038

# Restart service
sudo systemctl restart labsync-agent
```

## Frontend Installation

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
ping server-ip

# Check port
telnet server-ip 5038
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

## Next Steps

After installation, proceed to:

- [Getting Started Guide](./quick-start)
- [User Guide](../features/overview)
