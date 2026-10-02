# Scale.io API

ASP.NET Core API with PostgreSQL persistence and food-source aggregation.

- [Local setup, deployment, and maintenance](docs/operations.md)
- [Mobile API contract and migration guide](docs/mobile-api-contract.md)
- [Service responsibilities and extension guide](docs/service-architecture.md)
- [Prioritized API implementation roadmap](docs/implementation-roadmap.md)

Start locally with `docker compose up --build`; the API listens at `http://localhost:5175`.
Run regression tests with `dotnet test Scale.io_API.sln`.
