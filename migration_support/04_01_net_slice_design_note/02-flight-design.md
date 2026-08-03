# Design Note — Slice 2: Flight → FlightInventory

**Behavior spec:** `migration_support/03_01_java_api_slice_behaviour_doc/02-flight-spec.md` (Spec Validated)
**Status:** Design Note Ready
**Projects touched:** `FlightInventory.Api`, `FlightInventory.Application`, `FlightInventory.Domain`, `FlightInventory.Infrastructure`
**Also touched:** `BuildingBlocks.Security` (one shared authorization policy — see §5.1)

Contract-level only, per `04_01_net_slice_design_note/README.md` — routes, DTO shapes, service
interface signatures, EF Core entity/config changes. No method bodies; those land in `04_02`.

---

## 1. Endpoints

The Gateway's `flights-route` matches `/api/flights/{**catch-all}` with **no path-rewrite
transform** (`Gateway/appsettings.json`), so it forwards the full incoming path unchanged.
Unlike Identity — whose routes had to grow an `/api/identity` prefix — **FlightInventory's local
routes are byte-identical to `java-api`'s**. Nothing about the path shape changes in this slice.

Minimal APIs in a static `FlightEndpoints.MapFlightEndpoints(this WebApplication)`, mirroring
`Identity.Api/IdentityEndpoints.cs`. `FlightInventory.Api/Program.cs` currently maps nothing but
health endpoints.

| Method | Local path | Operation | Auth | Request | Response | Success |
|---|---|---|---|---|---|---|
| GET | `/api/flights` | List | Public | — | `FlightResponse[]` | 200 |
| GET | `/api/flights/{id:guid}` | Get | Public | — | `FlightResponse` | 200 |
| POST | `/api/flights` | Create | `Admin` | `FlightRequest` | `FlightResponse` | **201** |
| PUT | `/api/flights/{id:guid}/seats` | AdjustSeats | `Admin` | `SeatAdjustmentRequest` | `FlightResponse` | 200 |
| DELETE | `/api/flights/{id:guid}` | Delete | `Admin` | — | — | **204** |

- **201 and 204 are preserved** per spec §2 — the code, not the springdoc export, is authoritative.
  `Results.Json(response, statusCode: StatusCodes.Status201Created)` and `Results.NoContent()`.
- **⚠ Id shape changes: Java's `Long` → `Guid`** (route constraint `{id:guid}`). This is ADR 0001's
  project-wide PK convention, not re-litigated here — but it is a **breaking wire change** for any
  client holding numeric flight ids, and flight ids also travel inside
  `BookingLineItem.FlightId` (already `Guid` in `BuildingBlocks.Contracts`). Called out because
  it is the one place this slice cannot be wire-identical to Java.
- The two GET routes get an **explicit `.AllowAnonymous()`**. They would be anonymous by default
  today (`AddAuthorization()` registers no fallback policy), but stating it documents spec rule
  §2's deliberate public-schedule decision and survives a future fallback policy.

## 2. DTOs (field-level, C# records — `FlightInventory.Application/Dtos/`)

One file per record under `Dtos/`, matching how Identity's DTOs actually landed — that layout
question was settled at Identity's `04_01b` gate and is now convention, not a fresh decision.

```
FlightRequest         { string FlightNumber; string Origin; string Destination;
                        DateTimeOffset DepartureAt; decimal Fare; int? SeatCapacity; }
SeatAdjustmentRequest { int? Delta; }
FlightResponse        { Guid Id; string FlightNumber; string Origin; string Destination;
                        DateTimeOffset DepartureAt; decimal Fare; int SeatCapacity;
                        bool LowSeatAvailability; }
```

Matches spec §3 field-for-field, with three deliberate shape choices:

- **`DepartureAt`, not `DepartureAtUtc`, on the DTOs.** The solution's `*Utc` suffix convention
  (`CreatedAtUtc`, `ConfirmedAtUtc`) applies to the *entity* property; the DTO keeps Java's field
  name so the JSON wire contract is unchanged. This matters during Strangler Fig cutover, when
  both apps may serve the same clients through the Gateway. `Instant` → `DateTimeOffset`,
  serialized ISO-8601 UTC as before.
- **`decimal Fare`** with `HasPrecision(18, 2)` on the entity (§3) — never `double`.
- **`int?` on `SeatCapacity` and `Delta`** — nullable so a *missing* field is distinguishable
  from an explicit `0`. This is what makes spec decisions §10.2 and §10.4 implementable at all;
  see §7. If either decision is overridden to "preserve Java behavior", the corresponding
  property reverts to a plain `int`.

## 3. Entity / EF Core changes

`FlightInventory.Domain/Flight.cs` is an explicit placeholder ("shape only, enough for EF Core
migrations to run"). It has `Id`, `FlightNumber`, `DepartureAtUtc`, `AvailableSeats` — **half the
spec's fields are missing.** This slice is where the real entity lands.

| Change | Detail |
|---|---|
| **Add** `Origin`, `Destination` | `string`, required, `HasMaxLength(64)` |
| **Add** `Fare` | `decimal`, required, `HasPrecision(18, 2)` |
| **Add** `Active` | `bool`, required, default `true` — backs soft delete (spec rule 4.6) |
| **Add** `RowVersion` | `byte[]`, `IsRowVersion()` — concurrency token, see §7.6 |
| **Rename** `AvailableSeats` → `SeatCapacity` | Keeps a 1:1 name mapping to the spec/Java wire field so `04_04`'s compare is honest; see §7.5 |
| **Add** unique index on `FlightNumber` | **Does not exist today** — rule 4.1's DB-level constraint has no counterpart in the current schema. Filtered (`HasFilter("[Active] = 1")`) per §7.3 |
| **Keep** `FlightNumber` `HasMaxLength(16)` | Already in `FlightInventoryDbContext`. Java has *no* cap — a 17+ char flight number Java accepts would be rejected here. Add a matching FluentValidation `MaximumLength(16)` so it surfaces as a 400, not a DB error |
| **Not persisted** | `LowSeatThreshold` — config only (§5.2); `LowSeatAvailability` — computed in the read projection |

A **new EF Core migration is required**; `20260731125657_InitialCreate` predates all of the above.

Domain behavior stays on the entity, mirroring Java's `Flight.decreaseSeats`/`increaseSeats`/
`deactivate`/`isLowSeatAvailability` — the spec (rule 4.2) specifically flags entity-resident logic
as the thing a service-only reading would miss, so it should not migrate into the service layer.
See §6.3.2 for the one open question about that.

## 4. Service interface (signatures only)

`FlightInventory.Application` currently contains **only** `IFlightRepository` — there is no service
abstraction, no DTOs, and no `ServiceCollectionExtensions` (Identity has one; this project doesn't).
All of it is new here.

```
IFlightService (FlightInventory.Application)
    Task<IReadOnlyList<FlightResponse>> ListAsync(CancellationToken ct);
    Task<FlightResponse>                GetByIdAsync(Guid id, CancellationToken ct);
    Task<FlightResponse>                CreateAsync(FlightRequest request, CancellationToken ct);
    Task<FlightResponse>                AdjustSeatsAsync(Guid id, SeatAdjustmentRequest request, CancellationToken ct);
    Task                                DeleteAsync(Guid id, CancellationToken ct);
```

**`IFlightRepository` needs four additions** — it has `GetByIdAsync` and `AddAsync` only:

```
Task<IReadOnlyList<Flight>> ListActiveAsync(CancellationToken ct = default);
Task<Flight?>               GetActiveByIdAsync(Guid id, CancellationToken ct = default);        // AsNoTracking, for reads
Task<Flight?>               GetActiveByIdForUpdateAsync(Guid id, CancellationToken ct = default); // tracked, for adjust/delete
Task<bool>                  ExistsByFlightNumberAsync(string flightNumber, CancellationToken ct = default);
Task                        SaveChangesAsync(CancellationToken ct = default);
```

Two things drive the tracked/untracked split:

- **The existing `GetByIdAsync` uses `AsNoTracking()`** — correct for reads, **silently fatal** for
  `AdjustSeats` and `Delete`, which mutate and expect the change to persist. Java got this for free
  from JPA dirty checking inside `@Transactional`; spec §6 flags exactly this ("same method, two
  persistence semantics depending on the caller") as the thing EF Core will not reproduce. Making
  it two explicitly-named methods puts that distinction in the type system rather than in a comment.
- **`AdjustSeats` and `Delete` must call `SaveChangesAsync` explicitly.** Java's `FlightService`
  never calls `save()` on either path. This is the single most likely silent-data-loss bug in the
  `04_02` output, and the `04_03` tests should assert persistence, not just the returned DTO.

Reads use `AsNoTracking()` + a projection straight to `FlightResponse` (ADR-aligned, spec §8).

## 5. Cross-cutting gaps this note surfaces (required before `04_02`, not optional)

Contract-level holes the current skeleton has relative to what this slice's approved spec needs:

1. **No `Admin` authorization policy exists anywhere in the solution.**
   `AddBuildingBlocksJwtAuthentication` calls bare `services.AddAuthorization()` with no named
   policies, and **nothing has ever used `BuildingBlocks.Security.Roles.Admin`** — Identity's slice
   had no role-gated route, so Flight is the first slice that needs one.
   The good news: `JwtTokenIssuer` emits `ClaimTypes.Role` specifically so
   `RequireRole`/`[Authorize(Roles=…)]` work against `TokenValidationParameters`' default
   `RoleClaimType`. So this works with **zero changes to the token or the validation wiring** —
   only the policy registration is missing. See §6.3.1 for where it should live.
2. **`FlightInventory:LowSeatThreshold` doesn't exist** in `FlightInventory.Api/appsettings.json`.
   Needs the key plus a `FlightOptions` record bound via `IOptions<FlightOptions>` — the .NET
   replacement for Java's `@Value` field injection (spec §8), and the thing that makes the
   threshold testable without `ReflectionTestUtils`-style hacks.
3. **No unique index on `FlightNumber`** (§3) — rule 4.1's DB-level half is unenforced today.
4. **`IFlightRepository` is read/add-only and `AsNoTracking` throughout** (§4).
5. **`FlightInventory.Api` maps no endpoints and `FlightInventory.Application` has no DI
   extension.** Needs `app.MapFlightEndpoints()` in `Program.cs` and a new
   `AddFlightInventoryApplication()` registering `IFlightService` plus
   `AddValidatorsFromAssemblyContaining<…>`, matching `AddIdentityApplication()`.
6. **`HoldSeatConsumer` and `ReleaseSeatConsumer` are logging stubs** that never touch
   `FlightInventoryDbContext` — they always reply `SeatHeld` and never decrement anything. They are
   FlightInventory's code operating on FlightInventory's data. Scope decision in §6.3.3.

## 6. Design Patterns

### 6.1 Reused from Java
- Layering: endpoints (`FlightInventory.Api`) → service (`FlightInventory.Application`) →
  repository (`FlightInventory.Infrastructure`) — same Controller/Service/Repository shape, per the
  migration plan's Section 2 mapping.
- DTOs as plain request/response records (Java records → C# `record`).
- Error codes and status mapping carry over unchanged (spec §5): `DUPLICATE_FLIGHT_NUMBER` 409,
  `INSUFFICIENT_SEATS` 409, `FLIGHT_NOT_FOUND` 404, `VALIDATION_FAILED` 400 — thrown as
  `ApiException`, rendered by the already-built `GlobalExceptionMiddleware`.
  **Note `INSUFFICIENT_SEATS` is shared with Booking** for a different condition (spec rule 4.4);
  keeping it merged is the parity-preserving choice and is what this note assumes.
- **Soft delete** (rule 4.6) carried over as-is: `Active` flag, active-filtered reads, no delete
  path, no reactivation, non-idempotent `DELETE`.
- **`LowSeatAvailability` stays derived, never persisted** (rule 4.2).
- Behavioral rules locked in the approved spec — public GETs, admin-only mutations, immutable
  route/fare/schedule after creation, zero-allowed/negative-rejected seat adjustment — are **cited,
  not re-decided** here.

### 6.2 Already mandated project-wide (cite ADR 0001, not re-justified)
- `Guid` primary keys; database-per-service (`FlightInventoryDb`); SQL Server.
- Zero-trust JWT validation — already wired in `FlightInventory.Api/Program.cs`.
- Transactional outbox already enabled on `FlightInventoryDbContext`. **This slice publishes no new
  events** — Java's Flight slice has none (spec §1) — so the outbox stays as-is, serving the saga
  replies the consumers already emit.
- FluentValidation via `WithValidation<T>()` + `ValidationFilter<T>`, producing the same
  `VALIDATION_FAILED` envelope. Settled at Identity's `04_01b` gate; standing convention now.
- Minimal APIs in a static `*Endpoints` class, not MVC controllers.
- `AsNoTracking()` + projection on read paths.

---

> [!IMPORTANT]
> **Manual gate (`04_01b`) — needs your sign-off before `04_02` generates any code.**
> Two blocks below need answers: **§6.3** (three per-slice pattern choices) and **§7** (the seven
> decisions the approved spec explicitly deferred to this note). Everything above §6.3 follows
> directly from the approved spec and existing conventions and needs no decision from you.

### 6.3 New for this slice

1. **Where the `Admin` authorization policy lives.** Inline
   `.RequireAuthorization(p => p.RequireRole(Roles.Admin))` on each of the three routes, vs. a
   named `"AdminOnly"` policy registered centrally in
   `BuildingBlocks.Security.AddBuildingBlocksJwtAuthentication`.
   **Recommend the named central policy** — Booking (`/ticket`, `/complete`) and Notification
   (`GET /api/notifications`) need the identical rule per their inventory rows, so building it once
   in the slice that first needs it avoids three divergent copies later.
2. **Who owns the seat-mutation guard.** `AdjustSeatsAsync` (admin) and `HoldSeatConsumer` (saga)
   mutate the same counter and need the same "must not go negative" rule.
   **Recommend a single domain method on `Flight`** (e.g. `AdjustSeats(int delta)` returning a
   result, plus `IsLowSeatAvailability(int threshold)`), called by both — keeping the rule on the
   entity exactly as Java does, which is what spec rule 4.2 flags as easy to lose. The alternative
   is the guard living in `FlightService` and being duplicated in the consumer.
3. **Whether the real `HoldSeat`/`ReleaseSeat` consumers land in this slice's `04_02` or in
   Booking's.** They're FlightInventory's code and data, and the contracts in
   `BuildingBlocks.Contracts` are already frozen — but they're only *exercised* by Booking's saga,
   whose spec isn't written yet.
   **Recommend implementing them here**, in the slice that owns the data, leaving Booking's slice
   to cover the saga end to end. Caveat: `BookingLineItem.FareClass` has **no Java counterpart** —
   `Flight` has a single `Fare` and no fare classes — so the consumer will have to ignore
   `FareClass` until Booking's spec says what it means. If you'd rather not write a consumer
   against a field nobody has specified, deferring both consumers to Booking's pass is the clean
   alternative.

---

## 7. Spec §10 decisions — recommendations (part of the same `04_01b` sign-off)

The approved spec deferred seven decisions to this note. Recommendations below; **items 1–4 and 6
are deliberate deviations from Java behavior**, which means `04_04` (Java/.NET compare) will show
intentional diffs — they should be recorded there as expected, not as regressions.

| # | Spec rule | Recommendation | Why |
|---|---|---|---|
| 1 | 4.3 — `listAll` threshold bug | **Fix** | One `IOptions<FlightOptions>.LowSeatThreshold` applied in the single projection both list and get use. The bug is only *expressible* in Java because the threshold is pushed onto each entity instance; with a shared projection, reproducing it would mean deliberately hardcoding `10` in the list path. Fixing is the lower-effort option here. |
| 2 | 4.5 — unvalidated `Delta` | **Fix** | `int? Delta` + `.NotNull()` → missing body returns 400 `VALIDATION_FAILED` instead of a silent 200 no-op. A no-op that looks like success on an admin inventory mutation is an operational hazard, and every other mutating DTO in the app is validated. |
| 3 | 4.8 — flight numbers burned after soft delete | **Fix** | Filtered unique index `HasFilter("[Active] = 1")` + `ExistsByFlightNumberAsync` filtered on `Active`. Real airlines reuse flight numbers every season; SQL Server supports this natively, so it's a one-line index change. |
| 4 | 4.9 — nullable `fare`, silent `0` capacity | **Fix** | `decimal Fare` + `.GreaterThanOrEqualTo(0.01m)` turns an omitted fare into a 400 *for free* (missing → `0` → fails the rule), replacing Java's unhandled 500. `int? SeatCapacity` + `.NotNull()` closes the silent-zero-seat-flight case. |
| 5 | 4.7 — single counter vs. split capacity/available | **Keep the single counter**, named `SeatCapacity` | Splitting is the right end state, but the second number (seats sold) is only knowable from Booking, whose spec isn't validated yet — designing the split now means designing against an unwritten spec. Keeping the Java name makes `04_04`'s field-by-field compare honest; a doc comment records that its real semantics are *remaining seats*. **Revisit in Booking's design note.** |
| 6 | §5 — lost-update race on seats | **Fix — add `RowVersion`** | This is *worse* in .NET than in Java: admin `AdjustSeats` and the saga's `HoldSeat` run in **different processes**, so there isn't even a shared transaction to accidentally serialize them. `IsRowVersion()` + catching `DbUpdateConcurrencyException` → 409. Needs a new error code (`CONCURRENT_MODIFICATION`) with no Java counterpart — flagging that as a real, if small, contract addition. |
| 7 | §8 — pagination/filtering on list | **Defer** — the one item I recommend *against* doing now | It's a genuine contract change (new query params, a wrapped response shape), and Strangler Fig cutover is materially safer if the .NET list endpoint stays wire-identical to Java's. Better as a follow-up once traffic is shifted. Say the word and it goes in, but it shouldn't ride along silently with the parity work. |

## 8. Dependencies

Spec §6's outbound answer is unchanged: **none**. The inbound side is where the architecture
diverges most sharply from Java, and it's worth stating plainly because it is the biggest single
structural change in the whole migration so far:

> Java's **one synchronous in-process call** — `BookingService` → `FlightService.findOrThrow(id)`,
> then `decreaseSeats`/`increaseSeats` inside Booking's own transaction — becomes an **asynchronous
> saga over RabbitMQ**: `HoldSeat` → `SeatHeld`/`SeatHoldFailed`, with `ReleaseSeat` as the
> compensating command (ADR 0001, "Saga: orchestration, not choreography").

Consequences this slice inherits:

- **Seat reservation is no longer atomic with booking creation.** Java's guarantee came free from a
  single transaction; here it's the saga's job, with compensation instead of rollback.
- **Spec rule 4.6's "a deactivated flight is 404 at booking creation" changes shape** — there's no
  404 to return over a message bus. It becomes `SeatHoldFailed(BookingId, Reason)`. The *behavior*
  (can't book a cancelled flight) is preserved; the *mechanism* isn't. Worth an explicit note in
  Booking's spec when it's written.
- **`ReleaseSeat` still has no trigger in the current saga** (ADR 0001) — `SeatHeld` transitions
  straight to `Confirmed`. Java's cancel-releases-seats behavior therefore has no path yet on the
  .NET side. That gap belongs to Booking's slice, not this one, but it's recorded here since the
  consumer this slice may implement (§6.3.3) is the receiving end of it.

## 9. Not in scope for this slice's `04_02`

- Pagination/filtering/sorting on the list endpoint (§7.7).
- Fare classes and seat maps — `BookingLineItem.FareClass` exists in the frozen contract but has no
  Java counterpart and no spec behind it (§6.3.3).
- A general flight-update endpoint — **Java has none** (spec §2); route, fare, and departure time
  stay immutable after creation. Adding one would be new functionality, not migration.
- Flight reactivation — no Java counterpart (spec rule 4.6).
- Redis caching for flight reads — deferred by ADR 0001 until a real consumer exists.
- The saga's post-hold failure path and `ReleaseSeat` trigger — Booking's slice (§8).

---

*No code has been written yet — this document is the `04_01` design output only. Once you confirm
§6.3 and §7 (`04_01b`, the manual gate above), `04_02` generates the actual
endpoints/service/repository/DTO/validator/entity/migration code from this note plus the approved
spec.*
