# 工具参考（63 个）

所有工具通过 `tools/call` 调用：`{ "name": "...", "arguments": { ... } }`。
`person` 参数省略时默认使用场景中第一个 Person。数值数组均为 `[x, y, z]`。

## 状态与会话

### status
VaM 运行时状态：插件版本、VaM 版本、原子/人物数量、端点、eval 开关。

## 场景

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_scenes` | `search?` | 列出 `Saves/scene` 下的场景 |
| `load_scene` | `path`*, `merge?` | 加载场景（`merge=true` 合并进当前场景）；支持 .var 包路径（`Package.1:/Saves/scene/x.json`） |
| `new_scene` | — | 新建空场景（**丢弃当前场景**；空场景无默认灯光，记得打灯/开环境光） |
| `save_scene` | `path?` | 保存场景（默认 `Saves/scene/VaMMCP_<时间戳>.json`；覆盖已存在文件会自动先删后存） |

## Atom

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_atom_types` | — | 可创建的原子类型（按分类，来自 VaM 运行时数据） |
| `list_atoms` | `type?` | 场景原子列表（uid/type/on/位置/旋转） |
| `add_atom` | `type`*, `uid?` | 创建原子，等待其就绪（最长 45s） |
| `remove_atom` | `uid`* | 删除原子 |
| `set_atom_on` | `uid`*, `on`* | 显示/隐藏 |
| `get_atom_transform` | `uid`* | 世界坐标/欧拉角/缩放 |
| `set_atom_transform` | `uid`*, `position?`, `rotation?`, `scale?`, `relative?` | 变换（`relative=true` 为相对位移） |

## 人物

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `add_person` | `uid?` | 添加人物（uid 可能被 VaM 重命名，以返回值为准） |
| `list_persons` | — | 人物列表（uid/角色名/性别/位置） |
| `list_looks` | `search?` | 外观预设列表（`Saves/Person/Appearance`） |
| `load_look` | `path`*, `person?` | 应用外观预设（角色/服装/发型/morph 增量；异步，稍后验证） |
| `save_look` | `path?`, `person?` | 导出外观预设（**注意**：VaM 的预设保存不含完整材质参数，跨场景搬运后需按需补设皮肤参数） |
| `set_character` | `person?`, `name`* | 切换基础角色网格（名称见 `get_param storable=geometry param=characterSelection` 的 choices） |

## 预设导出

| 工具 | 说明 |
| --- | --- |
| `save_look` | 外观（角色/服装/发型/morph）→ `Saves/Person/Appearance/` |
| `save_pose` | 姿态+物理 → `Saves/Person/Pose/` |
| `save_full_preset` | 全量（外观+姿态+物理）→ `Saves/Person/full/` |

## 捏人（Morph）

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_morphs` | `person?`, `search?`, `region?`, `group?`, `limit?` | 搜索 morph（含名称/uid/区域/分组/当前值） |
| `set_morph` | `person?`, `name`*, `value`* | 设置 morph 值（典型范围 -1..1） |
| `get_morph` | `person?`, `name`* | 读取 morph 值 |
| `reset_morphs` | `person?`, `region?` | 归零（可按区域） |

## 服装 / 发型

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_packages` | — | 已安装 .var 包（111 个级别的资产源） |
| `list_clothing_presets` | `search?` | 服装预设/物品：磁盘 + **递归扫描所有包**的 `Custom/Clothing`（.vam/.json） |
| `load_clothing_preset` | `path`*, `person?` | 应用服装预设 |
| `list_clothing_items` | `person?` | 人物当前服装物品（uid/名称/active） |
| `add_clothing_item` | `path`*, `person?` | 添加服装物品（包路径 `.vam`；等待角色就绪后加载） |
| `remove_clothing_item` | `id`*, `person?` | 移除 |
| `set_clothing_item_on` | `id`*, `on`*, `person?` | 穿/脱 |
| `list_hair_presets` / `load_hair_preset` / `list_hair_items` / `add_hair_item` / `remove_hair_item` / `set_hair_item_on` | 同服装 | 发型（`Custom/Hair`） |

## 姿态 / 表情

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_poses` | `search?` | 姿态预设（`Saves/Person/Pose`） |
| `load_pose` | `path`*, `person?`, `all?` | 应用姿态（`all=true` 应用到所有人） |
| `save_pose` | `path?`, `person?` | 导出姿态 |
| `list_expressions` | `person?` | 表情 morph 列表（启发式过滤） |
| `set_expression` | `person?`, `name`*, `value?`, `reset?` | 设置表情（默认先清空其他表情再设，value 默认 1） |

## 骨骼控制

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_controls` | `person?` | FreeControllerV3 控制点（41 个：headControl/chestControl/…） |
| `get_control` | `person?`, `control`* | 控制点世界位置/欧拉角 |
| `set_control` | `person?`, `control`*, `position?`, `rotation?` | 移动/旋转控制点 |
| `set_gaze` | `person?`, `amount?`, `targetControl?` | 视线（尽力而为；精确控制见 lookAt storable 参数） |

## 通用参数（长尾全覆盖）

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_atom_storables` | `uid`* | 原子全部 storable（含类型） |
| `list_storable_params` | `uid`*, `storable`* | 参数内省：floats（含 min/max/default）/bools/strings/choosers/colors/actions/customParams/properties |
| `get_param` | `uid`*, `storable`*, `param`* | 读参数（float/bool/string/chooser/color/property 自动识别） |
| `set_param` | `uid`*, `storable`*, `param`*, `value`*, `type?` | 写参数（颜色支持 `#RRGGBB` 或 `r,g,b,a`） |
| `call_action` | `uid`*, `storable`*, `action`* | 触发 storable 的动作（按钮） |

`type` 可强制指定：`float|bool|string|chooser|color|property`（`property` 走反射读写公共属性，覆盖 customParamNames 如 `gravityX`）。

## 相机

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `get_camera` | — | 监视器相机位置/旋转/FOV |
| `set_camera` | `position?`, `rotation?`, `fov?` | 移动相机 |
| `capture_view` | `width?`, `height?`, `path?` | 渲染截图（默认 `Saves/PluginData/vam-mcp/preview.png`，Agent 可配合图像工具"看"效果） |

## 模拟

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `set_simulation` | `paused?`, `timeScale?` | 暂停/恢复/时间缩放 |
| `reset_simulation` | — | 重置物理模拟（软体/布料抖动） |

## Hub 社区资源

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `hub_browse` | `search?`, `page?`, `perpage?`, `sort?`, `type?` | 搜索 VaM Hub（返回 id/title/作者/版本/类型/下载量/评分/tags） |
| `hub_detail` | `package?`, `resource_id?` | 资源详情：包名 + 所有版本 .var 文件 + 依赖 |
| `hub_download` | `package`* | 用游戏自带下载器下载安装（处理鉴权/依赖；免费内容匿名可用，付费需在 VaM 登录） |

## 皮肤

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `set_skin_sss` | `person?`, `color`* | 设置次表面反射色（`_SubdermisColor`；粉调如 `#FFC9C9` 显白皙） |

## 插件管理（VaM 原生插件）

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `list_plugins` | `uid?` | 插件列表（空 uid = CoreControl 会话插件；传人物 uid 看人物插件） |
| `add_plugin` | `path`*, `uid?` | 挂载 .cs/.cslist/.dll 插件，等待编译完成；加载后插件成为 storable，用 `list_storable_params`/`set_param` 配置（storable id 形如 `plugin#0_MVRPlugin.类名`） |
| `remove_plugin` | `plugin_uid`*, `uid?` | 移除插件 |

## 逃生舱

### eval_cs（默认关闭，`Security.AllowEval=true` 开启）
- 无 `;` → 表达式模式，返回表达式的值
- 含 `;` → 语句模式（需要值时用显式 `return`）
- `sc` 绑定 `SuperController.singleton`
- 受 VaM 运行时沙箱限制：System.IO / System.Reflection / System.AppDomain / UnityEditor / Mono.Cecil 禁用（注意 `GetType().Name` 会引用 System.Reflection，用 `ToString()` 代替）
