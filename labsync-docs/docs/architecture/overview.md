---
sidebar_position: 1
---

# Architecture Overview

LabSync is designed with a modern, modular architecture that prioritizes security, scalability, and extensibility.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Browser / Dashboard                       │
│                      (React 19, TypeScript)                      │
└──────────────────┬──────────────────────────────────────────────┘
                   │ HTTPS / WebSocket
                   │
        ┌──────────▼──────────┐
        │   LabSync.Server    │
        │  (ASP.NET Core 9)   │
        │                     │
        ├─ REST API Endpoints │
        ├─ SignalR Hubs       │
        └────────┬────────────┘
                 │ SignalR / WebRTC Signaling
         ┌───────┴────────┐
         │                │
    ┌────▼─────┐    ┌────▼─────┐
    │ Device A │    │ Device B │
    │ (Agent)  │    │ (Agent)  │
    │ Windows  │    │ Linux    │
    │          │    │          │
    │┌────────┐│    │┌────────┐│
    ││ Remote ││    ││  SSH   ││
    ││Desktop ││    ││Module  ││
    │└────────┘│    │└────────┘│
    │┌────────┐│    │┌────────┐│
    ││  SSH   ││    ││ Remote ││
    ││Module  ││    ││Desktop ││
    │└────────┘│    │└────────┘│
    │┌────────┐│    │┌────────┐│
    ││ Script ││    ││ Script ││
    ││Executor││    ││Executor││
    │└────────┘│    │└────────┘│
    │┌────────┐│    │┌────────┐│
    ││System  ││    ││System  ││
    ││Info    ││    ││Info    ││
    │└────────┘│    │└────────┘│
    └──────────┘    └──────────┘
         │ WebRTC      │ SSH/SFTP
         │ (Video)     │ (Terminal)
         └─────┬───────┘
               │
         ┌─────▼─────────┐
         │  PostgreSQL   │
         │  Database     │
         │ (TimescaleDB) │
         └───────────────┘
```

## Core Concepts

### Control Plane vs. Data Plane

LabSync uses a dual-channel communication strategy:

**Control Plane (SignalR)**

- Lightweight message protocol (MessagePack)
- Used for commands, status updates, telemetry
- Low bandwidth, high reliability
- Persistent WebSocket connection

**Data Plane (WebRTC)**

- Dedicated channel for high-bandwidth media
- H.264 video streaming (RemoteDesktop)
- Bypasses server to reduce latency
- UDP-based, peer-to-peer

This separation ensures video streams never block critical commands.

### Micro-Kernel Agent Architecture

The agent is not monolithic but uses a plugin-based architecture:

```
┌────────────────────────────────────────────────────┐
│            LabSync.Agent (Host Process)            │
│                                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │              Module Loader                   │  │
│  │         (Discovers & Loads DLLs)             │  │
│  └────────────────┬─────────────────────────────┘  │
│                   │                                │
│      ┌────────────┼────────────┬────────────┐      │
│      │            │            │            │      │
│  ┌───▼────┐  ┌────▼────┐  ┌────▼────┐  ┌────▼────┐ │
│  │ Remote │  │ Script  │  │   SSH   │  │ System  │ │
│  │ Desktop│  │Executor │  │ Module  │  │  Info   │ │
│  └────────┘  └─────────┘  └─────────┘  └─────────┘ │
│                                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │        Dependency Injection Container        │  │
│  │      (Services, Logging, Configuration)      │  │
│  └──────────────────────────────────────────────┘  │
│                                                    │
│  ┌──────────────────────────────────────────────┐  │
│  │             SignalR Hub Invoker              │  │
│  │          (Server Communication Layer)        │  │
│  └──────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────┘
```

**Benefits:**

- New features added without core changes
- Modules can be developed independently
- Easy to disable/enable features
- Reduced memory footprint (load only needed modules)

## Component Overview

### Backend (Server)

**Technology:** ASP.NET Core 9, Entity Framework Core 9, PostgreSQL

**Responsibilities:**

- REST API for device/job management
- SignalR hubs for real-time communication
- Database persistence
- JWT authentication and authorization
- Job dispatch and tracking

**Key Controllers:**

- `AgentsController` - Device registration
- `DevicesController` - Device management and jobs
- `DeviceGroupsController` - Group management
- `SavedScriptsController` - Script storage
- `ScriptSchedulerController` - Schedule management
- `AuthController` - Authentication
- `SystemController` - System setup

### Frontend (Client)

**Technology:** React 19, TypeScript, Vite, Tailwind CSS

**Responsibilities:**

- User authentication interface
- Device management dashboard
- Remote desktop viewer
- Script editor and deployment
- System metrics visualization
- Real-time status updates

**Key Pages:**

- Dashboard - Device overview and monitoring
- Device Details - Single device information
- Remote View - WebRTC VNC viewer
- Scripts - Script management and deployment
- Tasks - Job execution history
- Settings - Configuration

### Database

**Technology:** PostgreSQL 15, TimescaleDB (for time-series data)

**Key Entities:**

- `Device` - Registered managed computers
- `DeviceGroup` - Logical device grouping
- `Job` - Script/command execution records
- `SavedScript` - Reusable script templates
- `ScheduledScript` - Recurring execution schedules
- `AdminUser` - User accounts
- `DeviceCredentials` - SSH credential storage
- `AgentLog` - Audit trail

### Agent Host

**Technology:** .NET 9 Worker Service

**Responsibilities:**

- Module lifecycle management
- Server registration and authentication
- Command routing to appropriate module
- Result aggregation and reporting
- Background operation processing

**Deployment:**

- Windows Service (on Windows)
- Systemd Service (on Linux)
- Automatic startup on reboot

## Communication Flow

### Job Execution Flow

```
1. Admin clicks "Execute Script" in Dashboard
   │
2. Browser sends: POST /api/devices/{id}/jobs
   │
3. Server creates Job record (Status: Pending)
   │
4. Server sends via SignalR AgentHub:
   │ ExecuteJob(jobId, command, parameters)
   │
5. Agent receives command
   │
6. Agent selects appropriate module
   │ (ScriptExecutor for "RunScript" command)
   │
7. Module executes script
   │ ├─ Create temp file
   │ ├─ Execute process
   │ ├─ Capture output
   │ └─ Collect results
   │
8. Agent sends: JobCompleted(jobId, exitCode, output)
   │
9. Server updates Job record (Status: Completed)
   │
10. Browser polls API and displays results
```

### Remote Desktop Session Flow

```
1. User clicks "Remote Desktop" in Dashboard
   │
2. Browser connects to RemoteDesktopHub (SignalR)
   │
3. Server sends: InitiateRemoteDesktop(sessionId)
   │
4. Agent receives, creates RemoteSession
   │ ├─ Start screen capture
   │ ├─ Initialize video encoder (GPU detection)
   │ ├─ Create WebRTC peer connection
   │ └─ Generate SDP offer
   │
5. Agent sends via SignalR:
   │ SendOffer(offer, candidates)
   │
6. Server forwards to Browser via SignalR
   │
7. Browser receives offer
   │ ├─ Create WebRTC peer connection
   │ ├─ Process ICE candidates
   │ └─ Generate SDP answer
   │
8. Browser sends: SendAnswer(answer)
   │
9. Agent receives answer
   │ ├─ Set remote description
   │ └─ Start video streaming (UDP)
   │
10. Video stream establishes
    ├─ H.264 frames via RTP
    ├─ Mouse/keyboard input channel (data channel)
    └─ ICE connectivity checks
```

## Security Architecture

### Authentication Pipeline

```
Device Registration:
├─ Device sends: POST /api/agents/register
│  with (hostname, macAddress, platform)
├─ Server creates Device record (IsApproved: false)
├─ Response: Device waits for approval
│
Admin Approval:
├─ Admin logs in (JWT issued for 8 hours)
├─ Admin clicks "Approve" in dashboard
├─ Server updates Device.IsApproved = true
│
Device Reconnection:
├─ Agent sends: POST /api/agents/register (same device)
├─ Server checks IsApproved flag
├─ If true: Issues JWT token
├─ Agent uses JWT for all SignalR connections
```

### No-Eval Policy

The agent NEVER executes arbitrary code strings. Instead:

```
Server Request:
{
  command: "ScriptExecution",
  arguments: {
    scriptContent: "Get-Date",
    interpreter: "PowerShell",
    timeout: "300"
  }
}
   │
   ▼
Agent Processing:
├─ Validate interpreter (must be PowerShell/Bash/CMD)
├─ Create temp script file
├─ Build ProcessStartInfo
│  ├─ FileName: "powershell.exe"
│  ├─ Arguments: "-File temp_file.ps1"
│  ├─ UseShellExecute: false (NO SHELL!)
│  └─ RedirectStandardOutput: true
├─ Execute process (NOT through shell)
├─ Capture output (UTF-8 decoded)
└─ Return results (no code evaluation)
```

This prevents:

- Shell injection attacks
- Command chaining (`; rm -rf /`)
- Variable expansion
- Command substitution

## Module Interface

All modules implement `IAgentModule`:

```csharp
public interface IAgentModule
{
    string Name { get; }                    // Module name
    string Version { get; }                 // Version string

    Task InitializeAsync(IServiceProvider sp);  // Startup

    bool CanHandle(string jobType);         // Can this module handle job?

    Task<ModuleResult> ExecuteAsync(
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken);  // Execute job
}
```

**Lifecycle:**

1. Agent loads DLL at startup
2. Calls `InitializeAsync()` with DI container
3. Module registers event handlers or services
4. Agent calls `CanHandle()` to check if module processes job
5. Agent calls `ExecuteAsync()` if `CanHandle()` returns true
6. Module processes and returns `ModuleResult`

## Performance Considerations

### Scalability Limits

**Single Server Instance:**

- Concurrent agents: 1000+ (depends on hardware)
- Concurrent video streams: 50-100 (depends on bandwidth)
- Job throughput: 100+ jobs/second
- Database: PostgreSQL handles 10,000+ transactions/sec

**Scaling Strategy:**

- Horizontal: Load balance multiple server instances
- Database: PostgreSQL streaming replication
- Cache: Redis for session state (future)

### Optimization Techniques

**Video Streaming:**

- GPU acceleration (H.264 via ffmpeg)
- Adaptive bitrate (future)
- Key frame insertion on demand
- RTP level fragmentation

**Script Execution:**

- Parallel job processing
- Process timeout management
- Output buffering via channels
- UTF-8 standardization

**Remote Shell:**

- Session multiplexing (multiple sessions per connection)
- Terminal output batching
- Keep-alive heartbeats
