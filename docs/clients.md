# MCP 客户端接入

VaM 运行且 VaMMCP 插件加载后，MCP 端点为：

```
http://127.0.0.1:9837/mcp
```

## Claude Code

```bash
claude mcp add vam --transport http http://127.0.0.1:9837/mcp
```

或写入项目 `.mcp.json`：

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

或 `~/.codex/config.toml`：

```toml
[mcp_servers.vam]
transport = "http"
url = "http://127.0.0.1:9837/mcp"
```

## Cursor

Settings → MCP → Add new MCP server，类型选 HTTP，URL 填 `http://127.0.0.1:9837/mcp`。

## Claude Desktop

`%APPDATA%\Claude\claude_desktop_config.json`：

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

## 验证

客户端里看到 `vam` server 与工具列表（`status`、`list_scenes`、`list_atoms`、`set_morph`、…）即成功。

```
可用提示词：
- "现在场景里有什么？"            → status / list_atoms
- "新建一个场景，加一个女性角色"    → new_scene → add_person → (等几秒) list_persons
- "把她的大腿调粗一点"            → list_morphs (search=thigh) → set_morph
- "让她坐下，面带微笑"            → list_poses → load_pose → set_expression
- "截个图看看"                   → capture_view（返回 Saves/PluginData/vam-mcp/preview.png）
```

## 端口冲突 / 改端口

编辑 `<VaM>/BepInEx/config/com.vammcp.core.cfg` 中的 `Server.Port`，重启 VaM 后客户端 URL 同步修改。
