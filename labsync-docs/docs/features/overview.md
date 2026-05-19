---
sidebar_position: 1
---

# Features Overview

LabSync provides a comprehensive suite of remote management capabilities designed for both educational and enterprise environments.

## Device Management

### Device Registration

Agents automatically register with the server upon first connection. The registration process includes:

- Device identification (hostname, MAC address, IP address)
- Platform detection (Windows or Linux)
- OS version information
- Automatic online/offline status tracking

Each device remains in "Pending" state until an administrator approves it through the dashboard. Once approved, the device receives a JWT token and can execute commands.

### Device Groups

Organize devices into logical groups for bulk operations:

- Create custom groups (e.g., "Lab A", "Accounting Department")
- Assign devices to multiple groups (if needed)
- Execute commands on entire groups with one click
- Automatic group-based job scheduling

### Device Credentials

Securely store SSH credentials for each device:

- Username/password storage (encrypted in database)
- SSH key management
- Support for both password and key-based authentication

## Remote Management

### Remote Desktop (VNC)

Stream the device screen in real-time to your browser:

- High-performance WebRTC video streaming (H.264 codec)
- Automatic GPU acceleration (Nvidia/AMD/Intel or software fallback)
- Mouse and keyboard control
- Configurable bitrate and resolution

**Use Cases:**

- Provide remote support to users
- Monitor laboratory computer screens
- Install software visually

### SSH Terminal

Interactive shell access to devices:

- Real-time command execution
- Terminal emulation (xterm)
- Automatic SSH key management
- Visual command output
- Window resize support

**Use Cases:**

- Server administration
- Configuration management
- Log file inspection

## Script Execution

### Script Management

Create and save reusable scripts:

- PowerShell (Windows)
- Bash (Linux)
- CMD (Windows)
- Rich text editor with syntax highlighting
- Organize scripts with descriptions and tags

### Execute Scripts

Run scripts on-demand or scheduled:

**On-Demand:**

1. Select device or group
2. Choose script
3. Execute immediately
4. Stream live output

**Features:**

- Real-time command output streaming
- Configurable timeout (default 300 seconds)
- Exit code tracking
- Full stdout/stderr capture

### Scheduled Execution

Automate recurring tasks:

- CRON-style scheduling (e.g., `0 9 * * MON` for Mondays at 9 AM)
- Execution history tracking
- Enable/disable schedules without deletion
- Group-based targeting
- Execution logs and results

**Use Cases:**

- Weekly system maintenance
- Monthly software updates
- Daily backup verification
- Nightly cleanup tasks

## System Monitoring

### Real-Time Telemetry

Collect device metrics on-demand or scheduled:

**Collected Metrics:**

- CPU usage and core count
- Memory (total, available, usage %)
- Disk space per partition
- Network interface status
- System uptime
- OS information (version, architecture)
- Hardware specifications (CPU model, RAM, GPU)

### Metric Dashboard

Visual display of device metrics:

- Device overview cards
- Online/offline status
- Last heartbeat time
- Quick metric snapshot
- Drill-down to detailed metrics

## Extensibility

### Module System

LabSync's agent is extensible through a plugin architecture:

- Core host process loads DLLs at startup
- Each module implements `IAgentModule` interface
- New functionality can be added without modifying core
- Modules initialized with dependency injection

### Current Modules

- **RemoteDesktop** - WebRTC VNC streaming
- **ScriptExecutor** - Multi-platform script execution
- **SSH** - Terminal and file transfer
- **SystemInfo** - Telemetry collection

### Extensibility Potential

- Custom modules for third-party integrations
- Specialized data collectors
- Custom job types
- Business logic plugins

## Performance Characteristics

### Scalability

- Agent connection limits determined by server resources
- Concurrent job execution per device
- Database scaling via PostgreSQL/TimescaleDB

### Latency

- Control plane: 50-100ms typical (SignalR)
- Data plane: {'<'}50ms typical (WebRTC)
- Signaling: {'<'}200ms offer/answer negotiation

### Resource Usage

- Agent memory: ~50-100MB idle
- Server memory: ~500MB-1GB (moderate load)
- Network: ~2-5 Mbps per video stream (H.264)
- Database: 100MB+ (depends on history retention)
