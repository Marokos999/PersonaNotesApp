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
- Per-user data isolation (covered by integration tests)

## Tech stack

| Layer | Technology |
|---|---|
| API | .NET 10, ASP.NET Core minimal APIs ([Carter](https://github.com/CarterCommunity/Carter) modules), EF Core, Npgsql |
| Frontend | Blazor WebAssembly |
| Database | PostgreSQL 16 |
| Auth | ASP.NET Identity, JWT bearer, refresh-token rotation |
| Observability | Serilog, OpenTelemetry (OTLP export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set), `/health` |
| API docs | OpenAPI + [Scalar](https://github.com/scalar/scalar) (Development only, `/scalar`) |
| Tests | xUnit, FluentAssertions, `WebApplicationFactory`, EF Core InMemory |
| CI/CD | GitHub Actions, multi-stage Docker build |

## Architecture

```
src/
├── PersonaNotesApp.API      # Carter modules → repositories → EF Core; also serves the Blazor files
│   ├── Modules              # Auth / Notes / Tags endpoints
│   ├── Application          # Interfaces, TokenService, MarkdownRenderer, validation filter, mappings
│   ├── Domain               # Note, Tag, NoteTag, RefreshToken, ApplicationUser
│   └── Infrastructure       # AppDbContext, repositories
├── PersonaNotesApp.Shared   # DTOs shared by API and client (DataAnnotations validation)
└── PersonaNotesApp.Web      # Blazor WASM client (auth handler, services, pages)
tests/
└── PersonaNotesApp.API.Tests   # unit + integration tests
```

In production a single container serves both the API and the compiled Blazor app on port 8080, so no CORS is required.

## Running with Docker

```bash
cp .env.example .env        # then set POSTGRES_PASSWORD and JWT_SECRET (>= 32 chars)
docker compose up -d --build
```

The app is available at <http://localhost:8080>; health check at `/health`. Migrations are applied on startup.

> The Postgres password is only applied when the `pna-postgres-data` volume is first created. If you already have that volume from an earlier version, either use the password it was created with or remove it (`docker compose down -v`, **deletes data**).

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
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Enables OpenTelemetry export |

### Migrations

```bash
dotnet ef migrations add <Name> --project src/PersonaNotesApp.API
```

## Tests

```bash
dotnet test
```

Covers repositories, `TokenService`, auth flows (register / login / refresh / logout), validation, XSS sanitization, pagination and user isolation. Full-text search needs PostgreSQL, so the in-memory tests fall back to `Contains`; the real query path is exercised by the Docker smoke test in CI.

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
| GET | `/health` | Liveness + database check |

## License

[MIT](LICENSE)
