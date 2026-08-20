# Connecting MCP clients

*[中文版](clients.zh-CN.md)*

With VaM running and the VaMMCP plugin loaded, the MCP endpoint is:

```
http://127.0.0.1:9837/mcp
```

## Claude Code

```bash
claude mcp add vam --transport http http://127.0.0.1:9837/mcp
```

Or in a project's `.mcp.json`:

```json
{
  "mcpServers": {
    "vam": {
      "type": "http",
      "url": "http://127.0.0.1:9837/mcp"
    }
  }
}
```

## Codex CLI

```bash
codex mcp add vam --transport http http://127.0.0.1:9837/mcp
```

Or in `~/.codex/config.toml`:

```toml
[mcp_servers.vam]
transport = "http"
url = "http://127.0.0.1:9837/mcp"
```

## Cursor

Settings → MCP → Add new MCP server, type HTTP, URL `http://127.0.0.1:9837/mcp`.

## Claude Desktop

`%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "vam": {
      "type": "http",
      "url": "http://127.0.0.1:9837/mcp"
    }
  }
}
```

## Verify

The client should show a `vam` server with its tool list (`status`, `list_scenes`, `list_atoms`,
`set_morph`, …). Things to try:

```
"What's in the scene right now?"          → status / list_atoms
"New scene, add a female character"       → new_scene → add_person → (wait a few seconds) list_persons
"Make her thighs a bit thicker"           → list_morphs (search=thigh) → set_morph
"Have her sit down and smile"             → list_poses → load_pose → set_expression
"Show me what it looks like"              → capture_view (returns Saves/PluginData/vam-mcp/preview.png,
                                             or the image itself with return_image=true)
```

If your client runs on a different machine from VaM (or inside a container), the endpoint is not
reachable: the server binds to loopback on purpose. Tunnel it yourself if you need to, and read
[SECURITY.md](../SECURITY.md) first — there is no authentication.

## Port conflicts / changing the port

Edit `Server.Port` in `<VaM>/BepInEx/config/com.vammcp.core.cfg`, restart VaM, and update the URL
in your client.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Client cannot connect | Is VaM running? Does `<VaM>/BepInEx/LogOutput.log` contain `VaMMCP ready`? |
| No `VaMMCP` line in the log at all | Is `VaMMCP.dll` in `<VaM>/BepInEx/plugins/`? Is BepInEx itself loading (any log file at all)? |
| `Server.Enabled=false` in the config | Set it back to `true` and restart VaM |
| Port already in use | Change `Server.Port`, restart |
| Tools time out | Some VaM operations are asynchronous (scene load, character load); retry the read a few seconds later |
