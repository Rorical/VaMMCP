# Security policy

## Threat model in one paragraph

VaMMCP opens an **unauthenticated** HTTP server inside the VaM process, bound to `127.0.0.1`.
Anything that can reach loopback on that machine — any local process, any user session on it —
can drive VaM through it: load and save scenes, write files under the VaM save folders, download
Hub content, and, if you switched it on, execute arbitrary C# in-process. That is the same trust
level as any local development server, and it is intentional: MCP clients run on the same machine.

Hardening that is in place:

- The listener binds to loopback only (`IPAddress.Loopback`), never to `0.0.0.0`.
- The `Origin` header is validated, so a web page you visit cannot reach the endpoint through
  DNS rebinding. Non-browser clients (which send no `Origin`) are allowed.
- `Access-Control-Allow-Origin` echoes only an already-validated loopback origin, never `*`.
- Concurrent connections are capped, request headers and bodies are size-limited.
- `eval_cs` is off by default (`Security.AllowEval=false`).

What is explicitly **not** protected against:

- Other local processes or other users on the same machine.
- Exposing the port yourself (port forwarding, a reverse proxy, a tunnel). Do not do this without
  putting authentication in front of it.
- Anything `eval_cs` does once you enable it. It is bounded only by VaM's own runtime sandbox
  (System.IO, System.Reflection, System.AppDomain, UnityEditor and Mono.Cecil are blocked).

## Reporting a vulnerability

Please report security issues privately through
[GitHub Security Advisories](https://github.com/Rorical/VaMMCP/security/advisories/new) rather than
in a public issue. A rough timeline: acknowledgement within a week, a fix or a decision within a month.

Useful things to include: what an attacker controls, what they gain, and a request or scenario that
reproduces it.

Since this is a hobby project with no paid users, there is no bounty — just credit in the changelog
unless you prefer otherwise.

## Scope

In scope: the HTTP/MCP layer (`src/Mcp/`), the tool implementations (`src/Api/`), the plugin
lifecycle (`src/Plugin.cs`), and the install/deploy scripts.

Out of scope: vulnerabilities in VaM itself or in BepInEx (report those upstream), and "an agent
did something destructive to my scenes" — the tools are supposed to be able to do that, keep backups.
