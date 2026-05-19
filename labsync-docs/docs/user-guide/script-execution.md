---
sidebar_position: 2
---

# Script Execution Guide

Execute PowerShell, Bash, or CMD scripts across your device fleet.

## Creating Scripts

### Via Dashboard

1. Navigate to **Scripts** → **My Scripts**
2. Click **New Script**
3. Enter script details:
   - **Title:** Script name (e.g., "Update Windows")
   - **Description:** What the script does
   - **Content:** The actual script code
   - **Interpreter:** Select execution environment

### Supported Interpreters

| Interpreter    | Platform    | Use Case                                |
| -------------- | ----------- | --------------------------------------- |
| **PowerShell** | Windows     | Windows administration, system queries  |
| **CMD**        | Windows     | Legacy batch commands, system utilities |
| **Bash**       | Linux/macOS | Shell scripting, system administration  |

### Script Content Example

#### Windows - PowerShell

```powershell
# Get system information
$computerSystem = Get-ComputerInfo
$computerSystem | Select-Object CsModel, CsSystemType, WindowsVersion | Format-List
```

#### Linux - Bash

```bash
#!/bin/bash
# Check disk usage
df -h
echo "---"
# Check memory
free -h
```

## Saving and Managing Scripts

### Save Script

1. Click **Save**
2. Script is stored in the database
3. Becomes available for deployment

### Edit Script

1. Navigate to **Scripts** → **My Scripts**
2. Click the script
3. Edit content
4. Click **Save**

### Delete Script

1. Navigate to **Scripts** → **My Scripts**
2. Click the script
3. Click **Delete**
4. Confirm deletion

## Executing Scripts

### On-Demand Execution

#### Single Device

1. Go to **Devices** → Select device
2. Click **Execute Script**
3. Choose script from list
4. Optionally adjust timeout (default: 300 seconds)
5. Click **Execute**
6. View real-time output

#### Device Group

1. Go to **Devices** → Select group
2. Click **Execute Script on Group**
3. Choose script
4. Confirm to execute on all devices in group
5. Monitor job status

### Monitoring Execution

During script execution:

- **Real-time Output:** See stdout as script runs
- **Progress Indicator:** Know how far along it is
- **Status:** Pending → Running → Completed/Failed
- **Exit Code:** Process return value (0 = success)

### After Execution

Results are displayed:

- **Output:** Full stdout and stderr
- **Exit Code:** Process return value
- **Duration:** How long the script ran
- **Timestamp:** When execution started/ended

## Scheduled Execution

### Creating a Schedule

1. Navigate to **Scripts** → **Schedules**
2. Click **New Schedule**
3. Select script
4. Enter CRON expression
5. Select target group
6. Click **Create**

### CRON Expression Guide

Basic format: `minute hour day month weekday`

Common examples:

- `0 9 * * MON` - Every Monday at 9:00 AM
- `0 */2 * * *` - Every 2 hours
- `0 0 * * *` - Daily at midnight
- `0 0 1 * *` - First day of month at midnight
- `30 2 * * 0-4` - Weekdays at 2:30 AM

### Enabling/Disabling Schedules

1. Navigate to **Scripts** → **Schedules**
2. Click the schedule
3. Toggle **Enabled**
4. Changes take effect immediately

### Monitoring Scheduled Executions

1. Click schedule
2. View **Execution History**
3. See results of past runs
4. Check for failures or issues

## Advanced Features

### Output Capture

LabSync captures:

- **Stdout:** Normal output and results
- **Stderr:** Error messages
- **Exit Code:** Process return value

Example output:

```
Output:
---
5/19/2026 2:45:00 PM
Microsoft Windows [Version 10.0.19044]
---

Exit Code: 0 (Success)
Duration: 1.2 seconds
```

## Script Templates

Common scripts ready to use:

### System Update (Windows)

```powershell
# Update Windows
Write-Host "Starting Windows Update..."
Start-Process -FilePath "C:\Windows\System32\cmd.exe" -ArgumentList "/c taskkill /IM explorer.exe /F; timeout /t 3; explorer.exe"
Write-Host "Restart scheduled for next maintenance window"
```

### System Cleanup (Linux)

```bash
# Clean package manager cache
sudo apt-get clean
sudo apt-get autoclean
sudo apt-get autoremove
echo "Cleanup complete"
```

### Hardware Inventory (Both)

```powershell
# Windows
Get-ComputerInfo | Select-Object CsManufacturer, CsModel, CsNumberOfProcessors | Format-List
```

```bash
# Linux
lscpu
dmidecode
```

Next: Learn about [Scheduling](./scheduling.md) or [Remote Desktop](./remote-desktop.md)
