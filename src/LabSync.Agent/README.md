# LabSync Agent

Worker service that runs on managed devices, registers with the LabSync server, and executes jobs (e.g. scripts, installs) via SignalR.

## Configuration

The agent requires the **server API base URL** to be set before it can connect.

- **Preferred environment variable:** `AGENT_SERVER_URL`
- **Fallback config key:** `ServerUrl` in `appsettings.json`

Example `appsettings.json`:

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "ServerUrl": "https://labsync.example.com"
}
```

Or set the environment variable:

- **Windows:** `$env:AGENT_SERVER_URL = "https://labsync.example.com"`
- **Linux:** `export AGENT_SERVER_URL="https://labsync.example.com"`

## Installer / First-run setup

When building an installer (e.g. .msi for Windows or a package for Linux):

1. During installation, prompt the user for the **LabSync server API address** (e.g. `https://labsync.contoso.com` or `http://192.168.1.10:5000`).
2. Persist that value in one of:
   - **Environment variable** `AGENT_SERVER_URL` (recommended; e.g. systemd environment file, or Windows machine env), or
   - **appsettings.json** in the agent install directory (key `ServerUrl`).

Example scripts (adapt to your installer):

- **Windows:** `..\..\install-agent.ps1` (from repo root) or `scripts/install-windows.ps1.example`.
- **Linux:** `scripts/install-linux.sh`.

After the URL is set, the agent will use it on startup to register and connect to the server.
