# VaMMCP — Virt-A-Mate × MCP

让任何 MCP 客户端（Claude Code / Codex / Cursor / Claude Desktop / …）通过自然语言**完整控制 VaM**：
搭场景、加 Atom、捏人（Morph）、换外观/服装/发型、摆姿势、做表情、控制骨骼、调任意参数、
操作相机截图、浏览/下载 Hub 社区资源、挂载配置 VaM 插件、皮肤次表面反射等。

**MCP server 直接内置于 BepInEx 插件进程**，走 Streamable HTTP 传输（规范 2025-06-18），
Agent 直连 `http://127.0.0.1:9837/mcp`，无需 Python、无外部进程、无轮询文件。

```
MCP 客户端 ──HTTP/JSON-RPC──▶ VaMMCP (BepInEx 插件, VaM 进程内)
                               ├─ 后台线程: 最小 HTTP server (仅绑定 127.0.0.1, Origin 校验)
                               ├─ JSON-RPC / MCP 协议层 (initialize / tools/list / tools/call / resources)
                               ├─ 主线程调度器 (队列 → Unity Update)
                               └─ VaM 控制层 (SuperController / Atom / JSONStorable / DAZMorph / MVRPlugin …)
```

> ⚠️ 本项目是**非官方**社区项目，与 Mesh VR / Virt-A-Mate / VaM Hub 无任何关联。
> 请仅在你合法拥有的 VaM 副本上使用，并遵守 VaM 的 EULA 与 Hub 内容许可。

## ✨ 功能一览（63 个工具）

| 分类 | 工具 |
| --- | --- |
| 状态 | `status` |
| 场景 | `list_scenes` `load_scene` `new_scene` `save_scene` |
| Atom | `list_atom_types` `list_atoms` `add_atom` `remove_atom` `set_atom_on` `get_atom_transform` `set_atom_transform` |
| 人物 | `add_person` `list_persons` `list_looks` `load_look` `save_look` `set_character` |
| 预设导出 | `save_look`（外观，含服装/发型）`save_pose` `save_full_preset`（Full） |
| 捏人 | `list_morphs` `set_morph` `get_morph` `reset_morphs` |
| 服装/发型 | `list_packages` `list_clothing_presets`（扫描磁盘+全部 .var 包）`load_clothing_preset` `list_clothing_items` `add_clothing_item` `remove_clothing_item` `set_clothing_item_on` `list_hair_presets` `load_hair_preset` `list_hair_items` `add_hair_item` `remove_hair_item` `set_hair_item_on` |
| 姿态/表情 | `list_poses` `load_pose` `save_pose` `list_expressions` `set_expression` |
| 骨骼控制 | `list_controls` `get_control` `set_control` `set_gaze` |
| 通用参数（长尾全覆盖） | `list_atom_storables` `list_storable_params` `get_param` `set_param` `call_action` |
| 相机 | `get_camera` `set_camera` `capture_view` |
| 模拟 | `set_simulation` `reset_simulation` |
| Hub 社区资源 | `hub_browse` `hub_detail` `hub_download` |
| 皮肤 | `set_skin_sss`（次表面反射 `_SubdermisColor`） |
| 插件管理 | `list_plugins` `add_plugin` `remove_plugin` |
| 逃生舱（默认关） | `eval_cs` |

**设计要点**：任何 VaM UI 滑块本质上都是 JSONStorable 参数——
`list_storable_params` 枚举 → `set_param` 设置，这就是"全面"的来源；
Morph 是捏人核心（`DAZMorph.morphValue`）；插件加载后即成为 atom 的 storable，通用层直接覆盖。

## 📦 环境要求

- Virt-A-Mate 1.20+（开发验证于 1.22.0.13 / Unity 2018.1.9f2 / Mono）
- BepInEx 5.4.x（x64）已安装到 VaM 根目录（`winhttp.dll` + `BepInEx/`）
- 构建：.NET SDK（在 WSL2 + dotnet 10 下交叉编译通过；**目标 net35**——VaM 运行在 .NET 3.5 API 级别）

## 🚀 安装

```bash
# 1) 安装 BepInEx（若尚未安装；只新增文件，不覆盖任何现有文件）
./scripts/install-bepinex.sh            # 或设 VAM_ROOT 指向 VaM 目录

# 2) 构建并部署插件
./scripts/deploy.sh
# 产物: <VaM>/BepInEx/plugins/VaMMCP.dll

# 3) 启动 VaM。查看日志:
#     <VaM>/BepInEx/LogOutput.log  应出现 "VaMMCP ready. MCP endpoint: ..."
```

仓库放在 VaM 目录内时开箱即用；放在别处时设置环境变量/构建参数：

```bash
export VAM_ROOT="D:/path/to/VaM"       # scripts 读取
# 或: dotnet build -p:VaMRoot=D:/path/to/VaM
```

## 🔌 接入 MCP 客户端

核心配置（VaM 运行期间）：

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

```bash
claude mcp add vam --transport http http://127.0.0.1:9837/mcp
codex mcp add vam --transport http http://127.0.0.1:9837/mcp
```

详见 [docs/clients.md](docs/clients.md)。冒烟测试：`./scripts/smoke-test.sh`。

## ⚙️ 配置（BepInEx/config/com.vammcp.core.cfg）

| 键 | 默认 | 说明 |
| --- | --- | --- |
| Server.Enabled | true | 是否随 VaM 启动 MCP server |
| Server.Port | 9837 | 监听端口（仅绑定 127.0.0.1） |
| Security.AllowEval | false | 是否开放 `eval_cs`（进程内执行任意 C#，高危） |
| Security.EvalTimeoutSec | 30 | eval 超时（秒） |

## 🧱 架构与开发

```
src/
├── Plugin.cs              # BepInEx 入口、配置、主线程调度
├── MainThread.cs          # HTTP 线程 → Unity 主线程的队列调度器（同步等待）
├── Util.cs                # 日志 / ApiError
├── Mcp/
│   ├── HttpServer.cs      # 最小 HTTP/1.1 server（Streamable HTTP 传输、Origin 校验、keep-alive）
│   ├── McpServer.cs       # MCP 协议：initialize / tools/list / tools/call / resources
│   ├── Tool.cs            # 工具定义（名称/描述/JSON Schema/处理器/超时）
│   └── ToolRegistry.cs    # 63 个工具注册
└── Api/
    └── VaMApi.cs          # VaM 控制层（全部工具实现）
```

- 参考：VaM 安装目录的 `src2/` 是 Assembly-CSharp 反编译源码，查 API 直接翻它
- 详见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)

## 🔒 安全说明

- HTTP server 只绑定 `127.0.0.1`，并校验 Origin 头（防 DNS rebinding）
- `eval_cs` 默认关闭；开启等于给 Agent 本机任意代码执行权（且受 VaM 运行时沙箱限制：System.IO / System.Reflection / System.AppDomain / UnityEditor / Mono.Cecil 禁用）
- 下载 Hub 内容请遵守 Hub 的内容许可；付费内容需要你在 VaM 内登录 Hub 账户

## 📄 许可证

[MIT](LICENSE)。本项目按"现状"提供，作者不对任何用途承担担保或责任。
