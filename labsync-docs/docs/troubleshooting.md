---
sidebar_position: 1
---

# Troubleshooting Guide

Common issues and their solutions.

## Server Issues

### Server Won't Start

**Symptom:** Service fails to start or crashes immediately

**Diagnosis:**

```bash
# Check logs
journalctl -u labsync -n 100

# Or Windows event log
Get-EventLog -LogName Application -Source "LabSync*" -Newest 20
```

**Common Causes & Solutions:**

#### Database Connection Failed

```
Error: "Failed to connect to database"
```

**Solution:**

```bash
# Verify PostgreSQL is running
systemctl status postgresql

# Test database connection
psql -h localhost -U labsync -d labsyncdb -c "SELECT 1;"

# Check connection string in environment variables
echo $DB_HOST
echo $DB_PORT
echo $DB_USER
echo $DB_PASSWORD

# If credentials are wrong, update environment
export DB_PASSWORD="correct_password"
systemctl restart labsync
```

#### Port Already in Use

```
Error: "Port 5000 already in use"
```

**Solution:**

```bash
# Find process using the port
netstat -tlnp | grep 5000
sudo lsof -i :5000

# Kill the process
sudo kill -9 <PID>

# Or use different port
export ASPNETCORE_URLS="http://0.0.0.0:5040"
```

#### Migrations Failed

```
Error: "Database migrations failed"
```

**Solution:**

```bash
# Manually run migrations
cd /opt/labsync
dotnet LabSync.Server.dll --migrate

# Or if using manual migration tool
dotnet ef database update --project LabSync.Server

# Check migration status
psql -U labsync -d labsyncdb -c "SELECT * FROM __EFMigrationsHistory;"
```

### Server Running Slowly

**Symptoms:**

- Dashboard takes 10+ seconds to load
- Job execution hangs
- Database timeouts

**Diagnosis:**

```bash
# Check system resources
top -b -n 1 | head -20
free -h
df -h

# Check database size
psql -U labsync -d labsyncdb -c "SELECT pg_size_pretty(pg_database_size('labsyncdb'));"

# Check active connections
psql -U labsync -d labsyncdb -c "SELECT count(*) FROM pg_stat_activity;"

# Check server logs for long-running operations
journalctl -u labsync --since "10 minutes ago" | grep -i timeout
```

**Solutions:**

#### Database Too Large

```sql
-- Clean old job records (keep last 30 days)
DELETE FROM "Job" WHERE "CreatedAt" < NOW() - INTERVAL '30 days';
VACUUM ANALYZE;
```

#### Connection Pool Exhausted

```
Update /etc/systemd/system/labsync.service:

Environment="ASPNETCORE_Logging__LogLevel__Microsoft__EntityFrameworkCore=Debug"

Then check logs for "connection limit"
```

#### Too Many Active Connections

```bash
# Increase max connections in PostgreSQL
sudo nano /etc/postgresql/15/main/postgresql.conf

# Change:
# max_connections = 200

sudo systemctl restart postgresql
```

## Agent Issues

### Agent Won't Register

**Symptom:** Agent stays in "Offline" or "Pending" but doesn't connect

**Diagnosis:**

#### Windows

```powershell
# Check service status
Get-Service LabSyncAgent

# View service logs
Get-EventLog -LogName Application -Source "LabSync*" -Newest 50

# Check environment variable
$env:AGENT_SERVER_URL

# Test network connectivity
Test-NetConnection -ComputerName your-server -Port 5000 -InformationLevel Detailed

# Check firewall
Get-NetFirewallRule -Direction Outbound -Name "*LabSync*" -ErrorAction SilentlyContinue
```

#### Linux

```bash
# Check service status
systemctl status labsync-agent

# View service logs
journalctl -u labsync-agent -n 50 -f

# Check environment variable
echo $AGENT_SERVER_URL

# Or in service environment file
cat /etc/labsync-agent/labsync-agent.env

# Test connectivity
curl -I https://your-server:5000

# Check firewall
sudo iptables -L -n | grep 5000
```

**Common Causes & Solutions:**

#### Wrong Server URL

```bash
# Windows
[Environment]::SetEnvironmentVariable("AGENT_SERVER_URL", "https://your-server:5000", "Machine")
Restart-Service LabSyncAgent

# Linux
sudo nano /etc/labsync-agent/labsync-agent.env
# Update AGENT_SERVER_URL=https://your-server:5000
sudo systemctl restart labsync-agent
```

#### Network Blocked

```bash
# Windows - Check firewall
New-NetFirewallRule -DisplayName "LabSync Agent" -Direction Outbound -Action Allow -Protocol TCP -RemotePort 5000

# Linux - Check firewall
sudo ufw allow out to any port 5000
sudo iptables -A OUTPUT -p tcp --dport 5000 -j ACCEPT
```

#### Certificate Not Trusted

```
Error: "The SSL certificate is not trusted"
```

**Solution:**

```bash
# Windows - Add certificate to trusted store
# Linux - Update CA certificates
sudo apt install ca-certificates
sudo update-ca-certificates

# Or disable certificate verification (DEVELOPMENT ONLY)
# In appsettings.json
"HttpClientSettings": {
  "VerifyCertificate": false
}
```

#### Firewall Blocks Outbound

```bash
# Test from Windows machine
netstat -an | findstr :5000

# Test from Linux
netstat -an | grep :5000

# Enable through firewall
Windows: Settings → Privacy & Security → Windows Defender Firewall → Allow app through firewall
Linux: sudo ufw allow out 5000/tcp
```

### Agent Offline After Registration

**Symptom:** Agent appears in dashboard then goes offline

**Diagnosis:**

```bash
# Check logs immediately after agent goes offline
# Windows
Get-EventLog -LogName Application -Source "LabSync*" -Newest 20

# Linux
journalctl -u labsync-agent | tail -50
```

**Common Causes:**

#### Service Crashed

```bash
# Windows
Get-Service LabSyncAgent

# Linux
systemctl status labsync-agent

# Restart
Restart-Service LabSyncAgent  # Windows
sudo systemctl restart labsync-agent  # Linux
```

#### Authentication Token Expired

```
Error: "Token validation failed"
```

**Solution:** Agent will automatically re-register with new token. If not:

```bash
# Windows
Restart-Service LabSyncAgent

# Linux
sudo systemctl restart labsync-agent
```

#### Server Unreachable

```
Error: "Unable to connect to server"
```

**Solution:** See "Network Blocked" section above

### Job Execution Fails

**Symptom:** Job shows status "Failed" with no output

**Diagnosis:**

#### Windows

```powershell
# Check job details in dashboard for error message

# Check agent logs
Get-EventLog -LogName Application -Source "LabSync*" -Newest 10

# Test script manually in PowerShell
Get-Date  # If script is "Get-Date"
```

#### Linux

```bash
# Check job details in dashboard for error message

# Check agent logs
journalctl -u labsync-agent -n 10

# Test script manually in bash
date  # If script is "date"
```

**Common Causes:**

#### Timeout (Default 300 seconds)

```
Error: "Job timed out"
```

**Solution:**

```json
{
  "command": "ScriptExecution",
  "arguments": {
    "__TimeoutSeconds": "600" // Increase to 600 seconds
  }
}
```

#### Interpreter Not Available

```
Error: "PowerShell not found" or "Bash not found"
```

**Solution:**

```bash
# Windows - Verify PowerShell is in PATH
where powershell

# Linux - Verify bash is available
which bash
which pwsh  # For PowerShell Core

# Install missing interpreter
Ubuntu: sudo apt install powershell
Windows: Comes pre-installed
```

#### Script Syntax Error

```
Error: "Unexpected token" or "command not found"
```

**Solution:**

1. Check script content for typos
2. Test script locally first
3. Use specific interpreter (not "auto")

#### Insufficient Permissions

```
Error: "Access denied" or "Operation not permitted"
```

**Solution:**

```bash
# Windows - Run as Administrator
# Linux - Run with sudo
# Or adjust script permissions

# Example: Log file access denied
# Script: Get-Content "C:\Program Files\Some\log.txt"
# Solution: Change path to location agent has access
```

## Remote Desktop Issues

### Video Stream Not Starting

**Symptom:** Click "Remote Desktop" but screen stays black

**Diagnosis:**

```bash
# Windows - Check RemoteDesktop module logs
Get-EventLog -LogName Application -Source "*RemoteDesktop*" -Newest 10

# Check ffmpeg installation
ffmpeg -version

# Verify GPU is accessible
# If using Nvidia: nvidia-smi
# If using AMD: amdgpu info
```

**Common Causes:**

#### FFmpeg Not Installed

```
Error: "ffmpeg not found"
```

**Solution:**

```bash
# Windows
winget install ffmpeg

# Linux
sudo apt install ffmpeg
```

#### GPU Not Available

```
Error: "No compatible GPU found, falling back to software encoding"
```

**Solution:**

```bash
# Windows - Update graphics drivers
# Go to Windows Update or manufacturer website

# Linux - Install video drivers
# AMD: sudo apt install libva-glx2
# Intel: sudo apt install intel-media-driver
```

#### Port Blocked

```
Error: "WebRTC connection failed"
```

**Solution:**

```bash
# Firewall needs to allow UDP (WebRTC uses UDP)
# Windows: Windows Defender Firewall → Allow app
# Linux: sudo ufw allow in proto udp from any
```

### High Latency / Stuttering

**Symptoms:**

- Mouse/keyboard response is slow
- Video is choppy
- Audio/video out of sync

**Solutions:**

```bash
# Reduce bitrate in configuration
# appsettings.json

"RemoteDesktop": {
  "MaxBitrate": 2000,  // Reduce from default 5000
  "MaxFramerate": 15    // Reduce from default 30
}

# Or increase bandwidth for high-speed network
"MaxBitrate": 10000
"MaxFramerate": 60
```

## SSH Terminal Issues

### SSH Connection Refused

**Symptom:** Click "SSH Terminal" and get "Connection refused"

**Diagnosis:**

```bash
# Check SSH service running on device
# Linux
sudo systemctl status ssh
sudo netstat -tlnp | grep :22

# Verify SSH credentials
cat ~/.ssh/authorized_keys
```

**Common Causes:**

#### SSH Service Not Running

```bash
# Linux
sudo systemctl start ssh
sudo systemctl enable ssh

# Verify
sudo systemctl status ssh
```

#### SSH Credentials Not Set

```bash
# Dashboard: Device → SSH Credentials
# Set username and key or password
```

#### SSH Key Not Valid

```
Error: "Authentication failed"
```

**Solution:**

```bash
# Generate new SSH key if needed
ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa

# Update credentials in dashboard with new public key
cat ~/.ssh/id_rsa.pub  # Add this to authorized_keys
```

### SSH Timeout

**Symptom:** Terminal session disconnects after period of inactivity

**Solution:**

```bash
# Update SSH configuration
# Edit /etc/ssh/sshd_config

ServerAliveInterval 60
TCPKeepAlive yes
ClientAliveInterval 60
ClientAliveCountMax 100

sudo systemctl restart ssh
```

## Metrics Collection Issues

### No Metrics Displayed

**Symptom:** "Collect Metrics" returns empty result

**Diagnosis:**

```bash
# Check SystemInfo module is loaded
# Windows: Check Agent logs for "SystemInfo module initialized"
# Linux: journalctl -u labsync-agent | grep SystemInfo

# Check job status
# Dashboard: Device → Jobs → find metrics job → check output
```

**Common Causes:**

#### Module Not Loaded

```
Error: "SystemInfo module not initialized"
```

**Solution:**

```bash
# Verify module DLL exists
# Windows: C:\Program Files\LabSync.Agent\Modules\LabSync.Modules.SystemInfo.dll
# Linux: /opt/labsync-agent/Modules/LabSync.Modules.SystemInfo.dll

# Restart agent
Restart-Service LabSyncAgent  # Windows
sudo systemctl restart labsync-agent  # Linux
```

#### Permission Denied

```
Error: "Access denied reading /proc/stat" or "WMI access denied"
```

**Solution:**

```bash
# Windows - Run agent as Administrator (already required)
# Linux - Agent already runs as root via systemd

# If not, grant permissions
sudo usermod -a -G adm labsync-agent-user
```

## Performance Issues

### High CPU Usage

**Symptoms:**

- Agent consuming 50%+ CPU
- Dashboard sluggish

**Diagnosis:**

```bash
# Find process
# Windows
tasklist | findstr LabSync

# Linux
ps aux | grep LabSync

# Monitor usage
# Windows
Get-Process | Select ProcessName, CPU | Sort CPU -Descending

# Linux
top -b -n 1 | head -20
```

**Solutions:**

#### Video Stream Encoding

```bash
# Most common cause - RemoteDesktop module
# Reduce resolution and bitrate

"RemoteDesktop": {
  "MaxBitrate": 1000,
  "MaxFramerate": 10,
  "CaptureFramerate": 10
}
```

#### Multiple Concurrent Jobs

```bash
# Limit concurrent job execution
# In appsettings.json

"JobExecution": {
  "MaxConcurrentJobs": 2  // Reduce from default 5
}
```

### High Memory Usage

**Symptoms:**

- Agent memory grows over time
- Server crashes with OOM

**Diagnosis:**

```bash
# Check memory usage
# Windows
Get-Process LabSync* | Select Name, WorkingSet

# Linux
ps aux --sort=-%mem | head -10
```

**Solutions:**

#### Job Output Not Cleared

```bash
# Clear old job records
psql -U labsync -d labsyncdb -c "DELETE FROM \"Job\" WHERE \"CreatedAt\" < NOW() - INTERVAL '14 days';"

# Agent memory will be reclaimed on restart
systemctl restart labsync-agent
```

#### Log Files Growing

```bash
# Set up log rotation
# Linux: /etc/logrotate.d/labsync-agent
daily
rotate 7
compress
delaycompress

# Windows: Use Windows built-in log rotation
# Event Viewer → Application → Properties → Configure log size
```

## Database Issues

### Database Locks

**Symptom:** Operations hang or timeout

**Diagnosis:**

```bash
# Check for locks
psql -U labsync -d labsyncdb -c "SELECT * FROM pg_stat_activity WHERE state != 'idle';"

# Check blocked queries
psql -U labsync -d labsyncdb -c "SELECT * FROM pg_blocking_pids(pid);"
```

**Solution:**

```bash
# Restart database connection pool
systemctl restart labsync

# Or kill blocking queries
psql -U labsync -d labsyncdb -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE usename = 'labsync' AND state = 'idle';"
```

### Disk Space Full

**Symptom:** Database stops accepting writes

**Diagnosis:**

```bash
# Check disk usage
df -h

# Check database size
psql -U labsync -d labsyncdb -c "SELECT pg_size_pretty(pg_database_size('labsyncdb'));"

# Check table sizes
psql -U labsync -d labsyncdb -c "SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) FROM pg_tables WHERE schemaname='public' ORDER BY pg_total_relation_size DESC;"
```

**Solution:**

```bash
# Archive old records
DELETE FROM "Job" WHERE "CreatedAt" < NOW() - INTERVAL '30 days';
DELETE FROM "ScheduledScriptExecution" WHERE "CreatedAt" < NOW() - INTERVAL '90 days';
VACUUM FULL;

# Expand disk space
# Add more disk or mount new volume

# Compress old log files
find /var/log/labsync -name "*.log" -mtime +30 | xargs gzip
```

## Getting Help

### Collect Diagnostics

Before reporting issues, collect:

```bash
# Server diagnostics
# Server logs (last 100 lines)
journalctl -u labsync -n 100 > labsync-server.log

# Database status
psql -U labsync -d labsyncdb -c "SELECT version();" > database-version.txt

# Agent diagnostics
# Agent logs
journalctl -u labsync-agent -n 100 > labsync-agent.log

# System info
uname -a > system-info.txt
free -h >> system-info.txt
df -h >> system-info.txt
```

### Enable Debug Logging

In `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning",
      "LabSync": "Debug"
    }
  }
}
```

Restart and reproduce issue, then check logs.
