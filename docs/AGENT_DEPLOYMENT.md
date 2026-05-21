# LabSync Agent — packaging and installation

This guide describes how to **build a deployment bundle** on a developer or CI machine (with the Git repository) and how to **install and configure** the agent on **Windows** and **Linux** target machines (no repository required).

---

## Concepts

| Term | Meaning |
|------|---------|
| **Build machine** | PC or CI runner with the repo, .NET 9 SDK, and `pack-agent-release.ps1`. |
| **Target machine** | PC where the agent runs; receives only the extracted bundle (or ZIP). |
| **Framework-dependent (fx-dep)** | Smaller bundle; **.NET 9 runtime** must be installed on the target. |
| **Self-contained** | Larger bundle; includes the .NET runtime; **no separate dotnet install** on the target. |
| **`AGENT_SERVER_URL`** | Base URL of the LabSync **Server API** (HTTP(S) only, no path suffix). Example: `http://192.168.0.12:5000`. |
| **`ServerUrl`** | Optional fallback in `appsettings.json`; prefer **`AGENT_SERVER_URL`** for services. |

The agent registers at `POST {AGENT_SERVER_URL}/api/agents/register` and connects to SignalR at `{AGENT_SERVER_URL}/agentHub`.

---

## 1. Packaging (build machine)

Run from the **repository root** (where `pack-agent-release.ps1` lives).

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PowerShell (Windows; used to run the pack script)

### Script: `pack-agent-release.ps1`

**Typical outputs**

- Folder: `dist/LabSync.Agent-release-<rid>-<fx-dep|self-contained>/`
- Optional ZIP: same path with `.zip` if `-Zip` is used
- Inside: published agent binaries, `Modules/*.dll`, `install-agent.ps1`, `install-linux.sh`, `README-DEPLOY.txt`

### Parameters

| Parameter | Description |
|-----------|-------------|
| `-RepoRoot` | Repository root. Default: folder containing the script. |
| `-OutputDir` | Explicit output folder (optional). |
| `-RuntimeIdentifier` | `win-x64` (default), `linux-x64`, `linux-arm64`, etc. |
| `-SelfContained` | Switch: bundle includes .NET runtime (larger). |
| `-Zip` | Switch: also create a `.zip` next to the output folder. |

### Examples

**Windows target, framework-dependent (small ZIP; requires .NET 9 on target):**

```powershell
.\pack-agent-release.ps1 -RuntimeIdentifier win-x64 -Zip
```

**Linux x64 target, framework-dependent:**

```powershell
.\pack-agent-release.ps1 -RuntimeIdentifier linux-x64 -Zip
```

**Windows target, self-contained (no dotnet install on target):**

```powershell
.\pack-agent-release.ps1 -RuntimeIdentifier win-x64 -SelfContained -Zip
```

**Custom output folder:**

```powershell
.\pack-agent-release.ps1 -RuntimeIdentifier win-x64 -OutputDir "C:\artifacts\LabSyncAgent" -Zip
```

---

## 2. Windows — installing on the target machine

### Prerequisites on target

- **Framework-dependent bundle:** install [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (Desktop / suitable variant for your worker).
- Administrator rights for service installation.
- Copy the extracted bundle folder (or unzip the release ZIP).

### Configuration value to set

Use the **LabSync Server base URL**, including scheme and port, for example:

```text
http://192.168.0.12:5000
https://labsync.example.com
```

If you omit `http://` or `https://`, `install-agent.ps1` prepends `http://`.

### Installation commands

Open **PowerShell as Administrator**, go to the bundle folder, then either:

**Standard:**

```powershell
cd <path-to-extracted-bundle>
.\install-agent.ps1 -ServerUrl "http://YOUR_SERVER:5000" -SourcePath "."
```

**If scripts are blocked (Execution Policy):**

```powershell
cd <path-to-extracted-bundle>
powershell -ExecutionPolicy Bypass -File ".\install-agent.ps1" -ServerUrl "http://YOUR_SERVER:5000" -SourcePath "."
```

Optional parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-InstallDir` | `C:\Program Files\LabSync.Agent` | Installation directory. |
| `-SourcePath` | `.` | Folder containing published agent + `Modules` (the bundle root). |
| `-ServiceName` | `LabSyncAgent` | Windows service name. |

The script:

- Copies binaries and module DLLs into `InstallDir`
- Sets **machine** environment variable **`AGENT_SERVER_URL`**
- Updates **`ServerUrl`** in `appsettings.json` under `InstallDir`
- Creates and starts the **LabSync Agent** Windows service (`Automatic` start)

### Changing the server URL later

```powershell
[Environment]::SetEnvironmentVariable("AGENT_SERVER_URL", "http://NEW_SERVER:5000", "Machine")
Restart-Service LabSyncAgent
```

Edit `C:\Program Files\LabSync.Agent\appsettings.json` → `ServerUrl` to match if you use it as fallback.

---

## 3. Linux — installing on the target machine

### Prerequisites on target

- **Framework-dependent bundle:** install **.NET 9 runtime** for your distro (same major as the project, e.g. `dotnet-runtime-9.0` where available).
- `sudo`, `bash`, `systemd`

### Configuration

Same as Windows: **`AGENT_SERVER_URL`** (written to an env file consumed by systemd, default `/etc/labsync-agent/labsync-agent.env`).

### Installation commands

Use **`install-linux.sh`** from the release bundle or from `src/LabSync.Agent/scripts/` in the repository:

```bash
sudo chmod +x install-linux.sh
sudo ./install-linux.sh --server-url "http://YOUR_SERVER:5000" --source-path "/full/path/to/bundle-or-repo"
```

Useful options:

| Option | Default | Description |
|--------|---------|-------------|
| `--server-url` | *(prompt)* | LabSync Server base URL. |
| `--install-dir` | `/opt/labsync-agent` | Install directory. |
| `--source-path` | `.` | Bundle root **or** repository root (script can publish/build). |
| `--service-name` | `labsync-agent` | systemd unit name. |
| `--env-file` | `/etc/labsync-agent/labsync-agent.env` | Environment file for `AGENT_SERVER_URL`. |
| `--skip-build` | off | Skip building modules from repo when using repo as source. |

After install, manage the service:

```bash
sudo systemctl status labsync-agent
sudo systemctl restart labsync-agent
```

---

## 4. Server-side approval (dashboard)

New agents appear in the database after a successful **`POST /api/agents/register`**. An administrator must **approve** the device in the web UI before the agent receives a JWT and connects to SignalR. Until then the agent retries registration periodically.

Use dashboard filters **All** or **Pending** to see unapproved devices.

---

## 5. Quick troubleshooting

| Issue | What to check |
|-------|----------------|
| Script blocked on Windows | Use `-ExecutionPolicy Bypass` (see above). |
| No device row on server | Network/firewall to server port; correct **`AGENT_SERVER_URL`** (server IP/hostname); server listens on `0.0.0.0`, not only localhost. |
| Service fails to start | .NET 9 runtime installed (fx-dep); Event Viewer → Application logs. |
| IP changed | **`AGENT_SERVER_URL`** must point to the **server**, not the agent; update env + restart agent service. |

---

## File reference (repository)

| Path | Role |
|------|------|
| `pack-agent-release.ps1` | Builds the deployment folder/ZIP. |
| `install-agent.ps1` | Windows installer / service registration. |
| `src/LabSync.Agent/scripts/install-linux.sh` | Linux installer (also copied into release bundles as `install-linux.sh`). |
