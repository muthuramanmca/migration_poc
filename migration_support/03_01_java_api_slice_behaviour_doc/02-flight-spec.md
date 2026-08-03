# Behavior Spec — Slice 2: Flight

**Migration order:** 2 of 5 (no outbound dependencies — can be migrated in parallel with Identity)
**Java source:** `FlightController`, `FlightService`, `Flight`, `FlightRepository`, `FlightDtos`, `SecurityConfig` (flight-related rules only), `application.yml` (`app.flights.low-seat-threshold`)
**Status:** Spec Ready for Review

---

## 1. Purpose

Owns the flight schedule: creating flights, browsing them, adjusting seat
counts, and cancelling (soft-deleting) them. It is the app's seat-inventory
ledger — `seatCapacity` is not a static aircraft configuration but a live
*remaining seats* counter that Booking decrements and increments (see rule
4.7 and Section 6).

Unlike Identity, this slice publishes **no domain events** and has **no
outbound dependency** on any other slice. It is, however, the one slice that
exposes a public service method consumed directly by another slice
(`FlightService.findOrThrow`) — the single service-to-service call in the
entire app.

There is no entity state machine here (unlike Booking). A `Flight` has
exactly one lifecycle transition: `active = true` → `active = false`, one
way, via soft delete.

## 2. Endpoints

| Method | Path | Operation | Auth | Request | Response | Success |
|---|---|---|---|---|---|---|
| GET | `/api/flights` | `list_1` | **Public** | — | `FlightResponse[]` | 200 |
| GET | `/api/flights/{id}` | `get` | **Public** | — | `FlightResponse` | 200 |
| POST | `/api/flights` | `create` | `ADMIN` only | `FlightRequest` | `FlightResponse` | **201** |
| PUT | `/api/flights/{id}/seats` | `adjustSeats` | `ADMIN` only | `SeatAdjustmentRequest` | `FlightResponse` | 200 |
| DELETE | `/api/flights/{id}` | `delete` | `ADMIN` only | — | — | **204** |

Both GET routes are the only unauthenticated endpoints in the app outside of
`/api/auth/**` — anyone can browse the schedule with no token at all.

**Notable: there is no general update endpoint.** `origin`, `destination`,
`fare`, `departureAt`, and `flightNumber` are **immutable after creation** —
the entity exposes no setters for them and no `PUT /api/flights/{id}` exists.
The only mutable field is `seatCapacity`, and only through the dedicated
`/seats` sub-resource. A fare change or a re-timed departure is not
expressible through this API.

**Contract drift to be aware of:** `java-api-contract.txt` (the springdoc
export) documents `create` as `200` and `delete` as `200`, because springdoc
can't infer the status from `ResponseEntity.status(201)` /
`ResponseEntity.noContent()`. **The code is authoritative: 201 and 204.** A
.NET port generated from the contract file rather than from the source would
get both of these wrong.

## 3. DTOs (field-level)

- **`FlightRequest`**: `flightNumber` (string, `@NotBlank`), `origin`
  (string, `@NotBlank`), `destination` (string, `@NotBlank`), `departureAt`
  (`Instant`, `@NotNull`), `fare` (`BigDecimal`, `@DecimalMin("0.01")` —
  **but not `@NotNull`**, see rule 4.9), `seatCapacity` (primitive `int`,
  `@Min(0)` — a primitive, so it can never be null; an omitted field silently
  becomes `0`, see rule 4.9).
  No length caps, no format/IATA validation on the airport codes, and no
  `@Future` on `departureAt` — see rule 4.10.
- **`SeatAdjustmentRequest`**: `delta` (primitive `int`) — **zero validation
  annotations, and the controller doesn't even apply `@Valid`** (see rule 4.5).
  A negative `delta` removes seats, positive adds.
- **`FlightResponse`**: `id`, `flightNumber`, `origin`, `destination`,
  `departureAt`, `fare`, `seatCapacity`, `lowSeatAvailability` (boolean —
  computed, never persisted; see rule 4.2). Note `active` is **never
  returned** — clients cannot tell an active flight from a deactivated one,
  because deactivated flights are simply invisible (rule 4.6).

## 4. Business rules

**4.1 — Flight numbers are globally unique, checked case-sensitively at the application layer *and* by a DB constraint.**

| Given | When | Then |
|---|---|---|
| A flight `UA100` exists | POST /api/flights with `flightNumber: "UA100"` | 409, code `DUPLICATE_FLIGHT_NUMBER`, message "A flight with this number already exists" |
| A flight `UA100` exists | POST with `flightNumber: "ua100"` | **Succeeds** — the check is `existsByFlightNumber`, a case-sensitive equality, and the DB unique constraint is likewise case-sensitive on H2's default collation |

**4.2 — `lowSeatAvailability` is computed on the entity, not stored, using a config-driven threshold: `seatCapacity < threshold`.**

The rule lives in `Flight.isLowSeatAvailability()` — a **getter on the
entity, not in the service**. Deliberately planted: a migration that reads
only `*Service` classes will miss it entirely. The threshold comes from
`app.flights.low-seat-threshold` in `application.yml` (currently `10`),
injected into `FlightService` via `@Value` and then pushed onto each entity
instance via `setLowSeatThreshold(...)`.

Boundary: the comparison is strictly `<`. `seatCapacity == 10` with
threshold `10` is **not** low; `9` is.

**4.3 — ⚠ The threshold is applied inconsistently: `listAll()` uses a hardcoded `10`, every other path uses the configured value.**

This is the single most important finding in this slice.

| Path | How the entity gets its threshold | Effective value |
|---|---|---|
| `GET /api/flights/{id}` | via `findOrThrow` → `setLowSeatThreshold(lowSeatThreshold)` | **config** (`app.flights.low-seat-threshold`) |
| `POST /api/flights` | `create` explicitly calls `setLowSeatThreshold(lowSeatThreshold)` | **config** |
| `PUT /api/flights/{id}/seats` | via `findOrThrow` | **config** |
| `GET /api/flights` (list) | **never calls `findOrThrow`** — maps repository results straight to `toResponse` | **hardcoded `10`** (the `@Transient` field's initializer on `Flight`) |

Because the configured value is *currently also* `10`, the two agree today
and no test catches the divergence. Change `app.flights.low-seat-threshold`
to `20` and a flight with 15 seats reports `lowSeatAvailability: true` from
`GET /api/flights/{id}` but `false` from `GET /api/flights` — **the same
flight, two different answers in the same response cycle.**

**Decision required before `04_01`:** replicate the inconsistency faithfully,
or fix it (apply the configured threshold uniformly). Recommendation: fix it,
and record the fix as a deliberate deviation in the design note — but this is
your call, not mine to make silently.

**4.4 — Seat adjustment is a signed delta, rejected only if it would drive capacity below zero.**

| Given | When | Then |
|---|---|---|
| Flight has 5 seats | PUT `/seats` with `delta: 10` | 200, `seatCapacity` = 15 |
| Flight has 5 seats | PUT `/seats` with `delta: -3` | 200, `seatCapacity` = 2 |
| Flight has 5 seats | PUT `/seats` with `delta: -5` | 200, `seatCapacity` = **0** — zero is allowed, only negative is rejected |
| Flight has 5 seats | PUT `/seats` with `delta: -10` | 409, code `INSUFFICIENT_SEATS`, message names the **flight number**, not the id |
| Flight has 5 seats | PUT `/seats` with `delta: 0` | 200, no change (falls into the `>= 0` branch, `increaseSeats(0)`) |

The guard is evaluated **before** any mutation (`resultingSeats = capacity +
delta`), so a rejected adjustment leaves the entity untouched — there is no
partial application to roll back.

⚠ **The error code `INSUFFICIENT_SEATS` is shared with `BookingService`**,
where it means something different ("this booking asked for more seats than
the flight has"). Two distinct failure conditions, one code. Worth deciding
whether to keep them merged or split them in .NET.

**4.5 — ⚠ `SeatAdjustmentRequest` is completely unvalidated — a missing `delta` is a silent 200 no-op.**

Two independent reasons, either of which alone would be enough:
1. `delta` is a **primitive `int` with no constraint annotations** — Jackson
   defaults an absent or `null` JSON field to `0`.
2. `FlightController.adjustSeats` does **not** annotate the body with
   `@Valid` — unlike `create`, which does. So even if annotations were added
   to the DTO, they would never fire.

Consequence: `PUT /api/flights/1/seats` with body `{}` returns **200 with the
flight unchanged**, not a 400. Every other mutating DTO in the app is
validated; this one is the exception. Preserve or fix — but decide
deliberately, because .NET's model binding will *not* reproduce this by
accident (a missing `int` property in System.Text.Json also defaults to `0`,
but adding `[Required]` or making it `int?` would change the behavior).

**4.6 — Delete is a soft delete: the row is never removed, only flagged `active = false`.**

`Flight.deactivate()` sets the flag; no `repository.delete(...)` call exists
anywhere in the slice. Rationale is in the entity's own Javadoc: past
`BookingItem.farePaidAtBooking` snapshots must stay intact for passengers who
already booked or flew.

Every read path filters on `active`:

| Query | Filters deactivated flights? |
|---|---|
| `findAllByActiveTrue()` (list) | Yes |
| `findByIdAndActiveTrue(id)` (get / adjust / delete / **Booking's `findOrThrow`**) | Yes |
| `existsByFlightNumber(number)` (uniqueness check) | **No — see rule 4.8** |

Consequences:
- A deactivated flight is a **404 everywhere**, including from
  `BookingService.create` — so no new booking can be made against a cancelled
  schedule, and `BookingService.cancel` can no longer return seats to it
  either (it calls the same `findOrThrow`, which now throws).
- `DELETE` is **not idempotent**: the first call returns 204, a second call
  on the same id returns 404 `FLIGHT_NOT_FOUND` (asserted by
  `delete_throwsNotFoundWhenAlreadyDeactivated`).
- There is **no reactivation path** — deactivation is terminal via the API.

**4.7 — `seatCapacity` is a live remaining-seats counter, not a static aircraft configuration.**

Booking calls `flight.decreaseSeats(n)` at booking creation and
`flight.increaseSeats(n)` on cancellation, mutating this exact field. So
"capacity" in the field name means *seats still available*, and an admin
`PUT /seats` with `delta: +5` is indistinguishable from five cancellations.

There is **no separate seats-sold or booked count**, and no reconciliation
between adjustments and existing bookings — an admin can reduce capacity to
`0` while bookings for that flight are outstanding, and nothing detects it.

This matters for `dotnet-api`: the `FlightInventory` service should decide
explicitly whether to keep this single-counter model or split
capacity/available, and if it splits them, the Booking saga contract changes
with it.

**4.8 — Flight numbers are burned permanently: the uniqueness check does not exclude deactivated flights.**

`existsByFlightNumber` has no `AndActiveTrue` suffix, so it matches
soft-deleted rows too. Once `UA100` is created and then deleted, `UA100` can
**never be created again** — the API returns 409 `DUPLICATE_FLIGHT_NUMBER`
for a flight the caller cannot see through any endpoint. For a real airline
this is wrong (flight numbers are reused every season), and it is the
emergent consequence of combining soft delete (4.6) with a global uniqueness
check. No test covers it.

**Decision required:** scope uniqueness to active flights, add a
`(flightNumber, departureDate)` composite key, or accept the current
behavior.

**4.9 — ⚠ `fare` is nullable through validation and will fail at the database, not at the API boundary.**

`@DecimalMin` — like every Bean Validation constraint — **treats `null` as
valid**. There is no `@NotNull` on `fare`. The OpenAPI export confirms it:
`FlightRequest.required` is `["departureAt","destination","flightNumber","origin"]`
— `fare` and `seatCapacity` are absent.

| Given | When | Then |
|---|---|---|
| Body omits `fare` entirely | POST /api/flights | Passes `@Valid` → `Flight.fare = null` → `@Column(nullable = false)` → constraint violation on flush → **unhandled 500**, and the response body is *not* the `ApiError` envelope (`DataIntegrityViolationException` has no handler in `GlobalExceptionHandler`) |
| Body omits `seatCapacity` | POST /api/flights | **201** — primitive `int` defaults to `0`, which satisfies `@Min(0)`. A zero-seat flight is created and immediately reports `lowSeatAvailability: true` |
| Body sends `fare: 0` | POST /api/flights | 400 `VALIDATION_FAILED`, "Fare must be greater than zero" |

The `fare` case is a genuine defect (500 where 400 belongs). The
`seatCapacity` case is arguably intentional but silent. Both should be closed
in .NET with explicit required-ness.

**4.10 — Schedule fields are structurally unvalidated beyond non-blankness.**

None of the following are checked anywhere:
- `origin` and `destination` may be **identical** (`ORD` → `ORD` is accepted).
- Neither is validated as an IATA code, or against any airport list, or for
  length — `"x"` and a 500-character string both pass `@NotBlank`.
- `flightNumber` has no format rule and no length cap.
- `departureAt` may be **in the past** — no `@Future`. A flight departing in
  1998 can be created today and is bookable.

## 5. Error handling

| Condition | Status | Code | Where enforced |
|---|---|---|---|
| Duplicate flight number (incl. matching a soft-deleted flight) | 409 | `DUPLICATE_FLIGHT_NUMBER` | `FlightService.create` |
| Seat adjustment would go negative | 409 | `INSUFFICIENT_SEATS` | `FlightService.adjustSeats` |
| Flight id unknown **or** deactivated | 404 | `FLIGHT_NOT_FOUND` | `FlightService.findOrThrow` (message includes the id) |
| `@Valid` failure on `FlightRequest` (blank strings, null `departureAt`, `fare < 0.01`, negative `seatCapacity`) | 400 | `VALIDATION_FAILED` | `GlobalExceptionHandler`, field-level messages included |
| Anonymous or non-admin caller on POST/PUT/DELETE | 401 / 403 | *(no error body)* | **Spring Security**, bypasses `GlobalExceptionHandler` entirely — 401 without a token, 403 with a valid non-`ADMIN` token |
| Null `fare` in the request body | **500** | *(none — not an `ApiError`)* | Unhandled DB constraint violation — see rule 4.9 |
| Malformed / absent JSON body on `PUT /seats` | 400 | *(none — Spring's default error body, not `ApiError`)* | `HttpMessageNotReadableException`, also unhandled by `GlobalExceptionHandler` |

**Edge cases not currently handled:**
- **Uniqueness race** — same shape as Identity's: `existsByFlightNumber` and
  `save()` are not atomic, so two concurrent creates of `UA100` can both pass
  the check and the loser hits the DB unique constraint as an unhandled 500
  instead of a clean 409.
- **Lost-update race on seats** — `adjustSeats` and `BookingService.create`
  both read-modify-write `seatCapacity` with **no optimistic locking**
  (`Flight` has no `@Version` field) and no `SELECT … FOR UPDATE`. Concurrent
  booking + adjustment can silently lose one of the two updates, or drive
  capacity negative despite rule 4.4's guard. This is the strongest argument
  in the slice for concurrency tokens on the .NET side (`IsRowVersion` /
  `[Timestamp]`).

## 6. Dependencies

- **Outbound:** none. No events published, no other slice's service called.
  Migratable independently of, and in parallel with, Identity.
- **Inbound:**
  - `BookingService` calls `FlightService.findOrThrow(Long id)` directly —
    the **only service-to-service call in the app** — at booking creation
    (then `decreaseSeats`) and at cancellation (then `increaseSeats`). Booking
    mutates the `Flight` entity in place and relies on **JPA dirty checking
    inside Booking's own `@Transactional`** to persist the change; there is no
    `flightRepository.save(...)` on that path.
  - `SecurityConfig` depends on this slice's URL shape for its public-GET rule.

**Transactional subtlety worth carrying into .NET:** `findOrThrow` returns a
**managed** entity when called from inside Booking's transaction (mutations
persist automatically) but a **detached** one when called from
`FlightService.getById`, which is not `@Transactional` and runs with
`open-in-view: false`. Same method, two persistence semantics depending on
the caller. EF Core has no equivalent ambient behavior — the .NET rewrite
must make the write path explicit (a shared `DbContext` and an explicit
`SaveChangesAsync`, or an explicit repository/UoW call), because
`adjustSeats` and `delete` likewise **never call `save()`** and depend
entirely on dirty checking.

## 7. Tests already covering this behavior (mined from `FlightServiceTest`)

Six tests exist, asserting rules 4.1, 4.2, 4.4, and 4.6:
`create_rejectsDuplicateFlightNumber`,
`create_flagsLowSeatAvailabilityWhenBelowThreshold`,
`create_doesNotFlagLowSeatAvailabilityAboveThreshold`,
`adjustSeats_rejectsNegativeResultingCapacity`,
`delete_deactivatesFlightInsteadOfRemovingIt`,
`delete_throwsNotFoundWhenAlreadyDeactivated`,
`findOrThrow_excludesDeactivatedFlights`.

Note the tests inject the threshold via
`ReflectionTestUtils.setField(flightService, "lowSeatThreshold", 10)` —
**hardcoding the same value as the entity's default**, which is precisely why
rule 4.3's inconsistency is invisible to the existing suite.

**Not covered by any existing test** — all worth adding on the .NET side:
- The `listAll` vs. `getById` threshold divergence (4.3)
- Flight-number reuse after soft delete (4.8)
- Null `fare` → 500 (4.9)
- The unvalidated / absent `delta` no-op (4.5)
- `delta: 0` and the exact `resultingSeats == 0` boundary (4.4)
- Authorization: that GETs are public and mutations are `ADMIN`-only
- Any concurrency behavior at all

## 8. Non-functional notes

- **`GET /api/flights` returns the entire active flight table** — no
  pagination, no filtering, no sorting, no `ORDER BY` (result order is
  whatever the DB returns). For an airline schedule this is the endpoint most
  likely to be a production problem, and it is also the endpoint with **no
  route-by-origin/destination/date search at all** despite being the natural
  flight-search surface. Consider paging and query filters in .NET, and treat
  that as an explicit contract change rather than a silent one.
- **`lowSeatAvailability` is exposed to anonymous callers.** Low-inventory
  signalling is commercially sensitive (it is a scarcity/pricing hint) and
  requires no token. Flagged rather than assumed to be a bug.
- **`FlightRepository.findByFlightNumber` is declared but never called
  anywhere** in main or test code — dead surface area; don't port it without
  a reason.
- **N+1 and tracking:** `listAll` maps entities to DTOs one by one; in EF Core
  the equivalent must use `AsNoTracking()` plus a projection (`Select` to the
  response type) rather than materializing tracked entities — per Section 8 of
  the migration plan.
- **`Instant` → .NET:** `departureAt` is a UTC instant. Map to
  `DateTimeOffset` (or `DateTime` with `DateTimeKind.Utc`), and keep the JSON
  wire format ISO-8601 UTC so existing clients are unaffected.
- **`BigDecimal fare` → `decimal`**, with an explicit precision/scale
  (e.g. `decimal(18,2)`) on the EF Core property. Leaving it to convention
  invites silent rounding differences from the Java side.
- **`@Value` field injection into a mutable non-final field** is the Spring
  idiom used here; the .NET equivalent is `IOptions<FlightOptions>` bound to a
  config section — which also makes the threshold testable without reflection,
  removing the exact blind spot described in Section 7.

## 9. Source references

Jump-to references for the manual approval gate (`03_02`) and the .NET code
review (`04_05`). Paths are relative to
`java-api/src/main/java/com/example/airlineapi/` unless noted otherwise.

**Endpoints (§2)**

| Endpoint | File | Lines |
|---|---|---|
| Base path `/api/flights` | `flight/FlightController.java` | 20 |
| `GET /api/flights` | `flight/FlightController.java` | 29–32 |
| `GET /api/flights/{id}` | `flight/FlightController.java` | 34–37 |
| `POST /api/flights` (201 via `ResponseEntity.status`) | `flight/FlightController.java` | 39–42 (esp. 41) |
| `PUT /api/flights/{id}/seats` | `flight/FlightController.java` | 44–47 |
| `DELETE /api/flights/{id}` (204 via `noContent()`) | `flight/FlightController.java` | 49–53 (esp. 52) |
| Contract drift (both documented as 200) | `migration_support/java-api-contract.txt` | 1 (`operationId: create`, `operationId: delete`) |

**DTOs (§3)** — all in `flight/dto/FlightDtos.java`

| DTO / field | Lines |
|---|---|
| `FlightRequest` | 13–20 |
| `FlightRequest.fare` (`@DecimalMin`, no `@NotNull`) | 18 |
| `FlightRequest.seatCapacity` (primitive `int`, `@Min(0)`) | 19 |
| `SeatAdjustmentRequest` (no annotations at all) | 22 |
| `FlightResponse` | 24–33 |

**Business rules (§4)**

| Rule | File | Lines |
|---|---|---|
| 4.1 — Duplicate check + 409 | `flight/FlightService.java` | 29–31 |
| 4.1 — `existsByFlightNumber` (no active filter) | `flight/FlightRepository.java` | 9 |
| 4.1 — DB unique constraint on `flightNumber` | `flight/Flight.java` | 16 |
| 4.2 — Computation on the entity getter | `flight/Flight.java` | 68–70 |
| 4.2 — `@Transient`, not persisted | `flight/Flight.java` | 46–47 |
| 4.2 — Config injection | `flight/FlightService.java` | 20–21 |
| 4.2 — Config value (`10`) | `java-api/src/main/resources/application.yml` | 33–34 |
| 4.3 — **`listAll` skips `findOrThrow`** | `flight/FlightService.java` | 45–47 (esp. 46) |
| 4.3 — Hardcoded fallback `= 10` | `flight/Flight.java` | 47 |
| 4.3 — Threshold applied on create | `flight/FlightService.java` | 36 |
| 4.3 — Threshold applied in `findOrThrow` | `flight/FlightService.java` | 79 |
| 4.3 — `lowSeatAvailability` read into the response | `flight/FlightService.java` | 86 |
| 4.4 — Guard before mutation | `flight/FlightService.java` | 52–56 |
| 4.4 — Signed-delta branch | `flight/FlightService.java` | 57–61 |
| 4.4 — `increaseSeats` / `decreaseSeats` | `flight/Flight.java` | 76–82 |
| 4.4 — `INSUFFICIENT_SEATS` reused by Booking | `booking/BookingService.java` | 41–44 |
| 4.5 — No `@Valid` on the body | `flight/FlightController.java` | 45 |
| 4.5 — `@Valid` present on `create` (the contrast) | `flight/FlightController.java` | 40 |
| 4.5 — Unannotated primitive `delta` | `flight/dto/FlightDtos.java` | 22 |
| 4.6 — `deactivate()` + rationale | `flight/Flight.java` | 84–90 |
| 4.6 — `delete` never removes the row | `flight/FlightService.java` | 65–69 |
| 4.6 — Active-filtered queries | `flight/FlightRepository.java` | 11–12 |
| 4.6 — `active` default `true`, not in the response | `flight/Flight.java` | 41–42 |
| 4.7 — Booking decrements seats | `booking/BookingService.java` | 38, 46 |
| 4.7 — Booking restores seats on cancel | `booking/BookingService.java` | 102–105 |
| 4.8 — Uniqueness ignores `active` | `flight/FlightRepository.java` | 9 vs. 11–12 |
| 4.9 — `fare` has no `@NotNull` | `flight/dto/FlightDtos.java` | 18 |
| 4.9 — `fare` is `NOT NULL` in the schema | `flight/Flight.java` | 35–36 |
| 4.9 — `seatCapacity` primitive default `0` | `flight/dto/FlightDtos.java` | 19 |
| 4.10 — `@NotBlank` only, no format/length/`@Future` | `flight/dto/FlightDtos.java` | 14–17 |

**Error handling (§5)**

| Condition | File | Lines |
|---|---|---|
| `DUPLICATE_FLIGHT_NUMBER` | `flight/FlightService.java` | 30 |
| `INSUFFICIENT_SEATS` | `flight/FlightService.java` | 54–55 |
| `FLIGHT_NOT_FOUND` | `flight/FlightService.java` | 77–78 |
| `VALIDATION_FAILED` (field-level messages) | `common/GlobalExceptionHandler.java` | 19–26 |
| `ApiException` → response mapping | `common/GlobalExceptionHandler.java` | 13–17 |
| Only two handlers exist (no DB/parse handler) | `common/GlobalExceptionHandler.java` | 13, 19 |
| 401/403 with no body — public GET rule | `config/SecurityConfig.java` | 36 |
| 401/403 with no body — `ADMIN` mutation rules | `config/SecurityConfig.java` | 39–41 |
| **Uniqueness race** — check vs. save not atomic | `flight/FlightService.java` | 29 (check) vs. 37 (save) |
| **Lost-update race** — no `@Version` on the entity | `flight/Flight.java` | 17–47 (absence of a version field) |

**Dependencies (§6)**

| Item | File | Lines |
|---|---|---|
| `findOrThrow` — public for cross-slice use, with rationale | `flight/FlightService.java` | 71–81 |
| Booking's call at creation | `booking/BookingService.java` | 38 |
| Booking's call at cancellation | `booking/BookingService.java` | 103 |
| No `save()` on the adjust path (dirty checking) | `flight/FlightService.java` | 49–63 |
| No `save()` on the delete path (dirty checking) | `flight/FlightService.java` | 65–69 |
| `open-in-view: false` | `java-api/src/main/resources/application.yml` | 13 |

**Non-functional notes (§8)**

| Note | File | Lines |
|---|---|---|
| Unpaginated, unsorted, unfiltered list | `flight/FlightService.java` | 46 |
| Dead repository method `findByFlightNumber` | `flight/FlightRepository.java` | 10 |
| Per-entity DTO mapping (EF Core projection target) | `flight/FlightService.java` | 83–88 |
| `Instant departureAt` | `flight/Flight.java` | 32–33 |
| `BigDecimal fare` | `flight/Flight.java` | 35–36 |
| `@Value` field injection (→ `IOptions<T>`) | `flight/FlightService.java` | 18–21 |
| Threshold set by reflection in tests | `flight/FlightServiceTest.java` (under `src/test/java/…`) | 29 |

---

## 10. Open decisions for `04_01`

Carried forward so the design note has to answer them explicitly:

1. **Rule 4.3** — replicate the `listAll` threshold bug, or fix it?
2. **Rule 4.5** — keep `delta` unvalidated (missing body = 200 no-op), or require it?
3. **Rule 4.8** — allow flight-number reuse after soft delete, or keep numbers burned?
4. **Rule 4.9** — make `fare` and `seatCapacity` explicitly required (400 instead of 500 / silent `0`)?
5. **Rule 4.7** — keep the single `seatCapacity` counter, or split capacity vs. available seats in `FlightInventory`?
6. **Section 5** — add a concurrency token (`[Timestamp]`) to close the lost-update race?
7. **Section 8** — add pagination/filtering to the list endpoint as a deliberate contract change?

---

*No code has been written yet — this document is the `03_01` "Understand"
output only. Awaiting manual validation (`03_02`) before moving to `04_01`
(.NET slice design note) for this slice.*
