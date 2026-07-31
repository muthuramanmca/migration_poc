# Java → .NET Core API Migration Plan
**Role:** Senior .NET Architect view | **Scope:** Rewrite (not lift-and-shift) | **Method:** Claude-assisted, service-by-service | **Source:** Dummy Java app (no real codebase available yet)

---

## 1. Guiding Strategy

Because there's no live codebase yet, the plan is written to work against *any* dummy Java app you generate (Spring Boot is assumed as the typical case — call it out if yours is Jakarta EE/Micronaut/plain servlets instead, the mapping table below adjusts slightly).

Two strategic decisions up front:

**Rewrite pattern — Strangler Fig, not big-bang.** Migrate one bounded module/domain at a time (e.g., `UserService`, `OrderService`), stand it up in .NET Core behind the same API gateway/route prefix, and cut traffic over module by module. This is standard practice for backend rewrites because it keeps a working system at every step and limits blast radius. Even with a dummy app, structure the plan this way so it's directly reusable on the real one later.

**Unit of migration — the Java class/component, not the whole app.** You migrate one Controller → Service → Repository slice at a time, verify it, then move to the next. This maps naturally onto how you'll prompt Claude (below).

---

## 2. Architecture & Concept Mapping (Java → .NET Core)

| Java / Spring Boot | .NET Core Equivalent | Notes |
|---|---|---|
| `@RestController`, `@RequestMapping` | ASP.NET Core Controller (`[ApiController]`, `[Route]`) or Minimal API endpoint | Minimal APIs suit small services; Controllers suit larger, convention-heavy ones |
| `@Service` | Plain C# class registered via DI (`services.AddScoped<IFooService, FooService>()`) | No attribute needed; DI is constructor-based |
| `@Repository`, Spring Data JPA | EF Core `DbContext` + repository classes (or just EF Core directly) | Consider whether you even need a repository layer, or EF Core's `DbContext` is enough |
| `@Entity`, JPA annotations | EF Core entity classes + Fluent API / Data Annotations | Migrations via `dotnet ef migrations` |
| `application.yml` / `application.properties` | `appsettings.json` + `appsettings.{Environment}.json` | Strongly-typed via `IOptions<T>` |
| Spring Security + JWT | ASP.NET Core Identity / `Microsoft.AspNetCore.Authentication.JwtBearer` | Claims-based auth maps closely |
| Maven/Gradle | NuGet + MSBuild (`.csproj`) | |
| `@ExceptionHandler` / `@ControllerAdvice` | Custom `IExceptionFilter` or middleware (`app.UseExceptionHandler`) | |
| Spring AOP (logging, transactions) | Middleware, `DelegatingHandler`, or MediatR pipeline behaviors | |
| `@Transactional` | EF Core `DbContext` transactions / `IDbContextTransaction` | |
| Swagger via springdoc-openapi | Swashbuckle / `Microsoft.AspNetCore.OpenApi` | |
| JUnit + Mockito | xUnit/NUnit + Moq/NSubstitute | |
| Spring Boot Actuator | `Microsoft.Extensions.Diagnostics.HealthChecks` | |
| Kafka/RabbitMQ client (Spring) | MassTransit or native client SDKs | |

---

## 3. API Paradigm Decision (REST vs GraphQL vs gRPC)

Don't default to REST-for-REST — decide per module using these criteria, since the target platform doesn't need to mirror Java's shape 1:1:

- **REST (ASP.NET Core Web API / Minimal APIs):** default choice. Use for CRUD-style resources, public-facing APIs, anything consumed by third parties, or where the Java side is already REST and there's no reason to change the contract.
- **GraphQL (HotChocolate is the standard .NET library):** consider where the Java app exposes many overlapping REST endpoints to feed different frontend views (dashboard aggregation, mobile vs web needing different field subsets), or where over-/under-fetching is a known pain point today.
- **gRPC:** consider for internal service-to-service calls (not browser-facing) where you control both ends and want strong typing + performance — common if the Java app has internal microservice-to-microservice REST calls that could become gRPC.

Recommendation for this migration: **keep REST as the default for anything currently REST in Java**, and only introduce GraphQL/gRPC where you're deliberately fixing a known problem — don't add paradigm-switching risk on top of language-switching risk in the same pass. Revisit per-module during `02` discovery below.

---

## 4. Step-by-Step Process

**Prerequisite (not a numbered step):** this plan assumes a Java source app already exists to migrate from — `java-api/` in this repo. If starting from scratch with no real codebase yet, first generate a representative dummy Spring Boot app (Claude can do this) that mirrors the *shape* of complexity you expect from the real one: a handful of REST controllers, a service layer, JPA entities/repositories, DTOs, validation, a security filter, and one integration point — before beginning at `01`.

```mermaid
flowchart TD
    P01["01<br/>Setup .NET Skeleton<br/><small>Layers, conventions, cross-cutting setup</small>"]
    P02["02<br/>Create Java API Contract<br/><small>extract OpenAPI + group/order into slices</small>"]
    subgraph P03["03 — Java API, Slice-Wise (per slice)"]
        direction LR
        S0301["03_01<br/>Create slice behaviour doc"] --> S0302["03_02<br/>Manual approve behaviour doc<br/><small>manual</small>"]
    end
    subgraph P04["04 — .NET API Slice Develop (per approved slice)"]
        direction LR
        S0401["04_01<br/>Design note"] --> S0402["04_02<br/>Generate code"] --> S0403["04_03<br/>Unit test code"] --> S0404["04_04<br/>Compare Java/.NET results"] --> S0405["04_05<br/>Code review<br/><small>manual</small>"]
    end
    P05["05<br/>Cross-Cutting & Integration<br/><small>partial_manual</small>"]
    P06["06<br/>Validation Strategy<br/><small>partial_manual</small>"]
    P07["07<br/>Cutover<br/><small>manual</small>"]

    P01 --> P02 --> P03
    P03 -- "approved slice" --> P04
    P04 -- "next slice from 02's queue" --> P03
    P04 --> P05 --> P06 --> P07
```

| Step | Name | Input | Output | Manual? |
|---|---|---|---|---|
| `01` | Setup .NET Skeleton | Target stack preferences<br>(otherwise defaults below) | .NET solution skeleton<br>ADR doc<br>Cross-cutting scaffolding | — |
| `02` | Create Java API Contract | Running `java-api/`<br>with OpenAPI generator wired up | OpenAPI contract export<br>Endpoint inventory (by slice)<br>Dependency-ordered slice queue | — |
| `03_01` | Create Java API Slice Behaviour Doc | Java source for the slice<br>Slice's portion of `02` contract<br>Existing JUnit tests | Behavior spec markdown<br>(`migration_support/specs/`) | — |
| `03_02` | Manual Approve Behaviour Doc | Behavior spec from `03_01` | Status → `Spec Validated`<br>or → `Rework Needed` | **manual** |
| `04_01` | .NET Slice Design Note | Approved spec (`03_02`)<br>+ `01`'s conventions | Design note:<br>route, DTOs, service interface,<br>EF Core entity changes | — |
| `04_02` | .NET Slice Generate Code | Design note (`04_01`)<br>+ approved spec | .NET implementation:<br>Controller, Service, EF Core entity,<br>DTOs, validators | — |
| `04_03` | .NET Slice Unit Test Code | Approved spec<br>(**not** the generated code) | xUnit/NUnit tests<br>(spec rules + edge cases) | — |
| `04_04` | Java/.NET Compare Test Result | .NET test results<br>+ optional Java/.NET diff | Pass/fail report<br>+ behavioral diff | — |
| `04_05` | .NET Code Review | Generated code (`04_02`)<br>+ verify results (`04_04`) | Reviewed/approved code | **manual** |
| `05` | Cross-Cutting & Integration | *TBD* | *TBD* | **partial_manual** |
| `06` | Validation Strategy | *TBD* | *TBD* | **partial_manual** |
| `07` | Cutover | *TBD* | *TBD* | **manual** |

### 01 — Setup .NET Skeleton

**Implemented as:** a microservices architecture (Identity, FlightInventory, Booking, Notification,
Gateway + shared BuildingBlocks libraries), not the single-solution layout originally sketched below —
see `dotnet-api/docs/adr/0001-microservices-skeleton.md` for the actual decisions. The layered shape
described in step 1 still applies, just per-service instead of once for a monolith.

1. Create the target solution structure once, up front, so every migrated module drops into the same shape:
   - `Api` (Controllers/Minimal API endpoints, DI wiring, `Program.cs`)
   - `Application` (services, business logic, DTOs, validators)
   - `Domain` (entities, enums, domain logic)
   - `Infrastructure` (EF Core `DbContext`, repositories, external clients)
   - `Tests` (unit + integration)
2. Pick and document conventions up front: layered vs Clean Architecture, Minimal APIs vs Controllers, EF Core vs Dapper, FluentValidation vs Data Annotations, MediatR (CQRS) or not. Write this as a short ADR (architecture decision record) so it's consistent across every migrated module — this matters a lot when Claude is generating code across many sessions, since consistent conventions are what make the output composable.
3. Stand up cross-cutting concerns once: global exception handling, logging (Serilog), auth/JWT validation, health checks, Swagger/OpenAPI, standard response envelope/error format.

### 02 — Create Java API Contract

Before writing any C#, catalog what exists — this inventory becomes the acceptance spec for the .NET rewrite, i.e. what you check the new code against, not the Java source line-by-line.

**Extract the machine-readable API contract first.** Don't hand-catalog endpoints — pull the real contract:
- **Spring Boot:** add `springdoc-openapi` (`springdoc-openapi-starter-webmvc-ui`), run the app, and pull `/v3/api-docs` (JSON/YAML) plus browse `/swagger-ui.html`. It introspects `@RestController` classes at runtime, so it picks up every route, HTTP verb, request/response DTO shape, and status code without you tracing through the service layer by hand.
- **Older Spring apps:** check for `springfox-swagger2` instead (deprecated, but still common) — same idea, older annotation set.
- **Non-Spring Java (JAX-RS/Jersey):** use Eclipse MicroProfile OpenAPI or Swagger Core annotations (`io.swagger.core.v3`) directly on the resource classes.
- Save the generated `openapi.json` — it becomes the objective, diffable source of truth for "what the contract is," separate from the business-rule narrative you get from reading the service layer. Feed this to Claude alongside the Java source in `03_01` — it removes any ambiguity about exact routes, field names, and status codes.

**Group into slices and sequence them.** The OpenAPI export gives a flat list of endpoints; group them by which Controller/Service/Repository they actually share, since related endpoints (e.g., GET/POST/PUT/DELETE on the same resource) are almost always one slice, not several — migrating them together avoids re-reading the same service class multiple times and keeps shared business rules handled consistently. Then order the slices by dependency, not by list order: foundational modules (auth, users, shared lookups) go first, so that when a dependent module (orders, payments) comes up later, what it calls already exists on the .NET side to build and test against.

In this repo, `02`'s output is exactly `migration_support/dummy-api-contract.txt` (the raw export), `migration_support/api-inventory.csv` (grouped by slice), and `migration_support/migration-tracker.csv` (dependency-ordered queue with status tracking).

### 03 — Java API, Slice-Wise
Run once per slice, in `02`'s dependency order.

#### 03_01 — Create Java API Slice Behaviour Doc

Give Claude the Java file(s) for one slice and ask it to produce a plain-language behavior spec: inputs, outputs, validation, business rules, edge cases, error paths, side effects (DB writes, events published, external calls). Do not ask for code yet — ask for understanding. This is the highest-leverage step in the whole migration; errors here propagate into the rewrite, so it's worth a dedicated toolkit rather than just reading files top to bottom:

**Find logic in the places it's easy to miss.** The service layer is the obvious spot, but also check: entity getters/setters that do more than return a field, static utility/helper classes, `@ControllerAdvice` exception handlers (business meaning is often encoded in *which* exception maps to *which* response), custom Bean Validation annotations, AOP aspects (logging/transactions can carry hidden side effects), configuration-driven behavior in `application.yml` (feature flags, thresholds), and database triggers/stored procedures — code reading alone won't surface DB-level logic, so check the schema and any Flyway/Liquibase migration history directly.

**Mine the existing test suite before writing new documentation.** Existing JUnit tests are frequently the most reliable behavior spec that already exists, since test names and assertions state expected behavior explicitly, including edge cases someone already thought to cover. Read these before reading the implementation — they tell you *what* the code is supposed to do; the implementation just tells you what it currently does (which may include unintentional bugs you don't want to carry forward). A JaCoCo coverage report also flags which code paths have *no* test coverage — those are the highest-risk areas during migration since there's no safety net confirming current behavior.

**Capture real behavior, not just read code.** Where possible, pull real request/response pairs from access logs, an API gateway, or a proxy capture while the Java app is running. This becomes a golden dataset you can replay against the new .NET service in `06` validation — much stronger than eyeballing whether the rewrite "looks right."

**Use structured formats instead of free-form prose for business rules** — these translate far more reliably into both human review and Claude-generated code/tests than a paragraph description:
- **Decision tables** for conditional/branching logic (pricing rules, eligibility checks, validation chains) — rows of condition combinations to outcomes.
- **Given/When/Then (Gherkin)** scenarios per business rule — doubles as living documentation and can be turned directly into executable acceptance tests on the .NET side via Reqnroll/SpecFlow, closing the loop between spec and test.
- **State/lifecycle diagrams** for any entity with a status field (Order, Payment, Ticket, etc.) — these almost always hide transition rules ("can only cancel if status is PENDING") that are easy to miss reading service methods in isolation.
- **Sequence diagrams** for any flow spanning multiple classes/services — clarifies transaction boundaries, what's synchronous vs. async, and where side effects (events, external calls) actually fire.

**Use tooling to map structure before reading line by line.** SonarQube (or a free complexity check) flags high-cyclomatic-complexity methods worth extra scrutiny; IDE call-hierarchy/structure views (or `jdeps`) show what actually calls what, which matters because business logic in Java frequently spans Controller → Service → Repository → Entity, so reading one class in isolation misses context. Feed Claude the whole slice together for the same reason, not one file at a time.

**Check history for the "why," not just the "what."** Git blame, commit messages, and PR descriptions often reference the ticket or compliance reason a rule exists — that context doesn't live in the code and is easy to lose in a rewrite.

**Standardize the output per slice:**
1. Purpose/responsibility (one paragraph)
2. Endpoints — from the `02` OpenAPI export (routes, verbs, request/response shapes, status codes)
3. Business rules — as decision tables and/or Given/When/Then scenarios
4. Entity state/lifecycle diagram (if applicable)
5. Dependencies — internal services called, external APIs, DB tables touched
6. Error handling & edge cases
7. Non-functional notes — auth requirements, known perf issues, rate limits

Store these as versioned markdown alongside the code, one file per slice — this becomes both the spec Claude generates the .NET implementation from, and the living documentation that outlives the migration itself.

#### 03_02 — Manual Approve Behaviour Doc *(manual)*

Human review and sign-off. No `.NET` code is generated for a slice until this gate passes — errors caught here are cheap to fix; errors caught after `04_02` are not.

### 04 — .NET API Slice Develop (per approved behaviour doc)
Run once per slice that has passed `03_02`.

#### 04_01 — .NET Slice Design Note
No method bodies yet; this is contract-level design, not implementation.

#### 04_02 — .NET Slice Generate Code
Generated from the *behavior spec* (not a literal line-by-line port), following the `01` conventions.

#### 04_03 — .NET Slice Unit Test Code
Tests are prompted from the spec, not the generated code, so they validate against the spec rather than rubber-stamping whatever `04_02` produced.

#### 04_04 — Java/.NET Compare Test Result
Optionally, run both Java and .NET side by side against the same inputs (a small script or Postman/Insomnia collection) and diff the responses.

#### 04_05 — .NET Code Review *(manual)*
Naming, error handling, security (authz checks preserved?), performance (N+1 queries from EF Core is a common regression point coming from JPA).

Then repeat `03`→`04` for the next slice in `02`'s queue.

### 05 — Cross-Cutting & Integration *(partial_manual)*
Once individual slices are migrated, wire up what spans them: shared auth, consistent error envelope, API versioning, rate limiting, logging/correlation IDs, and any inter-service calls. Re-test integration points specifically, since these are where per-slice migration is most likely to silently diverge from the original. Tagged `partial_manual`: much of the cross-cutting code itself is generatable, but re-testing integration across already-migrated slices needs human coordination. *(Full input/output breakdown TBD — see table above.)*

### 06 — Validation Strategy *(partial_manual)*
- Contract tests against the documented API shape: diff the springdoc-openapi spec captured in `02` against the Swashbuckle-generated spec from the new .NET service (routes, verbs, field names/types, status codes) to catch drift objectively instead of relying on manual comparison.
- Parallel/shadow run where feasible: route a copy of real traffic to both Java and .NET, compare responses, before cutting over.
- Load/perf baseline comparison — EF Core and JPA have different default behaviors (lazy loading, query generation); don't assume perf parity without measuring.

Tagged `partial_manual`: contract-diff tooling and test scripts are generatable, but running shadow traffic/load tests against live environments and interpreting results is manual. *(Full input/output breakdown TBD — see table above.)*

### 07 — Cutover *(manual)*
Module-by-module traffic shift at the gateway/reverse-proxy level (percentage-based or by route), not a single flip for the whole app. Keep the Java module reachable as a fallback until the .NET replacement has run clean in production for an agreed soak period.

Tagged `manual`: production traffic-shifting, soak-period monitoring, and rollback decisions are operational actions outside what generated code can do. *(Full input/output breakdown TBD — see table above.)*

---

## 5. Worked Example (Dummy Slice)

To make this concrete, here's the pattern applied to a single typical slice — happy to actually generate this dummy Java class and its .NET rewrite next if useful:

- **Java side:** `ProductController` (GET/POST `/api/products`) → `ProductService` (validation + stock-check business rule) → `ProductRepository` (Spring Data JPA) → `Product` entity.
- **03_01 output (behavior spec):** "GET /api/products/{id} returns 404 if not found, 200 with ProductDto otherwise. POST /api/products validates name (required, max 100 chars) and price (> 0), rejects duplicate SKU with 409, persists via repository, publishes `ProductCreatedEvent`."
- **04_02 output:** ASP.NET Core `ProductsController` (or minimal API group), `IProductService`/`ProductService`, EF Core `Product` entity + `AppDbContext`, FluentValidation validator, same status codes.
- **04_03 output:** xUnit tests covering not-found, duplicate SKU, and the validation edge cases from the spec.

---

## 6. Claude Usage Playbook

Since Claude is the execution engine for this migration, treat each `03_XX`/`04_XX` step as a distinct prompt, not one mega-prompt:

- Keep `03_01` and `04_02` as separate turns — reviewing the behavior spec before code exists is what catches misread business logic early, when it's cheap to fix.
- Paste the actual Java source for the slice being migrated each time, plus the `01` ADR/conventions doc, so output stays consistent across sessions/slices.
- Ask explicitly for edge cases and error paths in the behavior spec — these are the most common thing lost in a rewrite.
- For tests, prompt from the spec, not from "write tests for this code," to avoid tests that just mirror bugs in the generated implementation.
- Periodically ask Claude to review a batch of already-migrated slices together for consistency (naming, error format, DTO conventions) — drift creeps in across long sessions.

---

## 7. Risks & Mitigations

Common failure modes in Java→.NET rewrites, worth watching for explicitly: silently dropped business rules embedded deep in Java service methods (mitigated by the spec-first step), N+1 query regressions from EF Core defaults differing from JPA, authz rules re-implemented incorrectly (test authz paths explicitly, not just happy paths), and validation rule mismatches (e.g., Bean Validation vs FluentValidation edge-case differences like empty-string vs null handling).

---

## 8. Beyond a 1:1 Port — Performance, Security, Scalability, Maintainability

A rewrite is the one chance to fix things a straight port would carry over unchanged. These are concrete .NET Core choices worth deciding on now, in `01`, rather than discovering the need mid-migration.

### Performance
- **Minimal APIs** for high-throughput/simple endpoints — materially less per-request overhead than MVC controllers; use Controllers where you need the structure (filters, conventions) instead.
- **`System.Text.Json`** (default) over Newtonsoft.Json — faster and lower-allocation; only pull in Newtonsoft if a specific serialization quirk from the Java side requires it.
- **EF Core query discipline**: `AsNoTracking()` for read-only queries, project to DTOs instead of loading full entity graphs, and watch for N+1s explicitly — this is the single most common performance regression teams hit coming from JPA, since EF Core's lazy-loading defaults differ.
- **Output/response caching** (`Microsoft.AspNetCore.OutputCaching`) and a distributed cache (Redis) for shared state across instances — same role as Ehcache/Caffeine/Redis on the Java side.
- **gRPC for internal service-to-service calls** where both ends are under your control — protobuf beats JSON-over-REST on latency and payload size for high-volume internal traffic.
- **Native AOT** (.NET 8+) if these are containerized and startup time/memory footprint matter (e.g., scale-to-zero, fast pod restarts) — no direct JVM equivalent; this is a genuine advantage over the Java baseline.
- Benchmark with the **same load-testing tool against both stacks** (k6 or JMeter) so performance comparisons are apples-to-apples, not vibes.

### Security
- **OWASP parity pass**: walk the OWASP Top 10 against what Spring Security currently enforces (CORS policy, CSRF handling, security headers) and confirm each is explicitly configured in ASP.NET Core — nothing carries over automatically just because the framework changed.
- **Secrets management**: move off anything hardcoded/`application.yml`-embedded into a proper secrets store (Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault) — don't let secrets handling regress during the rewrite.
- **Built-in NuGet vulnerability audit** (`dotnet list package --vulnerable`, or NuGet Audit in .NET 8 SDK) as the equivalent of the OWASP Dependency-Check Maven plugin — wire it into CI from day one.
- **FluentValidation everywhere input crosses a trust boundary** — consistent validation is also your first line of defense against injection-style bugs.
- **mTLS or at least network-policy isolation** for internal service-to-service calls if the target is a multi-service/microservices layout, not just perimeter auth.
- **Security headers middleware** (HSTS, CSP, X-Frame-Options) — easy to forget since Spring Security sets some of these by convention; ASP.NET Core does not.

### Scalability
- **Stateless services by design** — externalize session/state to Redis (or equivalent) so any instance can serve any request; this is what actually makes horizontal scaling and the strangler-fig traffic-shifting in `07` work cleanly.
- **Resilience via Polly** (retry, circuit breaker, timeout, bulkhead policies) wrapping outbound calls — the .NET equivalent of Resilience4j; add this now rather than bolting it on after the first cascading failure.
- **Event-driven decoupling** for anything that doesn't need a synchronous response — MassTransit over Kafka/RabbitMQ/Azure Service Bus, matching whatever async messaging pattern (if any) already exists in the Java app, or introducing it where synchronous call chains are currently a scaling bottleneck.
- **CQRS + read replicas** (MediatR for the in-process pattern) for specifically read-heavy modules identified during `02` discovery — don't apply this everywhere, only where the access pattern justifies it.
- Container-native from the start: Docker + Kubernetes (or whatever the Java app already runs on) with health checks wired to k8s liveness/readiness probes, and autoscaling rules tied to real metrics (CPU, queue depth), not guesses.

### Maintainability
- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`) — compile-time null-safety that Java doesn't have natively; turning this on early prevents a large class of runtime NullReferenceExceptions the Java version may currently tolerate at runtime.
- **Roslyn analyzers + `.editorconfig`** enforced in CI, as the equivalent of Checkstyle/SpotBugs/PMD on the Java side — keep code style and common-bug detection automated, not review-dependent.
- **OpenTelemetry** for logging, metrics, and distributed tracing — vendor-neutral and usable on both the Java and .NET side simultaneously, which is genuinely useful *during* the migration: you can trace a request across old and new services in the same view while modules are being cut over.
- **API versioning from the first release** (`Asp.Versioning.Http`) so later modules can evolve without breaking already-migrated consumers.
- **Feature flags** for the cutover itself (even a simple config-based flag), giving you a faster rollback lever than a full gateway re-route if a freshly migrated module misbehaves in production.
- Treat the **`02` OpenAPI spec as living documentation**, not a one-time export — regenerate and diff it on every change so the contract and the code can't silently drift apart.
