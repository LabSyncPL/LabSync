---
sidebar_position: 2
---

# Quick Start Guide

Get started with LabSync in 5 minutes.

## 1. Access the Dashboard

After starting the server, open your browser:

```
http://your-server-address:3000
```

If you are running the server locally, you can also use:

```
http://localhost:3000
```

The frontend connects to the backend API on port `5000`.

## 2. Create Administrator Account

On first access, you'll see the setup wizard:

1. Enter username (e.g., `admin`)
2. Enter password (at least 8 characters recommended)
3. Click "Create Account"
4. You'll be redirected to login page

## 3. Log In

Enter your credentials and click "Log In"

## 4. Install Agent

### Option A: Windows Device

On the Windows machine:

```powershell
# As Administrator
.\install-agent.ps1 -ServerUrl "http://your-server-address:5000"
```

### Option B: Linux Device

On the Linux machine:

```bash
sudo ./install-linux.sh --server-url "http://your-server-address:5000"
```

## 5. Approve Device

Back in the dashboard:

1. Navigate to "Devices"
2. Find your newly registered device (status: "Pending")
3. Click the device
4. Click "Approve"

The device will appear as "Online" within 10 seconds.

## 6. Execute Your First Command

### Test System Metrics

1. Go to Dashboard
2. Select your device
3. Click "Collect Metrics"
4. View the returned system information (CPU, RAM, Disk, etc.)

### Run a Script

1. Go to "Scripts" page
2. Click "Create Script"
3. Enter title: "Test Script"
4. Select interpreter: PowerShell (Windows) or Bash (Linux)
5. Enter content:

   ```powershell
   # Windows example
   Get-Date
   Get-ComputerInfo
   ```

   ```bash
   # Linux example
   date
   uptime
   ```

6. Click "Save"
7. Go to "Devices", select device
8. Click "Deploy Script", choose your script
9. Click "Execute"
10. View the output

## 7. Remote Desktop

1. Go to Devices → Select device
2. Click "Remote Desktop"
3. Browser will stream the device screen
4. Control with mouse and keyboard

## Common Tasks

### Create Device Group

1. Go to "Devices" → "Groups"
2. Click "New Group"
3. Enter name: "Lab Computers"
4. Add devices to the group
5. Can now execute commands on entire group

### Schedule Script Execution

1. Create or select a script
2. Go to "Scripts" → "Schedules"
3. Click "New Schedule"
4. Set cron expression: `0 9 * * MON` (every Monday at 9 AM)
5. Select target group
6. Enable

### SSH Terminal

1. Go to "Devices" → select device
2. Click "SSH Terminal"
3. Browser opens interactive shell
4. Type commands as if you were SSH'd in

## What's Next?

- Explore [Features](../features/overview)
- Read [Architecture Overview](../architecture/overview)
- Check [Troubleshooting](../troubleshooting) if issues arise
