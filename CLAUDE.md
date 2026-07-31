# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A **Java → .NET Core migration PoC** for United Airlines. The source app is a synthetic Spring Boot 3 REST API (`java-api/`) standing in for a real codebase. The target .NET Core solution lives in the sibling `dotnet-api/` folder. The goal is to build a repeatable Claude-assisted migration playbook using a Strangler Fig pattern — one bounded slice at a time, spec-first before any code generation.

**`dotnet-api/` is a microservices architecture skeleton for the real airline ticket-booking domain — not a 1:1 port of `java-api`'s three slices.** Its service boundaries (Identity, FlightInventory, Booking, Notification, Gateway) were deliberately designed independently of `java-api`'s generic `user`/`product`/`order` shape. See `dotnet-api/docs/adr/0001-microservices-skeleton.md` for the full reasoning. Project structure, DI wiring, and cross-cutting infrastructure (saga, outbox, JWKS, YARP routing) are built and verified; business logic per service still lands through the `02`-`04` pipeline below, same as any other slice.

---

## Commands (Java source app)

All commands run from `java-api/`:

```powershell
# Build
mvn clean package

# Run (port 8081)
mvn spring-boot:run

# Run all tests
mvn test

# Run a single test class
mvn test -Dtest=BookingServiceTest

# Run a single test method
mvn test -Dtest=BookingServiceTest#create_rejectsWhenSeatsInsufficient

# Swagger UI (when app is running)
# http://localhost:8081/swagger-ui.html

# OpenAPI JSON contract (Phase 2a source of truth)
# http://localhost:8081/v3/api-docs

# H2 console (in-memory DB, wiped on restart)
# http://localhost:8081/h2-console  (JDBC URL: jdbc:h2:mem:airlinedb)
```

---

## Migration workflow and current state

The migration follows a strict spec-first discipline: **no .NET code is written until the behavior spec for a slice is validated**. The tracker is the source of truth for what's next.

**Current status:** `java-api/` was rewritten around 5 airline domains (see "Java source architecture" below), superseding the earlier generic `user`/`product`/`order` domain. The 3 specs, `migration-tracker.csv`, `api-inventory.csv`, and the OpenAPI contract export that described the old domain have been moved to `migration_support/archive/` as historical reference — **no active tracker exists yet for the new domain**. The next step is re-running `02` (contract extraction + slice grouping/ordering) against the current app before `03` can start.

**The workflow per slice** (step IDs match `Java-to-DotNetCore-Migration-Plan.md` Section 4):
1. Read the relevant Java source from `java-api/src/main/java/com/example/airlineapi/<domain>/`
2. Write the behavior spec to `migration_support/specs/<N>-<slice>-spec.md` (`03_01` — create slice behaviour doc)
3. Set `migration-tracker.csv` status to `Spec Ready for Review` and present it in chat
4. User validates; status becomes `Spec Validated` (or `Rework Needed` if corrections are needed) — this is `03_02`, the manual approval gate
5. Only after all specs are validated does .NET code generation begin (`04_02` — generate code)

---

## Java source architecture

**Package layout** (`com.example.airlineapi`):

- `identity/` — `Passenger` entity, `Role` enum, `IdentityService`, `PassengerRepository`, `AuthController` (register/login), `PassengerController` (/me), `IdentityDtos`, `event/PassengerRegisteredEvent`
- `flight/` — `Flight` entity, `FlightService`, `FlightRepository`, `FlightController`, `FlightDtos`
- `booking/` — `Booking` entity, `BookingItem`, `BookingStatus` enum, `BookingService`, `BookingRepository`, `BookingController`, `BookingDtos`, `event/BookingCreatedEvent`, `event/BookingPaidEvent`, `event/BookingEventListener`
- `notification/` — `NotificationLog` entity, `NotificationService`, `NotificationLogRepository`, `NotificationController`, `dto/NotificationDtos`
- `loyalty/` — `LoyaltyAccount` entity, `LoyaltyTier` enum, `LoyaltyService`, `LoyaltyAccountRepository`, `LoyaltyController`, `dto/LoyaltyDtos`, `event/LoyaltyEventListener`
- `security/` — `JwtService` (JJWT 0.12.x, HS256, 1-hour expiry), `JwtAuthFilter` (stateless bearer-token filter)
- `config/` — `SecurityConfig` (public routes: register, login, GET /api/flights/**), `OpenApiConfig`
- `common/` — `ApiError`/`ApiException` (the error envelope), `GlobalExceptionHandler` (`@ControllerAdvice`), `PasswordValidator`/`ValidPassword` (custom Bean Validation)

**Cross-cutting patterns:**
- All API errors go through `GlobalExceptionHandler` → returns `ApiError { code, message, details }` — **except** Spring Security's own 401/403 rejections, which bypass it entirely and produce no body
- DTOs are all Java records defined as static inner classes inside `*Dtos.java` files (e.g., `IdentityDtos.RegisterRequest`)
- No Lombok — all boilerplate is explicit

**Cross-slice dependencies** (a diamond, not a line — see `java-api/README.md`'s Modules table):
- `BookingService` calls `FlightService.findOrThrow(Long id)` directly (the one *service-to-service* call in the app).
- Everything else is **event-mediated, not a direct service call**: `IdentityService` fires `PassengerRegisteredEvent` (consumed by `loyalty.event.LoyaltyEventListener`, so `identity` has zero dependency on `loyalty`); `booking.event.BookingEventListener` (not `BookingService` itself) calls into both `NotificationService` and `LoyaltyService` when `BookingCreatedEvent`/`BookingPaidEvent` fire. All three events fire **synchronously inside the open transaction, before commit** — deliberately, the motivating "before" state for `dotnet-api`'s real transactional outbox.

---

## Key behavioral findings from the specs

*(Findings below describe the original `user`/`product`/`order` domain, archived in `migration_support/archive/`. New specs for the current 5-domain app haven't been written yet — this section will be replaced once they are. The equivalent rules all carry over renamed; see `java-api/README.md`'s "Business rules deliberately planted" section for the current, authoritative list.)*

### Auth/Users (→ now Identity)
- Username uniqueness is checked **before** email — if both are duplicate, only `DUPLICATE_USERNAME` is returned
- Login errors are **intentionally identical** for "no such user" vs "wrong password" — do not make them more specific
- `GET /me` roles come from the JWT claim, **not a DB re-read** — role changes don't take effect until token expiry
- For a real user-table migration: existing BCrypt hashes are incompatible with ASP.NET Core Identity's default PBKDF2 hasher

### Products (→ now Flight)
- The low-stock/low-seat computation is inconsistent: the single-get endpoint uses the configured threshold; the list endpoint uses a hardcoded default because `listAll()` skips `findOrThrow()`. Both currently agree, but would diverge if the config changed. **Decision needed:** replicate the bug or fix it.
- The stock/seat adjustment DTO's `delta` field has **no validation** — a missing body field silently becomes `0` (no-op), unlike every other mutating DTO in the app

### Orders (→ now Booking)
- Booking creation reserves seats per line item **in a single transaction** with no intermediate `save()` calls — EF Core rewrite must call `SaveChangesAsync()` once at the end, not per item
- Booking events fire **synchronously inside the open transaction** (before commit) — an improvement opportunity: fire them after a successful `SaveChangesAsync()` instead
- `ticket` and `complete` have **no per-booking ownership check** — any admin can act on any booking; this is intentional, not a bug
- `cancel` always releases seats, whether the booking was `PENDING` or `PAID` (seats were reserved at creation, before payment)
- Cancellation is blocked once a booking is `TICKETED` or `FLOWN` — state transitions live on the `Booking` entity's `transitionTo()` method, not in the service

**State machine:** `PENDING → PAID → TICKETED → FLOWN`; `PENDING` or `PAID` may go to `CANCELLED`; `TICKETED`/`FLOWN`/`CANCELLED` are terminal.

---

## Migration plan reference

`Java-to-DotNetCore-Migration-Plan.md` is the master document. Key sections:
- **Section 2** — Java → .NET concept mapping table (Spring annotations → ASP.NET Core equivalents)
- **Section 4** — The 6-phase plan with Mermaid diagrams (currently in Phase 3)
- **Section 3a** — The spec-writing toolkit (decision tables, Gherkin, state diagrams, mining test suites)
- **Section 8** — Concrete .NET architectural choices: Nullable reference types, EF Core query discipline (`AsNoTracking`), `System.Text.Json`, Polly for resilience, OpenTelemetry

`dotnet-api/` already exists as a microservices skeleton (Identity, FlightInventory, Booking, Notification, Gateway + shared BuildingBlocks libraries) — see `dotnet-api/docs/adr/0001-microservices-skeleton.md` for the actual structure and every decision behind it. It supersedes the single-solution layered layout Section 4 originally sketched.

---

## Files to read when working on a slice

When generating specs or .NET code for a slice, always read these together (not one at a time):

**Identity:** `identity/AuthController.java`, `identity/PassengerController.java`, `identity/IdentityService.java`, `identity/Passenger.java`, `identity/Role.java`, `identity/dto/IdentityDtos.java`, `identity/event/PassengerRegisteredEvent.java`, `common/PasswordValidator.java`, `common/ValidPassword.java`, `security/JwtService.java`, `security/JwtAuthFilter.java`, `config/SecurityConfig.java`

**Flight:** `flight/FlightController.java`, `flight/FlightService.java`, `flight/Flight.java`, `flight/FlightRepository.java`, `flight/dto/FlightDtos.java`

**Booking:** `booking/BookingController.java`, `booking/BookingService.java`, `booking/Booking.java`, `booking/BookingItem.java`, `booking/BookingStatus.java`, `booking/dto/BookingDtos.java`, `booking/event/BookingCreatedEvent.java`, `booking/event/BookingPaidEvent.java`, `booking/event/BookingEventListener.java` — plus `flight/FlightService.java` for the cross-slice call, and `notification/NotificationService.java` + `loyalty/LoyaltyService.java` since `BookingEventListener` calls both directly

**Notification:** `notification/NotificationController.java`, `notification/NotificationService.java`, `notification/NotificationLog.java`, `notification/NotificationLogRepository.java`, `notification/dto/NotificationDtos.java`

**Loyalty:** `loyalty/LoyaltyController.java`, `loyalty/LoyaltyService.java`, `loyalty/LoyaltyAccount.java`, `loyalty/LoyaltyTier.java`, `loyalty/LoyaltyAccountRepository.java`, `loyalty/dto/LoyaltyDtos.java`, `loyalty/event/LoyaltyEventListener.java` — plus `identity/event/PassengerRegisteredEvent.java` for the cross-slice event
