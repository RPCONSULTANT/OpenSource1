# OpenSource1.Blazor

| Setting | Value |
|---------|-------|
| **Interactivity Mode** | None (Static SSR) |
| **Interactivity Scope** | N/A |

## Rendering configuration

This project uses static server-side rendering with no interactive runtime.
Created with `dotnet new blazor -int None`.

Enhanced navigation via `blazor.web.js` is enabled by default, making page transitions feel instant without SignalR circuits or WebAssembly.

## Authentication

Authentication is handled by the API first: the Blazor login form posts credentials to `/api/auth/login`, receives a JWT, creates a secure HttpOnly cookie for the Blazor web app, and stores the JWT in server-side session for API calls. Do not expose the JWT to browser JavaScript.

Use `AuthorizeView`, `[Authorize]`, roles, and permission policies from `OpenSource1.Application.Security` for UI authorization. Server-side API endpoints still enforce JWT policies; client-side visibility is not a security boundary.

## Adding new components

- Create new `.razor` files in `Components/Pages/` for routable pages or `Components/` for shared components.
- Do **not** add `@rendermode` to any component unless the project is deliberately converted to an interactive mode.
- Forms use standard HTML POST with `[SupplyParameterFromForm]`, named `FormName`, validation components, and antiforgery.
- Query string parameters use `[SupplyParameterFromQuery]`.

## Data access

This project intentionally uses HTTP clients for the existing API, even though Static SSR can inject server services directly. Keep API-facing UI code behind typed client services in `Services/` and add the JWT with `BearerTokenHandler`.

## Don'ts

- Don't use `@onclick` or other event handlers; they require an interactive render mode.
- Don't store the JWT in local storage, session storage, or non-HttpOnly browser cookies.
- Don't trust hidden UI options as authorization; enforce policies in the API.
