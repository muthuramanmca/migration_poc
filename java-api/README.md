# java-api

A small, deliberately realistic Spring Boot REST API used as the source
application for the **Java -> .NET Core migration exercise**, modeling an
airline ticket-booking platform. It is not meant to be a production app --
it exists so the migration process (OpenAPI extraction, slice
grouping/ordering, the Phase 3 rewrite loop, the Phase 3a logic-extraction
toolkit) has real Java code, real business rules, and a real running app to
practice against -- and, for this United Airlines demo specifically, a
realistic domain instead of a generic e-commerce one.

## Modules

| Module        | Package                              | Depends on |
|---------------|---------------------------------------|------------|
| Identity      | `com.example.airlineapi.identity`     | -- (foundational) |
| Flight        | `com.example.airlineapi.flight`       | -- |
| Booking       | `com.example.airlineapi.booking`      | Identity, Flight |
| Loyalty       | `com.example.airlineapi.loyalty`      | Identity (via event), Booking (via event) |
| Notification  | `com.example.airlineapi.notification` | Booking (via event) |

Unlike the earlier 3-slice version, this is a diamond-shaped dependency
graph, not a line: Loyalty and Notification both fan out from Booking's
events, and Loyalty also reacts to Identity's registration event. See the
[dotnet-api counterpart ADR](../dotnet-api/docs/adr/0001-microservices-skeleton.md)
for how these map onto the target microservices.

## Run it

Requires Java 17+ and Maven.

```bash
mvn spring-boot:run
```

The app starts on `http://localhost:8081` with an in-memory H2 database
(no external setup needed; data resets every restart).

## Phase 2a -- extract the API contract

```bash
curl http://localhost:8081/v3/api-docs
```

or open `http://localhost:8081/swagger-ui.html` in a browser. This is the
machine-readable contract the migration plan says to pull before hand-cataloguing
endpoints.

## Run the tests (Phase 3a -- "mine the existing test suite" in practice)

```bash
mvn test
```

`IdentityServiceTest`, `FlightServiceTest`, `BookingServiceTest`,
`LoyaltyServiceTest`, and `NotificationServiceTest` encode the business
rules below as executable assertions -- read these before reading the
service implementations, exactly as Phase 3a recommends.

## Try the business flow end to end

```bash
# 1. Register a passenger (defaults to role PASSENGER)
curl -s -X POST localhost:8081/api/auth/register -H "Content-Type: application/json" \
  -d '{"username":"alice","email":"alice@example.com","password":"password1"}'

# 2. Log in -> get a JWT
TOKEN=$(curl -s -X POST localhost:8081/api/auth/login -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password1"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 3. A LoyaltyAccount was auto-created on registration -- check it
curl -s localhost:8081/api/loyalty/me -H "Authorization: Bearer $TOKEN"

# 4. Browse flights (public)
curl -s localhost:8081/api/flights

# 5. Booking a flight requires it to exist first -- creating one requires ADMIN.
#    Promote alice to ADMIN via the H2 console (see below), log in again for a fresh
#    token, then:
curl -s -X POST localhost:8081/api/flights -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"flightNumber":"UA100","origin":"ORD","destination":"SFO","departureAt":"2026-09-01T14:00:00Z","fare":249.00,"seatCapacity":5}'

# 6. Book a flight (any authenticated passenger)
curl -s -X POST localhost:8081/api/bookings -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"items":[{"flightId":1,"seatCount":2}]}'

# 7. Walk the booking through its state machine
curl -s -X POST localhost:8081/api/bookings/1/pay        -H "Authorization: Bearer $TOKEN"
curl -s -X POST localhost:8081/api/bookings/1/ticket      -H "Authorization: Bearer $TOKEN"   # ADMIN only
curl -s -X POST localhost:8081/api/bookings/1/complete    -H "Authorization: Bearer $TOKEN"   # ADMIN only
# or, from PENDING/PAID instead of the above:
curl -s -X POST localhost:8081/api/bookings/1/cancel      -H "Authorization: Bearer $TOKEN"

# 8. After paying, check that miles were awarded and a notification was logged
curl -s localhost:8081/api/loyalty/me -H "Authorization: Bearer $TOKEN"
curl -s localhost:8081/api/notifications -H "Authorization: Bearer $TOKEN"   # ADMIN only
```

There is no seed/admin-promotion endpoint on purpose (mirrors a realistic
app). To make a test passenger ADMIN, use the H2 console at
`http://localhost:8081/h2-console` (JDBC URL `jdbc:h2:mem:airlinedb`, user
`sa`, empty password):

```sql
UPDATE PASSENGERS SET ROLE = 'ADMIN' WHERE USERNAME = 'alice';
```

Then log in again -- the new JWT will carry the ADMIN role claim.

## Business rules deliberately planted for the Phase 3a exercise

These are the kind of rules the migration plan's Phase 3a toolkit is
designed to surface -- worth deliberately hunting for rather than assuming
a straight read of the Controller/Service layer catches everything:

- **`Flight.isLowSeatAvailability()`** -- logic living in an entity getter,
  not the service layer.
- **`app.flights.low-seat-threshold`** in `application.yml` -- a business
  rule driven by configuration, not a hardcoded constant. `GET /api/flights/{id}`
  uses the configured value; `GET /api/flights` (list) uses a hardcoded
  default because `listAll()` skips `findOrThrow()` -- the same
  inconsistent-computation bug as the original app, still undecided:
  replicate or fix during migration.
- **`SeatAdjustmentRequest.delta`** -- has no validation; a missing body
  field silently becomes `0` (no-op).
- **`Booking.transitionTo(...)`** -- the full booking status state machine
  (PENDING -> PAID -> TICKETED -> FLOWN, with CANCELLED reachable from
  PENDING or PAID only) lives on the entity, not in `BookingService`.
- **Cancelling a booking releases seats whether it was PENDING or PAID** --
  because seats are reserved at booking-creation time, before any payment
  step exists. Easy to assume release only applies to paid bookings; it
  doesn't.
- **`ticket`/`complete` have no per-booking ownership check** -- any admin
  can act on any booking; this is intentional, not a bug.
- **New registrations always get role `PASSENGER`** -- role is never
  client-suppliable at signup (see `IdentityService.register`).
- **Booking ownership check** -- a non-admin can only see/pay/cancel their
  own bookings (`BookingService.assertOwnerOrAdmin`), enforced in the
  service, not just via URL structure.
- **`@ValidPassword`** -- a custom Bean Validation annotation
  (min 8 chars + at least one digit), easy to miss if you only skim DTOs
  for `@NotBlank`/`@Size`.
- **Unmapped `AccessDeniedException`** -- there's no explicit
  `@ExceptionHandler` for it; Spring Security's filter chain maps it to
  403 automatically. A rule that isn't in `GlobalExceptionHandler` at all
  is still a rule the .NET rewrite needs to reproduce.
- **`BookingItem.farePaidAtBooking`** -- fare is captured at booking time
  so later `Flight` fare changes don't retroactively change historical
  booking totals *or* miles already earned from them.
- **Two synchronous events inside open transactions** --
  `BookingCreatedEvent` (on booking creation) and `BookingPaidEvent` (on
  payment) both fire *before* the transaction commits, and
  `PassengerRegisteredEvent` does the same on registration. All three are
  the deliberate "before" state for `dotnet-api`'s real transactional
  outbox -- an improvement opportunity to fire *after* a successful commit
  instead, on all three.
- **`LoyaltyAccount.redeem(...)`** -- has no validation against the current
  balance; redeeming more miles than available silently goes negative
  (mirrors `SeatAdjustmentRequest.delta`'s missing-validation gap).
- **Loyalty tier thresholds** (`app.loyalty.silver-threshold`,
  `app.loyalty.gold-threshold`) -- another config-driven business rule,
  re-evaluated on every `awardMiles` call.
- **Cross-package event listeners, not direct service calls, for
  cross-domain side effects** -- `identity` has no dependency on `loyalty`
  (uses `PassengerRegisteredEvent` instead); `booking` calls `notification`
  and `loyalty` directly from `BookingEventListener`, not from
  `BookingService` itself. Worth noting which domains a slice actually
  talks to isn't always visible from its own Service class.

## Note on this build

This project was generated in a sandbox that could not run a live Maven
build/test cycle to verify compilation end to end. Please run `mvn test`
and `mvn spring-boot:run` locally and report back anything that doesn't
compile or behave as described -- happy to fix it.
