# dotnet-api

Target .NET Core solution for the Java → .NET Core migration (see
`Java-to-DotNetCore-Migration-Plan.md` at the repo root and
`migration_support/migration-tracker.csv` for current status).

This folder is currently empty. Per the migration workflow, no .NET code is
generated until every slice's behavior spec under `migration_support/specs/`
is marked `Spec Validated`. Once code generation begins (Phase 3, step 3),
the solution will be scaffolded here as:

```
dotnet-api/
├── Api/            ← Controllers/Minimal API, DI wiring, Program.cs
├── Application/    ← Services, business logic, DTOs, validators
├── Domain/         ← Entities, enums, domain logic
├── Infrastructure/ ← EF Core DbContext, repositories, external clients
└── Tests/          ← Unit + integration
```

See `Java-to-DotNetCore-Migration-Plan.md` Section 8 for the concrete
architectural choices (nullable reference types, EF Core query discipline,
`System.Text.Json`, Polly, OpenTelemetry).
