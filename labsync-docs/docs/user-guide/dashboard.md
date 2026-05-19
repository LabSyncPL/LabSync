---
sidebar_position: 1
---

# Dashboard Guide

The main LabSync dashboard provides an at-a-glance view of your entire IT infrastructure.

## Dashboard Overview

When you log in to LabSync, you'll see the main dashboard with:

### Device Status Cards

- **Total Devices:** Count of all registered devices
- **Online Devices:** Number currently connected
- **Offline Devices:** Number not currently connected
- **Pending Approval:** Devices awaiting admin approval

### Device List

The main device list displays:

- Device hostname
- Operating system (Windows/Linux)
- Current status (Online/Offline)
- IP address
- Last connection time
- CPU and memory usage snapshot

### Quick Actions

For each device:

- **View Details:** See full device information and metrics
- **Remote Desktop:** Start VNC session
- **SSH Terminal:** Open interactive shell
- **Execute Script:** Deploy script to device
- **Collect Metrics:** Get current system telemetry

## Filtering and Search

### Search Bar

Type device hostname or IP address to filter the device list in real-time.

### Status Filter

- Show All Devices
- Show Only Online
- Show Only Offline
- Show Only Pending

### Group Filter

Select a device group to show only devices in that group.

## Device Groups

Manage your devices by creating logical groups:

### Creating a Group

1. Navigate to **Devices** → **Groups**
2. Click **New Group**
3. Enter group name and optional description
4. Click **Create**

### Adding Devices to Groups

1. Navigate to **Devices** → Select group
2. Click **Add Devices**
3. Select devices to add
4. Click **Confirm**

### Bulk Actions on Groups

1. Select a group
2. Click **Execute Script** or **Collect Metrics**
3. All devices in group will execute the action

### Removing Devices from Groups

1. Navigate to group
2. Click the device
3. Click **Remove from Group**

## Real-Time Metrics

### System Information Card

When viewing a device, the metrics card shows:

- **CPU Usage:** Current processor utilization percentage
- **Memory Usage:** RAM in use as percentage
- **Disk Usage:** Storage usage per partition
- **Network Status:** Interface status (up/down)
- **Uptime:** How long the device has been running
- **OS:** Operating system version

### Refreshing Metrics

Click **Collect Metrics** to get fresh data immediately, or metrics auto-refresh every 5 minutes.

## Device Management

### Approving Devices

When a new agent registers:

1. Navigate to **Devices**
2. Find the device with status "Pending"
3. Click the device
4. Click **Approve**
5. Device becomes available for commands

### Removing Devices

To remove a device from management:

1. Select the device
2. Click **Delete**
3. Confirm deletion
4. Device is removed (can re-register later)

### Setting SSH Credentials

For devices requiring SSH access:

1. Select device
2. Click **Credentials**
3. Enter username
4. Choose authentication method:
   - **SSH Key:** Paste private key (recommended)
   - **Password:** Enter SSH password
5. Click **Save**

## Notifications

### Status Changes

- Device comes online/offline
- Job completes
- Scheduled script executes

### Action Items

- Devices pending approval
- Failed job execution
- Script deployment errors

## Performance Tips

### For Large Device Counts (100+)

1. **Use Groups:** Organize devices into logical groups to reduce visible device count
2. **Use Filters:** Filter to only view relevant devices
3. **Search:** Use hostname/IP search to quickly find specific device

### Dashboard Refresh Rates

- Device status: 30-60 seconds
- Metrics: 5 minutes
- Job results: Real-time

Next: Learn about [Device Management](./device-management.md) or [Remote Desktop](./remote-desktop.md)
