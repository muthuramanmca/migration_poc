# ADR 0001: Microservices Skeleton for the Airline Ticket-Booking Platform

**Status:** Accepted (skeleton implemented). **Date:** 2026-07-31.

## Context

`dotnet-api/` is the target .NET Core platform for the United Airlines migration PoC. The original
placeholder (a single layered solution: `Api/Application/Domain/Infrastructure/Tests`) was superseded
by an explicit request for a **microservices architecture** for a real airline ticket-booking domain —
not a 1:1 port of `java-api`'s three generic dummy slices (`user`/`product`/`order`). This ADR records
the resulting design and the reasoning behind it, per the migration plan's `01_setup_net_skeleton` step.

This is a **skeleton**: project structure, DI wiring, cross-cutting middleware, and infrastructure
(saga, outbox, JWKS, YARP routing) are real and build/test-verified. Business logic (real
registration/login, real seat-hold/decrement, real booking rules) is deferred to each service's
future `02`-`04` migration pass.

## Decisions

### Service decomposition
Four services + a Gateway, decoupled from `java-api`'s slice names:

- **Identity** — passenger/staff auth, hardened JWT issuance.
- **FlightInventory** — flights/fares/seat inventory.
- **Booking** — reservation lifecycle, hosts the Booking-creation saga.
- **Notification** — pure event consumer (booking confirmations), no client-facing API.
- **Gateway** — YARP reverse proxy, edge JWT validation, rate limiting, security headers.

**No Payment service** — explicitly out of scope for this PoC (user decision). The saga's design
still allows a payment step to be inserted later without a redesign (see Saga section below), but no
placeholder project was scaffolded for it.

Each service follows the same `Api/Application/Domain/Infrastructure/Tests` layering as the original
plan, just applied per-service instead of once for a monolith. `BuildingBlocks.*` are shared
*infrastructure* libraries only — never shared domain/business logic, to preserve service autonomy.

### Repo layout
Single monorepo under `dotnet-api/`, one `dotnet-api.slnx` (the .NET 10 SDK's new solution format —
not the classic `.sln`) referencing all ~28 projects. Central Package Management
(`Directory.Packages.props`) pins every NuGet version once, solution-wide.

### Target framework: .NET 10, not .NET 8
The plan's earlier drafts assumed .NET 8 (LTS at time of writing). The actual installed SDK on this
machine is **10.0.300**, and .NET 10 is the current LTS as of the implementation date — so the
skeleton targets `net10.0` throughout. This also meant using current package versions rather than the
plan's originally-drafted ones (see "Version corrections" below).

### Saga: orchestration, not choreography
`BookingCreationSaga` (`MassTransitStateMachine<BookingSagaState>`, hosted in `Booking.Infrastructure`)
orchestrates `BookingRequested → HoldSeat → SeatHeld/SeatHoldFailed → BookingConfirmed/BookingCreationFailed`.
Chosen over choreography because two participants (Booking, FlightInventory) is too small a fan-out
for choreography's decoupling benefits to outweigh losing one legible owner of "did my booking
succeed." Saga state persists in `Booking.Db` (database-per-service — never a shared saga store).

`ReleaseSeat` (the compensating command) has a real consumer on FlightInventory's side, but nothing in
the current saga triggers it yet: `SeatHeld` transitions straight to `Confirmed` with no further step
that can fail after the hold succeeds. The trigger arrives once a real post-hold step exists (a
Booking confirmation write that can fail, or a future Payment step) — wiring a synthetic failure path
now would be fictitious, so it was deliberately left for that pass. Verified via
`BookingCreationSagaTests` using MassTransit's in-memory test harness (both the happy path and the
`SeatHoldFailed` path pass).

### Outbox
`AddEntityFrameworkOutbox<TDbContext>` (MassTransit's built-in transactional outbox, not a hand-rolled
table+poller) on `BookingDbContext` and `FlightInventoryDbContext` — any service publishing an event as
part of a local DB write needs it. This is also the direct fix for the bug flagged in
`java-api`'s Orders spec: the event firing *inside* the open transaction before commit. The outbox
guarantees publish-iff-commit by construction. Identity and Notification don't need it (Identity
publishes nothing yet; Notification only consumes).

### API Gateway: YARP over Ocelot
Actively maintained by Microsoft, built on Kestrel/`HttpClient` pipelines. Routes preserve the airline
domain directly (`/api/identity`, `/api/flights`, `/api/bookings`, `/api/notifications`) — no
dependency on `java-api`'s route shape, since this skeleton isn't a literal port. Destinations resolve
via `Microsoft.Extensions.ServiceDiscovery.Yarp` against Aspire's service-discovery env vars
(`http://identity-api` etc.), not hardcoded ports.

### Data ownership
Database-per-service, **SQL Server** (user decision — over PostgreSQL), one EF Core `DbContext` per
service, no cross-service DB access ever, not even read-only joins.

### Security — built now, not deferred
Per explicit instruction ("we need implement better security"), Identity's JWT design is hardened
beyond the `java-api` baseline (flat HS256, 1hr, no refresh):
- **RS256 + JWKS** (`GET /.well-known/jwks.json` on Identity, `RsaSigningKeyProvider`) — asymmetric,
  no shared secret across services. Every service (including Gateway) validates independently against
  this JWKS — zero trust, the network boundary is never the only defense.
- **Refresh tokens**: `RefreshToken` entity + repository scaffolded now; rotation/reuse-detection logic
  is deferred to Identity's business-logic pass.
- **MFA hook point**: `IMfaChallengeProvider` interface, default `NoOpMfaChallengeProvider`
  registration — wired into the shape now, a real TOTP/SMS provider lands later.
- **Rate limiting**: fixed-window limiter on the Gateway's identity route (`RateLimiterPolicy: "auth"`
  in `Gateway/appsettings.json`) — mitigates credential-stuffing against loyalty accounts.
- **PII guardrail from day one**: `SensitiveDataAttribute` (`BuildingBlocks.Common`) +
  `SensitiveDataDestructuringPolicy` (`BuildingBlocks.Observability`) redact any property tagged
  sensitive before it reaches any Serilog sink — applies automatically the moment a real PII field
  (passport, DOB, etc.) is added later, no logging call sites to update.
- **Security headers + CORS**: set explicitly at the Gateway (HSTS, CSP, X-Frame-Options,
  X-Content-Type-Options) — ASP.NET Core doesn't set these by convention the way Spring Security does.

**Known skeleton limitation**: the RSA signing key is generated in-memory and ephemeral (regenerates on
every restart). A real deployment needs to load/persist/rotate a key (e.g. from a key vault) — this is
explicitly deferred, not an oversight; see the `RsaSigningKeyProvider` doc comment.

### Known layering tradeoff
`SensitiveDataAttribute` lives in `BuildingBlocks.Common`, which every `*.Domain` project now
references (for the attribute alone) even though `Common` also carries `ApiException`/middleware
types that need `FrameworkReference Microsoft.AspNetCore.App`. This pulls the ASP.NET Core shared
framework reference transitively into Domain projects that, in a purist hexagonal-architecture sense,
shouldn't need it. Accepted as a pragmatic tradeoff for a skeleton — every Domain project here ends up
inside a web service anyway, so splitting out a sixth zero-dependency `BuildingBlocks.Abstractions`
project for one marker attribute wasn't judged worth the added indirection at this stage.

### Local dev orchestration: .NET Aspire
`AppHost` orchestrates all 5 services + SQL Server + RabbitMQ. **Confirmed on this local Windows
machine**: no Docker is available, and the AppHost genuinely starts (dashboard comes up, resource
graph resolves) but fails at container provisioning with `Container runtime 'docker' could not be
found` — an environment gap, not a wiring bug. Every individual service was instead verified via
`dotnet build`/`dotnet test` (in-process `WebApplicationFactory` smoke tests, no Docker needed).

**Confirmed end-to-end in a GitHub Codespace built from `dotnet-api/.devcontainer/devcontainer.json`**
(a new Codespace, distinct from the one already in use for `java-api` — that older one predates this
devcontainer config and lacks Docker-in-Docker): `dotnet --version` reports `10.0.200`, `docker
version` succeeds, and `dotnet run` on the AppHost starts all 5 services plus SQL Server and RabbitMQ
containers with zero manual environment fixing (no repeat of the JDK 11-vs-17 fight `java-api` needed
— the pinned SDK version closed that gap as intended). Verified via `curl` *inside* the Codespace
against each service's `localhost` port (bypasses the public `*.app.github.dev` forwarding URL, whose
DNS can lag for a few minutes right after a brand-new Codespace is created):
- `curl -sk -L http://localhost:5147/.well-known/jwks.json` → real RS256 JWKS JSON with a populated
  `keys` array — proves `RsaSigningKeyProvider` and the JWKS endpoint work for real, under real SQL
  Server, not just in an in-memory test.
- `curl -sk -L http://localhost:5196/alive` → `200` — Gateway healthy with YARP routing configured
  against live service-discovery-resolved destinations.

(`-L` follows the `UseHttpsRedirection()` 307 from the HTTP port to HTTPS; `-k` skips the self-signed
dev certificate.)

### Deliberately not built now
- **Redis** — mentioned in the wider migration plan as a future FlightInventory caching /
  Gateway rate-limit-store option, but nothing consumes it yet, so no Aspire resource or NuGet package
  was added for it. Provisioning unused infrastructure was judged worse than adding it when a real
  consumer exists.
- **CQRS/MediatR** — not adopted; no read-heavy access pattern has been identified yet to justify it.
- **mTLS between services** — network-policy isolation is the baseline; mTLS is flagged as the
  target-state hardening step, not built now.
- Any real business endpoint (registration, login, flight search, booking creation) — deferred to each
  slice's `02`-`04` migration pass, per explicit "skeleton only" instruction.

## Version corrections made during implementation

Several package versions guessed while drafting the plan didn't exist or were outdated; corrected
against the actual NuGet registry and, for ASP.NET Core/EF Core packages, pinned to match the installed
runtime (`10.0.8`) rather than a newer patch that might assume a runtime not present locally:

- `Microsoft.OpenApi` bumped from the default `2.0.0` (GHSA-v5pm-xwqc-g5wc, high severity) to `2.11.0`
  — **not** the `3.x` line, which is a breaking API change incompatible with
  `Microsoft.AspNetCore.OpenApi`'s source generator.
- `Asp.Versioning.Http`: `10.0.1`, not the guessed `8.1.0`.
- `Serilog.AspNetCore` `10.0.0` required `Serilog.Sinks.Console >= 6.1.1` (the initially-pinned `6.0.0`
  caused a central-package-management downgrade error across every project).
- MassTransit's EF Core outbox/saga-repository extension methods (`AddOutboxMessageEntity`,
  `EntityFrameworkRepository`, etc.) live in the `MassTransit` namespace, not
  `MassTransit.EntityFrameworkCoreIntegration` as the older docs/memory suggested.
- Aspire's workload-based installation is deprecated as of this SDK version; Aspire ships as plain
  NuGet packages + `Aspire.ProjectTemplates` now (`dotnet new install Aspire.ProjectTemplates`).

## Verification performed

- `dotnet build dotnet-api.slnx` — clean, 0 errors, no known package vulnerabilities
  (`dotnet list package --vulnerable` clean across all 28 projects).
- Identity, FlightInventory, Booking, Notification: each has a passing `WebApplicationFactory`-based
  smoke test proving the full DI graph resolves and the app serves a real HTTP request, without
  needing a live SQL Server/RabbitMQ.
- `BookingCreationSagaTests`: two passing tests (happy path and `SeatHoldFailed` path) using
  MassTransit's in-memory test harness — the saga's states/events/transitions are proven correct, not
  just compiling.
- Gateway: manually run and confirmed YARP config loads and `/alive` returns `200 Healthy`.
- **Full AppHost, in a Docker-enabled GitHub Codespace: all 5 services + SQL Server + RabbitMQ started
  cleanly; Identity's real JWKS endpoint and Gateway's `/alive` both verified via `curl` against the
  running containers** (see "Local dev orchestration" above for exact commands/output). This is the
  strongest verification level in this ADR — real infrastructure, not test doubles or in-memory
  substitutes.

## Follow-ups (not part of this ADR's scope)

- Update `dotnet-api/README.md` to describe this structure (done alongside this ADR).
- Update `CLAUDE.md` / `Java-to-DotNetCore-Migration-Plan.md` to note `dotnet-api/`'s service
  boundaries are an independent airline-domain design, not a 1:1 port of `java-api`'s three slices.
- ~~Confirm Docker availability in whatever environment will actually run the full AppHost~~ — done;
  see "Local dev orchestration" above.
- View the Aspire dashboard itself (not just `curl` the services) to visually confirm every resource's
  status — blocked so far by the brand-new Codespace's `*.app.github.dev` subdomain not yet resolving
  in the browser (DNS propagation lag, not an app issue — confirmed via `curl` inside the Codespace
  instead). Revisit once DNS catches up, or from a different network.
