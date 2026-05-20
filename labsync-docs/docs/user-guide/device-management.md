---
sidebar_position: 2
---

# Device Management

Device management is central to LabSync. This page explains how to register, approve, organize, and remove devices.

## Device Registration

Devices register automatically when the agent connects to the server for the first time.

### Registration Data

Each device sends:

- hostname
- operating system version
- IP address
- MAC address
- platform identifier (Windows/Linux)

### Pending Approval

New devices are created in the dashboard with status **Pending**. They cannot execute commands until approved by an administrator.

## Approving Devices

### Steps to Approve

1. Open **Devices** in the dashboard.
2. Locate the pending device in the list.
3. Click the device row to open details.
4. Click **Approve**.
5. The device will change to **Online** when connectivity is confirmed.

### What Approval Does

- Grants the device a JWT token
- Enables command execution
- Allows real-time telemetry
- Clears the pending state

## Device Details

### Key Information

A device detail page shows:

- hostname
- current status
- platform and OS version
- IP address and network details
- last seen timestamp
- SSH credentials status

### Action Buttons

- **Remote Desktop**
- **SSH Terminal**
- **Execute Script**
- **Collect Metrics**
- **Delete Device**

## Device Groups

Device groups allow you to perform actions on multiple devices at once.

### Create a New Group

1. Open **Devices** → **Groups**.
2. Click **New Group**.
3. Enter a name and optional description.
4. Click **Create**.

### Add Devices to a Group

1. Open the group.
2. Click **Add Devices**.
3. Select one or more devices.
4. Click **Confirm**.

### Remove a Device from a Group

1. Open the group.
2. Locate the device.
3. Click **Remove**.
4. Confirm the removal.

### Group Actions

From a device group you can:

- execute a script on all devices

## Device Credentials

The dashboard stores SSH credentials for devices.

### SSH Credentials Options

- **Password Authentication:** Username and password
- **Key Authentication:** Private key content stored securely

### Saving Credentials

1. Open device details.
2. Click **SSH Credentials**.
3. Enter username and authentication type.
4. Paste the private key or enter the password.
5. Click **Save**.

### Security Notes

- Credentials are encrypted in the database.
- Use SSH key authentication when possible.

## Removing a Device

### Steps to Remove

1. Open the device details page.
2. Click **Delete Device**.
3. Confirm deletion.

### Notes

- Removal does not delete agent software from the remote machine.
- The agent may re-register if it remains configured with the same server URL.

## Device Lifecycle

### New Device

- Registers
- Appears in pending list
- Awaits approval

### Approved Device

- Receives token
- Executes commands
- Reports telemetry

### Offline Device

- Still managed but not currently connected
- Can be returned to online state automatically when connection resumes

### Deleted Device

- Removed from dashboard
- Requires re-registration if agent remains active

## Best Practices

- Approve only known devices.
- Organize devices into logical groups by lab, department, or role.
- Keep device inventory clean by deleting unused entries.
- Verify device details before approving.

## Troubleshooting

### Device Stays Pending

- Verify agent configuration on the endpoint.
- Check network connectivity to the server.
- Confirm the server URL is correct.
- Restart the agent service.

### Device Does Not Appear Online

- Ensure the agent service is running.
- Check firewall rules for outbound connections.
- Confirm the JWT token was issued.
- Review server logs for connection errors.

---

Next: [Dashboard Guide](./dashboard) or [Remote Desktop](./remote-desktop)
