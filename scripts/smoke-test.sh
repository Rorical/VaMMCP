#!/usr/bin/env bash
# Smoke test the running VaMMCP MCP endpoint with curl.
# Usage: ./scripts/smoke-test.sh [port]
set -euo pipefail
PORT="${1:-9837}"
URL="http://127.0.0.1:$PORT/mcp"
post() {
  curl -sS -X POST "$URL" -H 'Content-Type: application/json' -H 'Accept: application/json' -d "$1"
  echo
}
echo "== initialize =="
post '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}'
echo "== notifications/initialized =="
post '{"jsonrpc":"2.0","method":"notifications/initialized"}'
echo "== tools/list (count) =="
post '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | python3 -c "import json,sys; d=json.load(sys.stdin); print('tools:', len(d['result']['tools'])); print([t['name'] for t in d['result']['tools']][:10], '...')"
echo "== tools/call status =="
post '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"status","arguments":{}}}'
echo "== tools/call list_atoms =="
post '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"list_atoms","arguments":{}}}'
