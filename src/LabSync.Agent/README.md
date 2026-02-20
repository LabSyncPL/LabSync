# LabSync Agent

Worker service that runs on managed devices, registers with the LabSync server, and executes jobs (e.g. scripts, installs) via SignalR.

## Configuration

The agent requires the **server API base URL** to be set before it can connect.

- **Configuration key:** `ServerUrl`
- **Sources (in order):** environment variables, then `appsettings.json`, then `appsettings.{Environment}.json`

Example `appsettings.json`:

```json
{
  "Logging": { "LogLevel": { "Default": "Information" } },
  "ServerUrl": "https://labsync.example.com"
}
```

Or set the environment variable:

- **Windows:** `$env:ServerUrl = "https://labsync.example.com"`
- **Linux:** `export ServerUrl="https://labsync.example.com"`

## Installer / First-run setup

When building an installer (e.g. .msi for Windows or a package for Linux):

1. During installation, prompt the user for the **LabSync server API address** (e.g. `https://labsync.contoso.com` or `http://192.168.1.10:5038`).
2. Persist that value in one of:
   - **appsettings.json** in the agent install directory (key `ServerUrl`), or
   - **Environment variable** `ServerUrl` (e.g. systemd environment file, or Windows machine/user env).

Example scripts (adapt to your installer):

- **Windows:** `scripts/install-windows.ps1.example` – prompts for server URL and writes `appsettings.json`.
- **Linux:** `scripts/install-linux.sh.example` – same for a Linux install directory.

After the URL is set, the agent will use it on startup to register and connect to the server.
