# 开发指南

*[English](DEVELOPMENT.md)*

## 构建

```bash
# 需要 .NET SDK（任意近期版本即可交叉编译）
./scripts/deploy.sh            # 构建 + 部署到 <VAM_ROOT>/BepInEx/plugins/
# 或手动:
dotnet build src/VaMMCP.csproj -c Release
```

### 关键构建事实

- **TargetFramework = net35**：VaM 运行在 Unity 2018.1 的 .NET 3.5 API 级别（BepInEx 日志里 `CLR runtime version: 2.0.50727`）。net46+ 程序集会因缺少 ConcurrentQueue 等 API 而加载失败（本仓库第一版就踩过）。
- **C# LangVersion 7.3**：够用且兼容旧工具链。
- **引用**：
  - `VaM_Data/Managed/Assembly-CSharp.dll`（通过 `VaMRoot` MSBuild 属性定位，默认 `..\..`，即仓库放在 VaM 目录内）
  - `BepInEx.Core 5.4.21`（nuget.bepinex.dev）
  - `UnityEngine.Modules 2018.1.9`（nuget.org，Unity 2018.1.9 模块程序集）
  - `Microsoft.NETFramework.ReferenceAssemblies`（Linux/macOS 交叉编译 net35 必需）
- **NuGet 缓存**：`scripts/deploy.sh` 会把 HOME/NUGET_PACKAGES 指到仓库内的 `.home/` 与 `.nuget/`（CI/沙箱 HOME 只读时必需）。

### 为什么 CI 不编译插件

`Assembly-CSharp.dll` 随 VaM 分发、不可再分发，公共 runner 上没有可编译的引用。所以
[`ci.yml`](../.github/workflows/ci.yml) 只跑不依赖它的部分（restore、shell lint、文档与注册表一致性），
仅当仓库配置了指向私有副本的 `VAM_ASSEMBLY_URL` secret 时才做真实构建；release 的二进制由
`scripts/release.sh` 在本地构建后上传。

## 运行时架构

```
HTTP 工作线程 (TcpListener @127.0.0.1:9837)
  └─ McpServer.HandleHttp → JSON-RPC → tool.Handler
       └─ MainThreadDispatcher.Run(fn, timeout)   ← 阻塞等待
            └─ Plugin.Update() 每帧 drain 队列 → fn 在 Unity 主线程执行
```

- **所有 VaM/Unity API 只能在主线程访问**；工具处理器默认经 `Run()` 包装。
- **轮询式工具**（`Tool.NeedsPoller=true`，如 `add_atom`/`add_person`/`add_plugin`/`hub_*`/`add_clothing_item`）在 HTTP 线程执行，内部用 `Mt.Run` 分片调用主线程——因为 VaM 的创建/加载是异步协程，需要轮询完成状态。
- **HTTP 协议**：MCP Streamable HTTP（2025-06-18）。POST /mcp → JSON-RPC 响应（application/json）；GET → 405（规范允许，表示不提供 SSE 流）；通知 → 202。仅绑定 loopback + Origin 校验（DNS rebinding 防护），并发连接上限 32。所有响应带 Content-Length，支持 keep-alive。
- **图片**：工具返回的 JSON 里带 `image_base64`/`image_mime` 时，`McpServer.DoToolsCall` 会把它们摘出来转成 MCP image content 块（见 `capture_view`）。

## 调试

- 插件日志：`<VaM>/BepInEx/LogOutput.log`（`Log.Info/Warn/Error`；`Log.Debug` 默认被 BepInEx 过滤，需在 `BepInEx/config/BepInEx.cfg` 调日志级别）
- VaM 自身日志：`%USERPROFILE%\AppData\LocalLow\MeshedVR\VaM\output_log.txt`（编译错误、Save 日志都在这里）
- 冒烟测试：`./scripts/smoke-test.sh`（握手 + tools/list + status + list_atoms）

## 已知限制与坑（踩坑记录）

| 坑 | 说明 / 对策 |
| --- | --- |
| net46 程序集无法加载 | VaM 是 .NET 3.5 级别，见上 |
| 覆盖保存静默失败 | VaM 对插件"覆盖已存在文件"弹确认框，无人点击则**不保存**。`PrepareSavePath` 用 System.IO 先删旧文件再存 |
| 预设保存不含完整材质参数 | `save_look`/`save_full_preset` 的 skin storable 只存了少量参数（VaM 的 Atom 预设机制如此）。跨场景搬运后需按需用 `set_param` 补设皮肤参数；**场景保存（save_scene）是完整的** |
| `new_scene` 空场景无灯 | VaM 默认场景靠 `3PointLightSetup`；空场景需要自行加灯，且 `GlobalLighting`（masterIntensity 默认 0.1、环境色默认黑）参数**不随场景保存**，每次加载场景后需重设 |
| `add_clothing_item` 找不到 DAZRuntimeCreator | 类型是 `MeshVR.DAZRuntimeCreator`（非全局命名空间）；且新建人物后角色异步加载中组件尚未实例化——工具已做轮询等待 |
| Hub 直连 TLS 失败 | .NET 3.5 的 HttpWebRequest 无法 TLS1.2；必须用 UnityWebRequest（Unity 自带 TLS） |
| eval 的 Eval() 模板不可用 | 库自带模板把代码插在类成员位置（编译必败）；本仓库自建完整包装类。且 `GetType().Name` 会隐式引用 System.Reflection 触发沙箱 |
| 服装/发型物品是 `.vam` 文件 | 不是 .json；`list_clothing_presets` 同时扫描 .vam 与 .json |
| `save_look` 目录 | VaM 预设目录：`Saves/Person/{Appearance,Pose,full}` |
| 添加的人物 uid 可能被重命名 | VaM `CreateUID` 逻辑；以工具返回值为准 |
| eval 沙箱 | System.IO / System.Reflection / System.AppDomain / UnityEditor / Mono.Cecil 禁用（VaM 运行时安全策略） |

## API 速查（VaM 侧）

- `SuperController.singleton`：`GetAtoms()` / `GetAtomByUid(uid)` / `AddAtomByType(type, uid, userInvoked)`（协程）/ `RemoveAtom` / `Load` / `LoadMerge` / `NewScene` / `Save` / `SaveFromAtom(path, atom, physical, appearance)` / `GetFreeControllerNamesInAtom(uid)` / `MonitorCenterCamera` / `LoadJSON`
- `Atom`：`uid` / `type` / `on` / `ToggleOn()` / `transform` / `GetStorableByID(id)` / `GetStorableIDs()`
- `JSONStorable`：`GetFloatParamNames()` / `GetFloatParamValue(name)` / `SetFloatParamValue(name, v)`（bool/string/chooser/color 同理）、`GetAction(name)` / `RestoreFromJSON(jc, physical, appearance, prev, setUnlisted)`
- 捏人：`DAZCharacterSelector`（`GetStorableByID("geometry")`）→ `morphBank1/2/3` → `DAZMorph.morphValue`
- 灯光：`InvisibleLight` 的 `Light` storable（AdjustLightV2）：`type`(Spot/Directional/Point) / `intensity` / `color` / `spotAngle` / `shadowStrength`
- 插件：`MVRPluginManager`（`GetStorableByID("PluginManager")`）→ `CreatePlugin()` → `pluginURLJSON.val = path` 触发异步编译；`scriptControllers.Count > 0` 表示加载完成
- Hub：`MVR.Hub.HubDownloader.DownloadPackages(success, error, names...)`；API 端点 POST `{source:"VaM", action:"getResources"/"getResourceDetail", ...}`
- 反编译源码参考：VaM 安装目录的 `src2/` 是反编译的 Assembly-CSharp 源码，查 API 最快；
  但那是 Meshed VR 的代码，**绝不要抄进本仓库**

## 发布流程

1. 更新 `CHANGELOG.md`，以及 `src/VaMMCP.csproj`、`src/Plugin.cs`（`BepInPlugin`）、
   `src/Mcp/McpServer.cs`（`ServerVersion`）里的版本号。
2. `./scripts/check-docs.sh` 与 `dotnet build -c Release` 均无告警。
3. VaM 运行中跑 `./scripts/smoke-test.sh`。
4. `./scripts/release.sh v1.2.3`——本地构建、打 tag、发布 release 并附带 `VaMMCP.dll`。

### 发布检查清单

- [ ] `dotnet build -c Release` 0 错误 0 告警
- [ ] `./scripts/check-docs.sh` 通过
- [ ] `./scripts/smoke-test.sh` 通过（VaM 运行中）
- [ ] 无测试残留路径（`Saves/scene/stage_test*.json` 等）
- [ ] `.gitignore` 覆盖 `bin/ obj/ .nuget/ .home/`
- [ ] README/TOOLS/DEVELOPMENT 与工具数一致
