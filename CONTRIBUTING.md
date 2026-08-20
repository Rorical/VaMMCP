# Contributing

Thanks for looking. Bug reports, tool ideas and pull requests are all welcome.

## Before you start

- You need a legally owned copy of VaM to build or test anything — the project references
  `VaM_Data/Managed/Assembly-CSharp.dll` from your own install.
- **Do not paste VaM's own code into this repository.** Calling its public API from a plugin is the
  entire point of the project; copying Meshed VR's implementation into an MIT-licensed repo is not.
- By contributing you agree that your work is released under the [MIT licence](LICENSE).

## Getting set up

```bash
git clone https://github.com/Rorical/VaMMCP.git
cd VaMMCP
./scripts/deploy.sh                 # builds and copies the DLL into <VaM>/BepInEx/plugins/
# repo outside the VaM folder? point it at the install:
VAM_ROOT="D:/path/to/VaM" ./scripts/deploy.sh
```

Restart VaM, then check `<VaM>/BepInEx/LogOutput.log` for `VaMMCP ready`.

[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) covers the threading model, the net35 constraint and a
long list of VaM-specific traps. Read it before your first change — most surprises are in there.

## Adding a tool

1. Implement it in `src/Api/VaMApi.cs`. Throw `ApiError` for anything the user can fix; the message
   goes straight back to the model, so write it for that reader.
2. Register it in `src/Mcp/ToolRegistry.cs`. Descriptions are the model's only documentation —
   say what it does, what the units are, and what it does *not* do.
3. Anything asynchronous in VaM (creating atoms, loading characters, Hub calls) needs
   `.Poller()` and a wait loop, not a fixed sleep.
4. Document it in **both** `docs/TOOLS.md` and `docs/TOOLS.zh-CN.md`, and update the tool count in
   the two READMEs. `./scripts/check-docs.sh` enforces this.

## Style

- Tabs, K&R braces, `LangVersion 7.3` — match the file you are editing.
- **net35 only.** No `ConcurrentQueue`, no `async`/`await`, no `Span`, no C# 8+ syntax. If it
  compiles here but the plugin refuses to load in VaM, this is usually why.
- Comments explain *why*, especially for VaM quirks. The pitfalls table in DEVELOPMENT.md is the
  right home for anything that cost you an afternoon.

## Before opening a PR

```bash
dotnet build src/VaMMCP.csproj -c Release   # 0 warnings
./scripts/check-docs.sh                     # docs/registry consistency
./scripts/smoke-test.sh                     # with VaM running
```

Say in the PR which VaM version you tested against — behaviour differs between builds.

## Reporting bugs

Include: VaM version, BepInEx version, VaMMCP version, your MCP client, the failing tool call with
its arguments, and the relevant lines from `BepInEx/LogOutput.log`.

Security issues go to [SECURITY.md](SECURITY.md) instead — not the public tracker.
