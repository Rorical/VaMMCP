# Tool reference (63 tools)

*[中文版](TOOLS.zh-CN.md)*

Every tool is invoked through `tools/call`: `{ "name": "...", "arguments": { ... } }`.
A `*` marks a required argument, `?` an optional one. When `person` is omitted the first Person
in the scene is used. Vectors are `[x, y, z]`.

`eval_cs` is only registered when `Security.AllowEval=true`, so a default install exposes 62 tools.

## Status

### status
VaM runtime status: plugin version, VaM version, atom/person counts, endpoint, eval switch.

## Scene

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_scenes` | `search?` | Scenes under `Saves/scene` |
| `load_scene` | `path`*, `merge?` | Load a scene (`merge=true` merges into the current one); .var paths work (`Package.1:/Saves/scene/x.json`) |
| `new_scene` | — | New empty scene (**discards the current one**; an empty scene has no lights — add one or raise ambient light) |
| `save_scene` | `path?` | Save the scene (default `Saves/scene/VaMMCP_<timestamp>.json`; an existing file is removed first so the save is not swallowed by VaM's confirm dialog) |

## Atom

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_atom_types` | — | Creatable atom types, grouped by category (from VaM's runtime data) |
| `list_atoms` | `type?` | Atoms in the scene (uid/type/on/position/rotation) |
| `add_atom` | `type`*, `uid?` | Create an atom and wait until it is ready (up to 45 s) |
| `remove_atom` | `uid`* | Delete an atom |
| `set_atom_on` | `uid`*, `on`* | Show/hide |
| `get_atom_transform` | `uid`* | World position / euler angles / scale |
| `set_atom_transform` | `uid`*, `position?`, `rotation?`, `scale?`, `relative?` | Transform (`relative=true` offsets instead of setting) |

## Person

| Tool | Arguments | Notes |
| --- | --- | --- |
| `add_person` | `uid?` | Add a Person (VaM may rename the uid — trust the return value) |
| `list_persons` | — | Persons in the scene (uid/character/gender/position) |
| `list_looks` | `search?` | Appearance presets (`Saves/Person/Appearance`) |
| `load_look` | `path`*, `person?` | Apply an appearance preset (character/clothing/hair/morph deltas; asynchronous, verify a moment later) |
| `save_look` | `path?`, `person?` | Export an appearance preset (**note**: VaM presets do not carry the full material parameter set — re-apply skin parameters after moving a look between scenes) |
| `set_character` | `person?`, `name`* | Switch the base character mesh (names come from the choices of `get_param storable=geometry param=characterSelection`) |

## Preset export

| Tool | Notes |
| --- | --- |
| `save_look` | Appearance (character/clothing/hair/morphs) → `Saves/Person/Appearance/` |
| `save_pose` | Pose + physics → `Saves/Person/Pose/` |
| `save_full_preset` | Everything (appearance + pose + physics) → `Saves/Person/full/` |

## Morphs

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_morphs` | `person?`, `search?`, `region?`, `group?`, `limit?` | Search morphs (name/uid/region/group/current value) |
| `set_morph` | `person?`, `name`*, `value`* | Set a morph value (typically -1..1) |
| `get_morph` | `person?`, `name`* | Read a morph value |
| `reset_morphs` | `person?`, `region?` | Zero morphs, optionally only one region |

## Clothing / hair

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_packages` | — | Installed .var packages |
| `list_clothing_presets` | `search?` | Clothing presets and items: disk **plus a recursive scan of every package**'s `Custom/Clothing` (.vam/.json) |
| `load_clothing_preset` | `path`*, `person?` | Apply a clothing preset |
| `list_clothing_items` | `person?` | Clothing currently on the person (uid/name/active) |
| `add_clothing_item` | `path`*, `person?` | Add a clothing item (`.vam` package path; waits for the character to finish loading) |
| `remove_clothing_item` | `id`*, `person?` | Remove an item |
| `set_clothing_item_on` | `id`*, `on`*, `person?` | Wear / take off |
| `list_hair_presets` / `load_hair_preset` / `list_hair_items` / `add_hair_item` / `remove_hair_item` / `set_hair_item_on` | same as clothing | Hair (`Custom/Hair`) |

## Pose / expression

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_poses` | `search?` | Pose presets (`Saves/Person/Pose`) |
| `load_pose` | `path`*, `person?`, `all?` | Apply a pose (`all=true` applies it to every person) |
| `save_pose` | `path?`, `person?` | Export a pose |
| `list_expressions` | `person?` | Expression morphs (heuristic filter) |
| `set_expression` | `person?`, `name`*, `value?`, `reset?` | Set an expression (clears the other expressions first by default; `value` defaults to 1) |

## Bone control

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_controls` | `person?` | FreeControllerV3 control points (headControl / chestControl / …) |
| `get_control` | `person?`, `control`* | Control point world position / euler angles |
| `set_control` | `person?`, `control`*, `position?`, `rotation?` | Move / rotate a control point |
| `set_gaze` | `person?`, `amount?`, `targetControl?` | Gaze (best effort; for precise control use the `lookAt` storable parameters) |

## Generic parameters (the long tail)

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_atom_storables` | `uid`* | Every storable on an atom, with its type |
| `list_storable_params` | `uid`*, `storable`* | Parameter introspection: floats (with min/max/default) / bools / strings / choosers / colors / actions / customParams / properties |
| `get_param` | `uid`*, `storable`*, `param`* | Read a parameter (float/bool/string/chooser/color/property auto-detected) |
| `set_param` | `uid`*, `storable`*, `param`*, `value`*, `type?` | Write a parameter (colors accept `#RRGGBB` or `r,g,b,a`) |
| `call_action` | `uid`*, `storable`*, `action`* | Trigger a storable action (a button in the UI) |

`type` can force the interpretation: `float|bool|string|chooser|color|property`
(`property` reads/writes a public property through reflection, covering customParamNames such as `gravityX`).

## Camera

| Tool | Arguments | Notes |
| --- | --- | --- |
| `get_camera` | — | Monitor camera position / rotation / FOV |
| `set_camera` | `position?`, `rotation?`, `fov?` | Move the camera |
| `capture_view` | `width?`, `height?`, `path?`, `return_image?` | Render a screenshot (default `Saves/PluginData/vam-mcp/preview.png`). With `return_image=true` the PNG also comes back as an MCP image block, so clients that cannot read the VaM folder can still see it — keep the resolution modest (e.g. 640×360); anything above 4 MB stays on disk only |

## Simulation

| Tool | Arguments | Notes |
| --- | --- | --- |
| `set_simulation` | `paused?`, `timeScale?` | Pause / resume / time scale |
| `reset_simulation` | — | Reset the physics simulation (settles soft-body and cloth jitter) |

## Hub (community content)

| Tool | Arguments | Notes |
| --- | --- | --- |
| `hub_browse` | `search?`, `page?`, `perpage?`, `sort?`, `type?` | Search the VaM Hub (id/title/author/version/type/downloads/rating/tags) |
| `hub_detail` | `package?`, `resource_id?` | Resource detail: package name + every version's .var + dependencies |
| `hub_download` | `package`* | Download and install through VaM's own downloader (handles auth and dependencies; free content works anonymously, paid content needs you signed in inside VaM) |

## Skin

| Tool | Arguments | Notes |
| --- | --- | --- |
| `set_skin_sss` | `person?`, `color`* | Subdermis (subsurface) colour `_SubdermisColor`; a pink tint such as `#FFC9C9` reads as fairer skin |

## Plugin management (native VaM plugins)

| Tool | Arguments | Notes |
| --- | --- | --- |
| `list_plugins` | `uid?` | Loaded plugins (empty uid = CoreControl session plugins; pass a person uid for that person's plugins) |
| `add_plugin` | `path`*, `uid?` | Load a .cs/.cslist/.dll plugin and wait for compilation. Once loaded the plugin is a storable, so `list_storable_params` / `set_param` configure it (storable ids look like `plugin#0_MVRPlugin.ClassName`) |
| `remove_plugin` | `plugin_uid`*, `uid?` | Unload a plugin |

## Escape hatch

### eval_cs (disabled by default, enable with `Security.AllowEval=true`)
- No `;` → expression mode, the value of the expression is returned
- Contains `;` → statement mode (use an explicit `return` when you want a value)
- `sc` is bound to `SuperController.singleton`
- Restricted by VaM's runtime sandbox: System.IO / System.Reflection / System.AppDomain /
  UnityEditor / Mono.Cecil are blocked (note that `GetType().Name` pulls in System.Reflection —
  use `ToString()` instead)
