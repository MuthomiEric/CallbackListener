#!/usr/bin/env bash
set -euo pipefail

SERVER="https://callback.erickmuthomi.dev"
API_KEY=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-key) API_KEY="$2"; shift 2 ;;
    --server)  SERVER="$2";  shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if [[ -z "$API_KEY" ]]; then
  echo "Error: --api-key is required" >&2
  echo "Usage: curl -fsSL $SERVER/install.sh | sudo bash -s -- --api-key YOUR_KEY" >&2
  exit 1
fi

ARCH=$(uname -m)
case "$ARCH" in
  x86_64)          ARCH_SLUG="x64"   ;;
  aarch64 | arm64) ARCH_SLUG="arm64" ;;
  *)
    echo "Unsupported architecture: $ARCH" >&2
    echo "Download the agent manually from $SERVER/downloads/" >&2
    exit 1
    ;;
esac

BINARY_URL="$SERVER/downloads/CallbackAgent-linux-$ARCH_SLUG"
INSTALL_PATH="/usr/local/bin/CallbackAgent"

echo "Downloading CallbackAgent (linux-$ARCH_SLUG)..."
if ! curl -fsSL "$BINARY_URL" -o "$INSTALL_PATH"; then
  echo "Download failed. Visit $SERVER/downloads/ for manual installation." >&2
  exit 1
fi

chmod +x "$INSTALL_PATH"
echo "Installing as system service..."
"$INSTALL_PATH" install --server "$SERVER" --api-key "$API_KEY"
