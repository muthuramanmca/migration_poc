# dotnet-api

Target .NET platform for the United Airlines migration PoC — a **microservices architecture** for
a real airline ticket-booking domain, not a 1:1 port of `java-api`'s three dummy slices. See
`docs/adr/0001-microservices-skeleton.md` for the full reasoning behind every decision below, and
`Java-to-DotNetCore-Migration-Plan.md` at the repo root for the overall migration workflow.

**Status: skeleton.** Project structure, DI wiring, cross-cutting middleware, the Booking-creation
saga, and the outbox/JWKS/YARP-routing infrastructure are real and build/test-verified. Business
logic (real registration/login, real seat-hold/decrement, real booking rules) is not implemented yet
— it lands per-service through the migration plan's `02`-`04` pipeline.

## Structure

```
dotnet-api/
├── dotnet-api.slnx                    ← .NET 10 SDK's new solution format
├── Directory.Build.props / Directory.Packages.props  ← shared build settings, Central Package Management
├── .editorconfig
├── docs/adr/0001-microservices-skeleton.md
├── .devcontainer/devcontainer.json    ← pins .NET 10 SDK for Codespaces
├── aspire/
│   ├── AppHost/                       ← orchestrates all services + SQL Server + RabbitMQ locally
│   └── ServiceDefaults/               ← shared OpenTelemetry/health-check/resilience wiring
├── gateway/Gateway/                   ← YARP reverse proxy, edge JWT validation, rate limiting
├── services/
│   ├── identity/           ← auth, hardened JWT issuance (RS256 + JWKS)
│   ├── flight-inventory/   ← flights/fares/seat inventory
│   ├── booking/            ← reservation lifecycle, hosts the Booking-creation saga
│   └── notification/       ← pure event consumer (booking confirmations), no client-facing API
└── building-blocks/                   ← shared cross-cutting infrastructure, never shared business logic
    ├── BuildingBlocks.Common          ← error envelope, correlation-ID middleware
    ├── BuildingBlocks.Contracts       ← integration event/command records
    ├── BuildingBlocks.Messaging       ← MassTransit + outbox registration
    ├── BuildingBlocks.Observability   ← Serilog + PII-redaction guardrail
    └── BuildingBlocks.Security        ← JWT validation, role constants
```

Each service (`identity`, `flight-inventory`, `booking`, `notification`) follows the same
`{Service}.Api / .Application / .Domain / .Infrastructure / .Tests` layering, applied per-service
rather than once for a monolith.

## Run it

**Requires .NET 10 SDK and Docker** (for Aspire's SQL Server/RabbitMQ containers). Docker was not
available in the environment this skeleton was built in — the `AppHost` was confirmed to start
correctly (dashboard comes up, resource graph resolves) but fails at container provisioning without
it. If you hit the same gap, follow the `.devcontainer/devcontainer.json` setup or run in a
Docker-enabled Codespace.

```bash
cd dotnet-api/aspire/AppHost
dotnet run
```

Opens the Aspire dashboard with all 5 services (Gateway + 4 services) plus SQL Server and RabbitMQ.

## Verify without Docker

Every service was verified independently without needing live infrastructure:

```bash
dotnet build dotnet-api.slnx      # 0 errors, 0 known package vulnerabilities
dotnet test dotnet-api.slnx       # in-process WebApplicationFactory smoke tests per service,
                                   # plus BookingCreationSagaTests (MassTransit in-memory harness)
```

To confirm a service's EF Core model is valid:

```bash
cd services/identity/Identity.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../Identity.Api/Identity.Api.csproj --context IdentityDbContext -o Migrations
```

(Already run once for all four services; migrations are checked in.)
