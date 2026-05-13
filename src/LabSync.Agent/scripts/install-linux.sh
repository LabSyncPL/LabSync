#!/usr/bin/env bash
set -euo pipefail

# LabSync Agent Linux installer (same role as install-agent.ps1 on Windows).
#
# Installs agent binaries, copies module DLLs into Modules/,
# configures AGENT_SERVER_URL in an environment file,
# and creates/starts a systemd service.
#
# Usage examples:
#   sudo chmod +x install-linux.sh && sudo ./install-linux.sh --server-url "http://SERVER:5038" --source-path /path/to/bundle
#   sudo ./install-linux.sh --server-url "http://192.168.1.10:5038" --source-path /path/to/repo

INSTALL_DIR="/opt/labsync-agent"
SOURCE_PATH="."
SERVER_URL="${SERVER_URL:-}"
SERVICE_NAME="labsync-agent"
ENV_FILE="/etc/labsync-agent/labsync-agent.env"
SKIP_BUILD="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --server-url)
      SERVER_URL="${2:-}"
      shift 2
      ;;
    --install-dir)
      INSTALL_DIR="${2:-}"
      shift 2
      ;;
    --source-path)
      SOURCE_PATH="${2:-}"
      shift 2
      ;;
    --service-name)
      SERVICE_NAME="${2:-}"
      shift 2
      ;;
    --env-file)
      ENV_FILE="${2:-}"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD="true"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ -z "$SERVER_URL" ]]; then
  read -r -p "Enter LabSync server URL (e.g. https://labsync.example.com): " SERVER_URL
fi

SERVER_URL="${SERVER_URL%/}"
if [[ -z "$SERVER_URL" ]]; then
  echo "Error: server URL is required." >&2
  exit 1
fi

if [[ $EUID -ne 0 ]]; then
  echo "Error: run as root (sudo)." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet runtime/sdk not found." >&2
  exit 1
fi

SOURCE_PATH="$(cd "$SOURCE_PATH" && pwd)"
TMP_PUBLISH_DIR=""
EFFECTIVE_SOURCE="$SOURCE_PATH"

build_from_repo() {
  local repo_root="$1"
  local publish_dir="$2"

  local agent_project="$repo_root/src/LabSync.Agent/LabSync.Agent.csproj"
  if [[ ! -f "$agent_project" ]]; then
    echo "Error: Agent project not found at $agent_project" >&2
    exit 1
  fi

  echo "[LabSync] Publishing agent from source..."
  dotnet publish "$agent_project" -c Release -o "$publish_dir" --self-contained false

  if [[ "$SKIP_BUILD" == "false" && -d "$repo_root/src/Modules" ]]; then
    shopt -s nullglob
    for module_dir in "$repo_root"/src/Modules/LabSync.Modules.*; do
      local module_name
      module_name="$(basename "$module_dir")"
      local module_proj="$module_dir/$module_name.csproj"
      if [[ -f "$module_proj" ]]; then
        echo "[LabSync] Building module $module_name..."
        dotnet build "$module_proj" -c Release
      fi
    done
    shopt -u nullglob
  fi
}

copy_module_dlls() {
  local source_root="$1"
  local modules_target="$2"
  local copied=0

  mkdir -p "$modules_target"

  shopt -s nullglob
  for dll in "$source_root"/Modules/*.dll; do
    cp -f "$dll" "$modules_target/"
    copied=$((copied + 1))
  done

  for module_dir in "$source_root"/src/Modules/LabSync.Modules.*; do
    for dll in "$module_dir"/bin/Release/net9.0/*.dll; do
      cp -f "$dll" "$modules_target/"
      copied=$((copied + 1))
    done
  done
  shopt -u nullglob

  if [[ $copied -eq 0 ]]; then
    echo "[LabSync] Warning: no module DLLs copied."
  else
    echo "[LabSync] Copied $copied module DLL files."
  fi
}

if [[ ! -f "$SOURCE_PATH/LabSync.Agent" && ! -f "$SOURCE_PATH/LabSync.Agent.dll" ]]; then
  if [[ -f "$SOURCE_PATH/src/LabSync.Agent/LabSync.Agent.csproj" ]]; then
    TMP_PUBLISH_DIR="$(mktemp -d /tmp/labsync-agent-publish.XXXXXX)"
    build_from_repo "$SOURCE_PATH" "$TMP_PUBLISH_DIR"
    EFFECTIVE_SOURCE="$TMP_PUBLISH_DIR"
  else
    echo "Error: source does not contain published agent binaries or repository root." >&2
    exit 1
  fi
fi

mkdir -p "$INSTALL_DIR"
cp -a "$EFFECTIVE_SOURCE"/. "$INSTALL_DIR"/

MODULES_DIR="$INSTALL_DIR/Modules"
copy_module_dlls "$SOURCE_PATH" "$MODULES_DIR"

mkdir -p "$(dirname "$ENV_FILE")"
printf 'AGENT_SERVER_URL=%q\n' "$SERVER_URL" > "$ENV_FILE"
chmod 640 "$ENV_FILE"

CONFIG_FILE="$INSTALL_DIR/appsettings.json"
if [[ -f "$CONFIG_FILE" ]]; then
  if command -v jq >/dev/null 2>&1; then
    jq --arg url "$SERVER_URL" '.ServerUrl = $url' "$CONFIG_FILE" > "$CONFIG_FILE.tmp" && mv "$CONFIG_FILE.tmp" "$CONFIG_FILE"
  fi
fi

if [[ -x "$INSTALL_DIR/LabSync.Agent" ]]; then
  EXEC_START="$INSTALL_DIR/LabSync.Agent"
elif [[ -f "$INSTALL_DIR/LabSync.Agent.dll" ]]; then
  EXEC_START="/usr/bin/dotnet $INSTALL_DIR/LabSync.Agent.dll"
else
  echo "Error: installed binary not found." >&2
  exit 1
fi

SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=LabSync Agent
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
WorkingDirectory=$INSTALL_DIR
EnvironmentFile=$ENV_FILE
ExecStart=$EXEC_START
Restart=always
RestartSec=5
User=root

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable --now "$SERVICE_NAME"

echo "[LabSync] Installation complete."
echo "[LabSync] Service: $SERVICE_NAME"
echo "[LabSync] AGENT_SERVER_URL: $SERVER_URL"

if [[ -n "$TMP_PUBLISH_DIR" && -d "$TMP_PUBLISH_DIR" ]]; then
  rm -rf "$TMP_PUBLISH_DIR"
fi
