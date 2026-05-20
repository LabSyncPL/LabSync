---
sidebar_position: 5
---

# SSH Terminal Guide

The SSH terminal provides interactive shell access to devices managed by LabSync.

## Accessing the SSH Terminal

### Prerequisites

- Device must be online.
- SSH service must be running on the device.
- SSH credentials must be configured.

### Starting a Session

1. Navigate to **Devices**.
2. Select device.
3. Click **SSH Terminal**.
4. The browser opens an interactive shell.

## Terminal Features

### Command Execution

Type commands directly and press Enter. The terminal supports:

- standard shell commands
- shell scripts
- command history
- tab completion (browser-dependent)

### Output View

- Displays stdout and stderr
- Supports line wrapping and scrolling
- Updates in real time

### Copy and Paste

- Copy text from terminal output normally.
- Paste commands into the input prompt.

## SSH Credentials Configuration

### Types of Credentials

- **Password authentication**
- **SSH key authentication** (recommended)

### Entering Credentials

1. Open device details.
2. Click **SSH Credentials**.
3. Enter username.
4. Provide either a password or private key.
5. Save credentials.

### SSH Key Best Practices

- Use RSA 4096-bit keys.
- Protect the private key with strong file permissions.
- Prefer key authentication over passwords.

## Common SSH Commands

### System Status

```bash
uname -a
hostname
uptime
```

### Resource Usage

```bash
top -b -n 1 | head -20
free -h
df -h
```

### File Inspection

```bash
cat /etc/os-release
ls -la /var/log
sudo journalctl -n 50
```

### Package Management

```bash
sudo apt update
sudo apt upgrade
```

## Session Management

### Disconnecting

- Close the browser tab.
- The SSH session will terminate automatically.
- For a clean end, type `exit` or `logout`.

### Reconnecting

- Return to the same device and open SSH Terminal again.
- The session starts a new shell instance.

### Terminal Does Not Open

- Confirm the device is online.
- Verify SSH credentials are configured.
- Check the SSH daemon on the device.
- Review the device logs for connection failures.

### Authentication Failed

- Verify username and password.
- Ensure the private key is valid.
- Check that the key is authorized on the target host.

### Slow Terminal Response

- Check network latency.
- Verify remote device CPU usage.
- Use a command like `top` to identify load.

### Lost Connection

- The device may have gone offline.
- SSH service may have restarted.
- Reopen the terminal session after verifying connectivity.

---

Next: [Device Management](./device-management) or [Scheduling](./scheduling)
