# MCP 客户端接入

*[English](clients.md)*

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
- "截个图看看"                   → capture_view（返回 Saves/PluginData/vam-mcp/preview.png；
                                   带 return_image=true 时直接返回图片本身）
```

若客户端与 VaM 不在同一台机器（或跑在容器里）则连不上：server 是有意只绑定 loopback 的。
确需穿透请自行做隧道，并先读 [SECURITY.md](../SECURITY.md)——**这里没有鉴权**。

## 端口冲突 / 改端口

编辑 `<VaM>/BepInEx/config/com.vammcp.core.cfg` 中的 `Server.Port`，重启 VaM 后客户端 URL 同步修改。

## 排查

| 现象 | 检查 |
| --- | --- |
| 客户端连不上 | VaM 在运行吗？`<VaM>/BepInEx/LogOutput.log` 里有 `VaMMCP ready` 吗？ |
| 日志里完全没有 VaMMCP | `VaMMCP.dll` 是否在 `<VaM>/BepInEx/plugins/`？BepInEx 本身有没有生成日志？ |
| 配置里 `Server.Enabled=false` | 改回 `true` 重启 VaM |
| 端口被占用 | 改 `Server.Port` 后重启 |
| 工具超时 | VaM 的场景/角色加载是异步的，过几秒再读一次 |
