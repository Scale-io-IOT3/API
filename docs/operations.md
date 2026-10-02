# API setup and maintenance

## Local development

Use a .NET 9 SDK and Docker Compose. Run `docker compose up --build` from the repository root. The API is available at `http://localhost:5175`, with docs at `/scalar/v1`. Compose supplies the database connection and Development environment. `API/.env` is now optional (requires Docker Compose supporting optional env files, version 2.24 or later).

During verification on this Mac's OrbStack installation, the native ARM container build crashed in the .NET compiler with exit code 132; the x86-64 build succeeded. Compose therefore defaults the API service to `linux/amd64`, using emulation on ARM Macs. PostgreSQL retains its native platform. This is a workaround for an observed local container limitation, not a diagnosed root cause. After updating the container runtime, you can test native execution with `API_DOCKER_PLATFORM=linux/arm64 docker compose up --build`. CI uses native x86-64 Linux. Host-side .NET builds and tests pass without this workaround.

After changing the API platform, run `docker compose up --build --force-recreate scale.io` to replace the existing API container. Do not remove database volumes; this fix does not require resetting PostgreSQL.

The tracked development settings contain a stable, public, development-only signing key. Production must supply its own `Jwt__Key`; do not copy the development key to production. Environment variables override JSON settings. For direct `dotnet run`, set `ASPNETCORE_ENVIRONMENT=Development` and supply a reachable PostgreSQL connection via `DATABASE_URL`, `SOURCE`, or `ConnectionStrings__DefaultConnection`, in that precedence order. Compose's database is intentionally not exposed to the host.

Optional development user seeding must be explicitly enabled, with your own credentials in the ignored `API/.env` file:

```dotenv
SeedDefaultUserOnStartup=true
DevelopmentUser__Username=local-user
DevelopmentUser__Password=replace-with-your-local-password
```

Seeding runs only in Development, creates an account only if absent, and never resets an existing password. If migrations are disabled, the schema must already exist before seeding. Startup no longer probes PostgreSQL's system catalog or rewrites passwords. The previous local simplification in Configuration.cs is superseded by this explicit initialization flow.

## Editor navigation and dependency resolution

Open `Scale.io_API.sln` in Rider, or open the repository root in your C#-enabled editor. Docker Compose isolates each project's `bin` and `obj` directories in container volumes so Linux NuGet paths cannot overwrite the Mac/host files used for code navigation and namespace resolution.

After upgrading from the previous Compose configuration, run `docker compose up -d --no-deps --force-recreate scale.io`, then `dotnet restore Scale.io_API.sln --force` on the host. Reload the solution or restart the editor's C# language service after the restore. The database does not need to be reset. If navigation still fails, check the editor's project-loading errors and confirm its C# language support is enabled.

## Production and migrations

Set `Jwt__Issuer`, `Jwt__Audience`, `Jwt__TokenValidityMins` (positive), and `Jwt__Key` (base64 encoding of at least 32 random bytes). Missing or invalid JWT options fail startup rather than generating an ephemeral key. Store the key and connection string as hosting secrets. Do not print them in logs.

`ApplyMigrationsOnStartup` defaults to true in Development and false elsewhere. Fly sets it to false and runs `dotnet API.dll --migrate` as its release command. The production Docker image uses `CMD` so Fly can replace the default server command. This command applies migrations and exits without listening or seeding. A failed migration stops deployment. The release environment inherits application secrets and needs a valid database connection. For other hosting platforms, run the same command once before starting the application. Keep future schema changes compatible with the previous running version during rolling deploys.

This change adds no EF schema migration and does not delete accounts or tokens. Production default-user seeding is blocked regardless of the seeding flag. An existing account created using the old public default credentials remains in the database: review and disable or change that account through your normal administrative process. Password changes alone do not invalidate its already-issued access/refresh tokens; revoke its refresh-token rows and account for the access-token lifetime.

Fly terminates TLS and now forces public HTTPS. The application no longer redirects internal HTTP health checks to HTTPS. If hosting elsewhere, enforce TLS at the ingress or configure HTTPS for that deployment.

## Health and delivery

- `/health`: readiness, verifies database connectivity; returns 503 on failure. This does not verify every table or external food provider.
- `/health/live`: process liveness, does not require database connectivity.
- Fly's service check uses `/health` to gate traffic.
- CI runs restore, a Release build with warnings treated as errors, HTTP contract tests, real PostgreSQL migration/concurrency tests, and a production Docker build on pull requests and pushes to main.
- Deployment runs only after a successful push-triggered `API checks` workflow and checks out that workflow's exact tested SHA.
- Docker build context excludes `.env` files and `.git`.
- The production image runs as the runtime image's non-root application user.
- The development image explicitly installs curl for its HTTP health check.

Food-source circuit breakers now require two consecutive failures instead of one before opening for two minutes. Source-specific availability settings and consensus algorithms remain unchanged.

## Verification

```sh
dotnet restore Scale.io_API.sln
dotnet build Scale.io_API.sln --configuration Release --no-restore --warnaserror
dotnet test Scale.io_API.sln --configuration Release --no-build
```

HTTP tests use isolated SQLite databases and deterministic food fixtures. The PostgreSQL test is skipped unless `TEST_DATABASE_URL` points at a dedicated test database; CI always sets it. That test applies migrations, inserts a unique test user, exercises competing refresh updates using separate connections, and cleans up its user. Never point it at a production database.

## Remaining work

- Move to .NET 10 LTS before .NET 9 support ends on November 10, 2026; update SDK, target frameworks, Microsoft/EF packages, Npgsql provider, Docker images, and CI together. Current policy: https://dotnet.microsoft.com/en-us/platform/support/policy . This change retains the installed .NET 9 toolchain.
- Extend the consensus fixtures with recorded upstream payloads and source-ranking edge cases before tuning algorithms or adding providers. See `service-architecture.md` for the new responsibility boundaries and existing regression coverage.
- Implement account provisioning, logout/revocation, and authentication rate limits with a defined mobile workflow. There is still no registration endpoint.
- Consider shared caching only when running enough instances to justify it; current memory caches and circuit breakers are per process.
- Add pagination and stable meal identifiers when the mobile UI needs them; the response remains unchanged in this release.

Fly release configuration reference: https://fly.io/docs/reference/configuration/ .
