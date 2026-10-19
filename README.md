# PersonaNotes

[![CI](https://github.com/Marokos999/PersonaNotesApp/actions/workflows/ci.yml/badge.svg)](https://github.com/Marokos999/PersonaNotesApp/actions/workflows/ci.yml)

A personal Markdown notes app: ASP.NET Core minimal API + Blazor WebAssembly + PostgreSQL, shipped as a single Docker image.

## Features

- **Notes** in Markdown (tables, task lists, …) rendered server-side and **sanitized** against XSS
- Clickable **task lists** that persist back into the note
- **Tags** (create, rename, recolor, delete, per-tag note counts) and tag filtering
- **Full-text search** (PostgreSQL `tsvector` + GIN index)
- **Pin**, **archive/restore** and **permanent delete**
- **Pagination** (`page` / `pageSize`, total in the `X-Total-Count` header)
- **Auth**: ASP.NET Identity + short-lived JWT access tokens + rotating, hashed refresh tokens; the Blazor client refreshes the session transparently
- **Tag management page** (rename, recolor, delete) and **live Markdown preview** while editing
- **Dark mode** (follows the OS, toggle in the sidebar)
- Per-user data isolation (covered by integration tests)
- **Privacy & data control**: Privacy Policy and Terms pages (operator details come from configuration), **data export** (JSON) and **account deletion** with password confirmation, which erases the account and everything it owns
- **Hardening**: Content-Security-Policy (script-src limited to the own origin, the inline import map allowed by hash), HSTS, `X-Frame-Options`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`
- **SEO basics**: `robots.txt`, `sitemap.xml` and meta/Open Graph tags

## Tech stack

| Layer | Technology |
|---|---|
| API | .NET 10, ASP.NET Core minimal APIs ([Carter](https://github.com/CarterCommunity/Carter) modules), EF Core, Npgsql |
| Frontend | Blazor WebAssembly, Markdig (client-side preview) |
| Database | PostgreSQL 16 |
| Auth | ASP.NET Identity, JWT bearer, refresh-token rotation |
| Observability | Serilog, OpenTelemetry (OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set), `/health` |
| API docs | OpenAPI + [Scalar](https://github.com/scalar/scalar) (Development only, `/scalar`) |
| Tests | xUnit, FluentAssertions, `WebApplicationFactory`, EF Core InMemory, bUnit |
| CI/CD | GitHub Actions, multi-stage Docker build |

## Architecture

```
src/
├── PersonaNotesApp.API      # Carter modules → repositories → EF Core; also serves the Blazor files
│   ├── Modules              # Auth / Notes / Tags / Account / Site (app info, robots, sitemap) endpoints
│   ├── Application          # Interfaces, TokenService, AccountService, MarkdownRenderer, validation filter, mappings
│   ├── Domain               # Note, Tag, NoteTag, RefreshToken, ApplicationUser
│   └── Infrastructure       # AppDbContext, repositories, security headers
├── PersonaNotesApp.Shared   # DTOs shared by API and client (DataAnnotations validation)
└── PersonaNotesApp.Web      # Blazor WASM client (auth handler, services, pages)
tests/
├── PersonaNotesApp.API.Tests   # unit + integration tests
└── PersonaNotesApp.Web.Tests   # bUnit component tests + auto-refresh handler tests
```

In production a single container serves both the API and the compiled Blazor app on port 8080, so no CORS is required.

## Running with Docker

```bash
cp .env.example .env        # then set POSTGRES_PASSWORD and JWT_SECRET (>= 32 chars)
docker compose up -d --build
```

The app is available at <http://localhost:8080>; health check at `/health`. Migrations are applied on startup.

> The Postgres password is only applied when the `pna-postgres-data` volume is first created. If you already have that volume from an earlier version, either use the password it was created with or remove it (`docker compose down -v`, **deletes data**).

## Deployment

The production setup mirrors my ToDoList project: a Hetzner VPS where a shared [Caddy](https://caddyserver.com) reverse proxy terminates HTTPS (certificates are issued and renewed automatically) and forwards to the app container. PostgreSQL runs in the same Docker Compose stack and is not reachable from outside.

Every push to `main` runs [`ci.yml`](.github/workflows/ci.yml). When it passes, [`deploy.yml`](.github/workflows/deploy.yml) connects to the server over SSH, checks out the tested commit, runs `docker compose up -d --build` and waits until `/health` answers. It needs the repository secrets `DEPLOY_HOST`, `DEPLOY_SSH_KEY` and `DEPLOY_KNOWN_HOSTS` (the pinned host key). The application's own settings live in a `.env` file on the server and never in Git.

[`docker-compose.prod.yml`](docker-compose.prod.yml) joins an external Docker network called `edge` as `notes-app:8080`, publishes no host ports and keeps PostgreSQL inside the compose network. The proxy needs a site pointing at `notes-app:8080`; behind it the app trusts the `X-Forwarded-*` headers (`ForwardedHeaders__Enabled=true`). To set up the app on a server that already runs the proxy:

```bash
git clone https://github.com/Marokos999/PersonaNotesApp.git ~/PersonaNotesApp && cd ~/PersonaNotesApp
docker network create edge 2>/dev/null   # once per server, skip if the proxy already created it
cp .env.example .env                     # set POSTGRES_PASSWORD (letters/digits only), JWT_SECRET, APP_PUBLIC_URL and LEGAL_*
docker compose -f docker-compose.prod.yml up -d --build
```

Fill in `LEGAL_OPERATOR_NAME` and `LEGAL_CONTACT_EMAIL` before sharing the site: they are shown on the Privacy Policy and Terms pages, which otherwise display a "not configured" notice. After the first start, updates are done by the pipeline. Migrations run on startup.

## Local development

Requirements: .NET 10 SDK and a PostgreSQL instance (the Development settings expect `localhost`, user/password `postgres`).

```bash
dotnet run --project src/PersonaNotesApp.API      # http://localhost:5203, Scalar UI at /scalar
dotnet run --project src/PersonaNotesApp.Web      # http://localhost:5047 (hot reload client)
```

`appsettings.Development.json` contains a dev-only JWT secret. In any other environment the API **refuses to start** unless `Jwt__Secret` is set to a real value (32+ characters, not a `CHANGE-ME` placeholder).

### Configuration

| Setting | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Secret` / `Jwt__Issuer` / `Jwt__Audience` | JWT signing |
| `Jwt__AccessTokenExpiryMinutes` / `Jwt__RefreshTokenExpiryDays` | Token lifetimes |
| `Cors__AllowedOrigins__0`, `__1`, … | Origins allowed to call the API cross-origin (empty = same-origin only) |
| `RateLimiting__AuthPermitLimit` | Auth requests per minute per IP (default 10) |
| `ForwardedHeaders__Enabled` | `true` behind a reverse proxy so the rate limiter sees the real client IP. Trusts `X-Forwarded-For`, so enable it only when a proxy is actually in front |
| `App__PublicUrl` | Public address of the site (`https://…`), used in `robots.txt` and `sitemap.xml`; defaults to the address of the request |
| `Legal__OperatorName` / `Legal__ContactEmail` | Who runs this installation; shown on the Privacy Policy and Terms pages |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Enables OpenTelemetry export |

### Migrations

```bash
dotnet ef migrations add <Name> --project src/PersonaNotesApp.API
```

## Tests

```bash
dotnet test
```

Covers repositories, Blazor components (bUnit), the auto-refresh HTTP handler, `TokenService`, auth flows (register / login / refresh / logout), validation, XSS sanitization, pagination, user isolation, security headers (CSP/HSTS), data export and account deletion. Full-text search needs PostgreSQL, so the in-memory tests fall back to `Contains`; the real query path is exercised by the Docker smoke test in CI.

## API overview

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/register` · `/login` · `/refresh` · `/logout` | Authentication (rate limited) |
| GET | `/api/notes?tag=&search=&page=&pageSize=` | List active notes |
| GET | `/api/notes/archived` | List archived notes |
| GET/PUT/DELETE | `/api/notes/{id}` | Read, update, **permanently delete** |
| PATCH | `/api/notes/{id}/pin` · `/archive` · `/tags` | Toggle pin / archive, set tags |
| GET/POST | `/api/tags` | List (with note counts) / create |
| PUT/DELETE | `/api/tags/{id}` | Rename or recolor / delete |
| GET | `/api/account/export` | Download all of the user's notes and tags as JSON |
| DELETE | `/api/account` | Permanently delete the account and its data (body: `{ "password": "…" }`, rate limited) |
| GET | `/api/app-info` | Public: operator details for the legal pages |
| GET | `/robots.txt` · `/sitemap.xml` | Crawler documents |
| GET | `/health` | Liveness + database check |

## License

[MIT](LICENSE)
