# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
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

[Unreleased]: https://github.com/Rorical/VaMMCP/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Rorical/VaMMCP/releases/tag/v1.0.0
