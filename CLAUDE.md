# OpenSource1

## Project context

- TFM: `net10.0`
- Solution: `test.slnx` (modern XML solution format; use `dotnet build test.slnx` / `dotnet test test.slnx`, not a `.sln`).
- Projects: `OpenSource1.Core`, `OpenSource1.Application`, `OpenSource1.Infrastructure`, `OpenSource1.Blazor`, `OpenSource1.Api`, `tests/OpenSource1.SmokeTests`.
- Blazor app: `src/OpenSource1.Blazor/OpenSource1.Blazor.csproj`. Render mode is **Static SSR only** — no interactive runtime.
  - Do not add `@rendermode`, `@onclick`, or other interactive event handlers unless the user explicitly asks to convert a page to an interactive render mode.
  - Use GET/POST forms, `[SupplyParameterFromForm]`, `[SupplyParameterFromQuery]`, antiforgery tokens, and typed client services instead.
- API app: `src/OpenSource1.Api/OpenSource1.Api.csproj`. Auth is API-first JWT.
  - Blazor must keep the JWT out of browser JavaScript: use a secure HttpOnly cookie plus a server-side session for calls to the API.
- Packages: direct `PackageReference` per project; this repo does not use Central Package Management (`Directory.Packages.props`).
- Conventions: nullable enabled, implicit usings enabled — follow the existing folder layout per project (Features/Handlers under Application, Controllers under Api, etc.).

## Git branch strategy

- `main` — stable base.
- `qa` — validation.
- `deploy` — deployment prep.
- Do not commit, merge, or push without an explicit request from the user.

## Current deliverable (Entregable_2): CRUD maintenance modules

When asked for a maintenance CRUD module, follow the existing pattern established by Clientes/Productos:

- Add API endpoints (`Controllers`), Application contracts/handlers (`Features/<Entity>`), Infrastructure data access (Dapper repositories), and a Static SSR Blazor page, matching existing conventions in each layer.
- Required actions per module: agregar, modificar, eliminar, consultar, limpiar campos.
- No interactive Blazor handlers — use submit buttons/actions and query parameters, consistent with the Static SSR rule above.

## Verification

- Build: `dotnet build test.slnx`.
- Test: `dotnet test test.slnx` (covers `OpenSource1.SmokeTests`).
- Run the smallest useful check first (targeted project/test); only build/test the full solution when the change could affect other layers.

## Related local agent config (not authoritative for Claude Code, context only)

- `.opencode/` (gitignored, personal) configures an OpenCode `dotnet` agent with the same conventions above.
- `~/.codex/agents/dotnet.md` and `~/.codex/AGENTS.md` (global, personal) configure an equivalent Codex `dotnet` agent.
- `.agents/skills/blazor/SKILL.md` is a shared, committed Blazor skill reference.
- `OpenSource1.code-workspace` (gitignored, personal) is a VS Code multi-root workspace that opens this repo alongside `~/.codex` for editing both configs together.
