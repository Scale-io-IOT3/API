# Scale.io API

ASP.NET Core API on .NET 10 LTS with PostgreSQL persistence and food-source aggregation.

- [Local setup, deployment, and maintenance](docs/operations.md)
- [Mobile API contract and migration guide](docs/mobile-api-contract.md)
- [Service responsibilities and extension guide](docs/service-architecture.md)
- [Prioritized API implementation roadmap](docs/implementation-roadmap.md)

Start locally with `docker compose up --build`; the API listens at `http://localhost:5175`.
Watch mode defaults to off. Toggle it in the running container with
`docker compose exec scale.io api-watch on` or `docker compose exec scale.io api-watch off`.
Check mode and API readiness with `docker compose exec scale.io api-watch status`.
Each mode change restarts the API inside the same container and rebuilds as needed.
To start with watch enabled, use `API_WATCH_MODE=on docker compose up --build`.
On Apple Silicon, prefix Compose commands with `API_DOCKER_PLATFORM=linux/arm64`
to explicitly select ARM64; native architecture is the default. Container restarts reset watch mode to `API_WATCH_MODE`.
Run regression tests with `dotnet test Scale.io_API.sln`.
Host builds and tests require a .NET 10 SDK compatible with `global.json`.

The development port binds to localhost by default; set `API_BIND_ADDRESS=0.0.0.0` for device testing or `API_PORT` for another port. Docker verification and image update instructions are in [the operations guide](docs/operations.md#verification).
