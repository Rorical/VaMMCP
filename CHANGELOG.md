# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.1] - 2026-08-21

### Fixed
- **Protocol values are now real JSON types.** VaM's SimpleJSON quotes everything it serialises,
  so `listChanged`, `subscribe`, `isError`, JSON-RPC error codes and — most importantly — the
  JSON-RPC `id` all went out as strings. Clients built on the official MCP SDK validate against
  the spec schema and match responses by id, so they rejected the handshake and fell back to the
  legacy SSE transport until they timed out. opencode could not connect at all; it can now.
- `status` reported `vaMVersion` from `Application.version`, which is the Unity player version
  ("1.0"). It now uses `SuperController.GetVersion()` and reports the actual VaM build.
- `scripts/deploy.sh` and `scripts/install-bepinex.sh` resolved `VAM_ROOT` one level too high
  (the scripts already `cd` to the repo root), so with the repo inside the VaM folder they wrote
  to the *parent* of the VaM install. Both now resolve it correctly and refuse to run against a
  directory with no `VaM_Data` in it.

## [1.0.0] - 2026-08-20

First public release: an MCP server that lives inside Virt-A-Mate as a BepInEx plugin.

### Added
- Streamable HTTP MCP transport (spec 2025-06-18) served from inside the VaM process, targeting net35.
- 63 tools covering scenes, atoms, persons, morphs, clothing, hair, poses, expressions, bone
  controls, camera, simulation, Hub browsing/downloads, skin subsurface colour and VaM plugin
  management (see [docs/TOOLS.md](docs/TOOLS.md)).
- Generic JSONStorable introspection (`list_atom_storables`, `list_storable_params`, `get_param`,
  `set_param`, `call_action`) so any VaM UI slider is reachable without a dedicated tool.
- Preset export/import, `.var` package asset scanning, and native plugin management.
- `eval_cs` escape hatch, disabled by default.
- MCP resources: `vam://status` and `vam://atoms`.
- `capture_view` can return the rendered PNG inline as an MCP image with `return_image=true`.

### Security
- The HTTP listener binds to loopback only and validates the `Origin` header (DNS-rebinding
  protection); CORS echoes only an already-validated loopback origin instead of `*`.
- Concurrent connections are capped and request sizes are bounded.

[Unreleased]: https://github.com/Rorical/VaMMCP/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/Rorical/VaMMCP/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Rorical/VaMMCP/releases/tag/v1.0.0
