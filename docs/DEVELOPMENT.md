# Development guide

*[中文版](DEVELOPMENT.zh-CN.md)*

## Build

```bash
# any recent .NET SDK can cross-compile this
./scripts/deploy.sh            # build + deploy to <VAM_ROOT>/BepInEx/plugins/
# or by hand:
dotnet build src/VaMMCP.csproj -c Release
```

### Build facts that matter

- **TargetFramework = net35.** VaM runs at Unity 2018.1's .NET 3.5 API level (BepInEx logs
  `CLR runtime version: 2.0.50727`). A net46+ assembly fails to load at runtime because APIs like
  `ConcurrentQueue` are missing — the first iteration of this repo learned that the hard way.
- **LangVersion 7.3**, which is as far as the old toolchain goes.
- **References:**
  - `VaM_Data/Managed/Assembly-CSharp.dll`, located through the `VaMRoot` MSBuild property
    (default `..\..`, i.e. the repo sitting inside the VaM folder)
  - `BepInEx.Core 5.4.21` (nuget.bepinex.dev)
  - `UnityEngine.Modules 2018.1.9` (nuget.org)
  - `Microsoft.NETFramework.ReferenceAssemblies` (required to target net35 on Linux/macOS)
- **NuGet caches:** `scripts/deploy.sh` points HOME/NUGET_PACKAGES at `.home/` and `.nuget/` inside
  the repo, which matters when the ambient HOME is read-only (sandboxes, CI).

### Why CI does not compile the plugin

`Assembly-CSharp.dll` ships with VaM and cannot be redistributed, so a public GitHub runner has
nothing to compile against. [`ci.yml`](../.github/workflows/ci.yml) therefore runs everything that
does not need it (restore, shell linting, docs/registry consistency) and only performs a real build
when the repository has a `VAM_ASSEMBLY_URL` secret pointing at a private copy of the assembly.
Releases are built locally with `scripts/release.sh`.

## Runtime architecture

```
HTTP worker thread (TcpListener @127.0.0.1:9837)
  └─ McpServer.HandleHttp → JSON-RPC → tool.Handler
       └─ MainThreadDispatcher.Run(fn, timeout)   ← blocks the worker
            └─ Plugin.Update() drains the queue each frame → fn runs on the Unity main thread
```

- **All VaM/Unity API access must happen on the main thread**; tool handlers are wrapped in `Run()`
  by default.
- **Polling tools** (`Tool.NeedsPoller = true`: `add_atom`, `add_person`, `add_plugin`, `hub_*`,
  `add_clothing_item`) run on the HTTP thread and call `Mt.Run` in slices, because VaM creates and
  loads things through coroutines that have to be waited on.
- **HTTP layer:** MCP Streamable HTTP (2025-06-18). `POST /mcp` → JSON-RPC response
  (`application/json`); `GET` → 405 (allowed by the spec, means "no SSE stream"); notifications →
  202. Loopback bind + Origin validation (DNS-rebinding protection), at most 32 concurrent
  connections, every response carries Content-Length and keep-alive works.
- **Images:** a tool that returns `image_base64`/`image_mime` in its JSON gets those lifted out by
  `McpServer.DoToolsCall` and re-emitted as an MCP image content block (see `capture_view`).

## Debugging

- Plugin log: `<VaM>/BepInEx/LogOutput.log` (`Log.Info/Warn/Error`; `Log.Debug` is filtered out by
  default — raise the level in `BepInEx/config/BepInEx.cfg`)
- VaM's own log: `%USERPROFILE%\AppData\LocalLow\MeshedVR\VaM\output_log.txt` (script compile errors
  and save diagnostics end up here)
- Smoke test: `./scripts/smoke-test.sh` (handshake + tools/list + status + list_atoms)

## Known limitations and traps

| Trap | What to do |
| --- | --- |
| net46 assemblies will not load | VaM is at the .NET 3.5 level, see above |
| Overwriting a save silently does nothing | VaM asks for confirmation when a plugin overwrites an existing file, and nobody clicks it. `PrepareSavePath` deletes the old file through System.IO first |
| Presets do not carry full material parameters | `save_look` / `save_full_preset` store only a few skin storable parameters (that is how VaM's atom presets work). Re-apply skin parameters with `set_param` after moving a look; **scene saves are complete** |
| `new_scene` has no lights | VaM's default scene relies on `3PointLightSetup`. An empty scene needs a light, and `GlobalLighting` (masterIntensity 0.1, ambient black by default) is **not saved with the scene** — set it after every load |
| `add_clothing_item` cannot find DAZRuntimeCreator | The type is `MeshVR.DAZRuntimeCreator` (not in the global namespace), and on a freshly created person the component does not exist yet while the character loads — the tool polls for it |
| Hub requests fail on TLS | .NET 3.5's HttpWebRequest cannot do TLS 1.2; use UnityWebRequest (Unity brings its own TLS) |
| The library's `Eval()` template is unusable | It inserts the code at class-member position, which never compiles; this repo builds its own wrapper class. Also `GetType().Name` implicitly pulls in System.Reflection and trips the sandbox |
| Clothing/hair items are `.vam` files | Not .json; `list_clothing_presets` scans both |
| `save_look` directories | VaM preset folders are `Saves/Person/{Appearance,Pose,full}` |
| An added person's uid may be renamed | VaM's `CreateUID` logic — trust the tool's return value |
| eval sandbox | System.IO / System.Reflection / System.AppDomain / UnityEditor / Mono.Cecil are blocked by VaM's runtime security policy |

## VaM API cheat sheet

- `SuperController.singleton`: `GetAtoms()` / `GetAtomByUid(uid)` / `AddAtomByType(type, uid, userInvoked)`
  (coroutine) / `RemoveAtom` / `Load` / `LoadMerge` / `NewScene` / `Save` /
  `SaveFromAtom(path, atom, physical, appearance)` / `GetFreeControllerNamesInAtom(uid)` /
  `MonitorCenterCamera` / `LoadJSON`
- `Atom`: `uid` / `type` / `on` / `ToggleOn()` / `transform` / `GetStorableByID(id)` / `GetStorableIDs()`
- `JSONStorable`: `GetFloatParamNames()` / `GetFloatParamValue(name)` / `SetFloatParamValue(name, v)`
  (same shape for bool/string/chooser/color), `GetAction(name)` /
  `RestoreFromJSON(jc, physical, appearance, prev, setUnlisted)`
- Morphs: `DAZCharacterSelector` (`GetStorableByID("geometry")`) → `morphBank1/2/3` → `DAZMorph.morphValue`
- Lights: the `Light` storable on `InvisibleLight` (AdjustLightV2): `type` (Spot/Directional/Point) /
  `intensity` / `color` / `spotAngle` / `shadowStrength`
- Plugins: `MVRPluginManager` (`GetStorableByID("PluginManager")`) → `CreatePlugin()` →
  setting `pluginURLJSON.val = path` starts an async compile; `scriptControllers.Count > 0` means loaded
- Hub: `MVR.Hub.HubDownloader.DownloadPackages(success, error, names...)`; API endpoint is a POST of
  `{source:"VaM", action:"getResources"/"getResourceDetail", ...}`
- Finding more: the plugin can introspect the running game. `list_atom_storables` followed by
  `list_storable_params` on a live atom enumerates every parameter VaM exposes, including the ones
  no dedicated tool covers — usually faster than hunting for the API by hand.

## Releasing

1. Update `CHANGELOG.md`, and the version in `src/VaMMCP.csproj`, `src/Plugin.cs`
   (`BepInPlugin`) and `src/Mcp/McpServer.cs` (`ServerVersion`).
2. `./scripts/check-docs.sh` and `dotnet build -c Release` must both be clean.
3. `./scripts/smoke-test.sh` against a running VaM.
4. `./scripts/release.sh v1.2.3` — builds locally, tags, and publishes the release with `VaMMCP.dll`
   attached.

### Checklist

- [ ] `dotnet build -c Release` — 0 errors, 0 warnings
- [ ] `./scripts/check-docs.sh` passes
- [ ] `./scripts/smoke-test.sh` passes with VaM running
- [ ] No leftover test artefacts (`Saves/scene/stage_test*.json` and friends)
- [ ] `.gitignore` still covers `bin/ obj/ .nuget/ .home/`
- [ ] README / TOOLS / DEVELOPMENT agree with each other and with the tool count
