# Behavior Spec — Slice 1: Identity

**Migration order:** 1 of 5 (foundational — no outbound dependencies)
**Java source:** `AuthController`, `PassengerController`, `IdentityService`, `Passenger`, `Role`, `PassengerRepository`, `IdentityDtos`, `event/PassengerRegisteredEvent`, `ValidPassword`/`PasswordValidator`, `JwtService`, `JwtAuthFilter`, `SecurityConfig` (auth-related rules only)
**Status:** Spec Ready for Review

---

## 1. Purpose

Registers new passengers, authenticates existing ones, and issues/validates
the JSON Web Tokens used for every other protected endpoint in the app. No
entity lifecycle here (unlike Booking) — a `Passenger` is created once and
never transitions through states. Identity also fires a domain event on
registration that Loyalty consumes to auto-provision a `LoyaltyAccount`, even
though Identity itself has no code-level dependency on the `loyalty` package.

## 2. Endpoints

| Method | Path | Operation | Auth | Request | Response | Success |
|---|---|---|---|---|---|---|
| POST | `/api/auth/register` | `register` | Public | `RegisterRequest` | `PassengerResponse` | 201 |
| POST | `/api/auth/login` | `login` | Public | `LoginRequest` | `AuthResponse` | 200 |
| GET | `/api/passengers/me` | `me` | Any authenticated passenger | — | `PassengerResponse` | 200 |

Registration and login are two of the only unauthenticated endpoints in the
entire app (the other is `GET /api/flights/**`) — everything else requires a
valid bearer token, and that token is minted here.

## 3. DTOs (field-level)

- **`RegisterRequest`**: `username` (string, required, 3–30 chars), `email`
  (string, required, must be a valid email format), `password` (string,
  required, custom-validated — see rule 4.3). **There is no `role` field.**
  Role can never be client-supplied at signup — see rule 4.2.
- **`LoginRequest`**: `username` (required, non-blank), `password` (required,
  non-blank — no format/strength check on login, only on registration).
- **`AuthResponse`**: `token` (JWT string), `expiresInSeconds` (long,
  currently always `3600` — see rule 4.6).
- **`PassengerResponse`**: `id`, `username`, `email`, `role`
  (`"PASSENGER"`, `"AGENT"`, or `"ADMIN"`). Never includes `passwordHash`.

## 4. Business rules

**4.1 — Username and email must be unique (case-sensitive, as implemented), username checked first.**

| Given | When | Then |
|---|---|---|
| A passenger with username "alice" already exists | POST /register with username "alice" | 409, code `DUPLICATE_USERNAME`, message contains "Username" |
| A passenger with email "a@x.com" already exists | POST /register with that email (different username) | 409, code `DUPLICATE_EMAIL`, message contains "Email" |
| Both username and email are already taken | POST /register | Username is checked **before** email — response is always `DUPLICATE_USERNAME`, never `DUPLICATE_EMAIL` |

Enforced at the application layer (`existsByUsername`/`existsByEmail`) as
well as DB-level unique constraints on the `passengers` table (`username`,
`email`) — see edge case in Section 5 about the gap between the two checks.

**4.2 — New registrations always get role `PASSENGER`; role is never client-suppliable.**

The request DTO has no `role` field at all, so there's no input to smuggle a
role through — `IdentityService.register` hardcodes `Role.PASSENGER` when
constructing the entity. `Role` has **three** values in this domain
(`PASSENGER`, `AGENT`, `ADMIN`) — unlike the old generic Auth/Users slice's
two-value enum. Neither `AGENT` nor `ADMIN` is reachable through any API in
this app; there is no self-service or admin-facing promotion endpoint.

**4.3 — Password rule (registration only): minimum 8 characters, at least one digit.**

Enforced by the custom `@ValidPassword` Bean Validation annotation
(`PasswordValidator`), not by the service layer — the kind of rule that's
invisible if you only skim the DTO for `@NotBlank`/`@Size`. No maximum
length, no special-character requirement, no uppercase requirement. Login
does **not** re-validate password format — only presence (`@NotBlank`).

**4.4 — Password is hashed with BCrypt before storage; plaintext is never persisted or returned.**

`passwordEncoder.encode(...)` on register, `passwordEncoder.matches(...)` on
login. `PassengerResponse` never includes the hash.

**4.5 — Login failure is intentionally indistinguishable between "unknown username" and "wrong password."**

| Given | When | Then |
|---|---|---|
| Username "ghost" doesn't exist | POST /login with username "ghost" | 401, code `INVALID_CREDENTIALS`, message "Invalid username or password" |
| Username "carl" exists, wrong password supplied | POST /login | 401, same code, same message |

Both paths produce the exact same status/code/message — a deliberate
security property (not leaking which field was wrong) that's easy to lose in
a rewrite if someone "helpfully" makes the messages more specific.

**4.6 — Successful login issues a JWT: subject = username, claim `role` = role name, 1-hour expiry.**

Token is signed HS256 with a secret from `app.jwt.secret` (config, not
hardcoded — but see non-functional note below). `AuthResponse.expiresInSeconds`
is always `3600`; this is a **hardcoded constant** (`JwtService.EXPIRY_SECONDS`),
not read from config — unlike the secret itself. No refresh token, no
sliding expiration, no server-side session/token store — fully stateless.

**4.7 — `GET /me` resolves the current passenger via the JWT's subject claim, not a path parameter.**

`JwtAuthFilter` parses the bearer token, sets `SecurityContext` with
principal = username and authority `ROLE_<role>` (from the token's `role`
claim, **not re-checked against the database**). `PassengerController.me()`
reads `Authentication.getName()` and looks the passenger up by username.
**Consequence:** if a passenger's role changes in the database after a token
was issued, the token still carries the old role until it expires — role
changes are not live.

**4.8 — Registration fires `PassengerRegisteredEvent` synchronously, inside the open transaction, before commit.**

`eventPublisher.publishEvent(...)` runs immediately after `passengerRepository.save(...)`,
still inside `@Transactional register(...)`. Consumed by
`loyalty.event.LoyaltyEventListener` to auto-create a `LoyaltyAccount` —
Identity itself has zero code dependency on the `loyalty` package; the
coupling only exists via the event bus. The event carries the **username**
(not the numeric id), matching the natural-key convention `Booking` also
uses. This is the same "fire inside the transaction" antipattern flagged in
Booking's events — an outbox/after-commit dispatch would be the improvement
opportunity, listed here for consistency rather than as an Identity-specific
finding.

## 5. Error handling

| Condition | Status | Code | Where enforced |
|---|---|---|---|
| Duplicate username | 409 | `DUPLICATE_USERNAME` | `IdentityService.register` (explicit `ApiException`) |
| Duplicate email | 409 | `DUPLICATE_EMAIL` | `IdentityService.register` |
| Bad credentials (either cause) | 401 | `INVALID_CREDENTIALS` | `IdentityService.login` |
| `@Valid` failure (bad email format, short username, weak password, blank fields) | 400 | `VALIDATION_FAILED` | `GlobalExceptionHandler` (`MethodArgumentNotValidException`), field-level messages included |
| Missing/invalid/expired bearer token on a protected endpoint | 401 | *(no error body from this app's code)* | **Spring Security's default entry point** — `JwtAuthFilter` just clears the security context on a bad token; the 401 itself comes from `anyRequest().authenticated()` rejecting an unauthenticated request. Not visible anywhere in `GlobalExceptionHandler`. |
| Passenger looked up by username not found (`IdentityService.getByUsername`) | 404 | `PASSENGER_NOT_FOUND` | Only reachable in practice if a valid, unexpired token references a username that's since been deleted from the DB — an edge case worth a test on the .NET side even though it's hard to hit manually. |

**Edge case not currently handled:** `existsByUsername`/`existsByEmail`
checks and the subsequent `save()` are not atomic — a race between two
concurrent registrations with the same username could both pass the
existence check and then hit the DB's unique constraint on `save()`, which
isn't caught anywhere here and would surface as an unhandled 500 rather than
a clean 409. Worth deciding deliberately for the .NET rewrite (e.g., catch
the DB constraint violation too, or accept the current gap).

## 6. Dependencies

- **Outbound:** none — this is the foundational slice (migrate first, per
  tracker order 1 of 5). The one cross-slice call in the app
  (`BookingService` → `FlightService.findOrThrow`) does not involve Identity.
- **Inbound:**
  - `loyalty.event.LoyaltyEventListener` consumes `PassengerRegisteredEvent`
    to auto-create a `LoyaltyAccount` (event-mediated, not a direct call —
    Identity has no knowledge of Loyalty).
  - Every other protected endpoint in the app depends on the JWT this slice
    issues and the `JwtAuthFilter`/`SecurityConfig` wiring that validates it
    on every request.

## 7. Tests already covering this behavior (mined from `IdentityServiceTest`)

Existing tests already assert rules 4.1, 4.2, 4.5, and 4.8 above:
`register_rejectsDuplicateUsername`, `register_rejectsDuplicateEmail`,
`register_newPassengerDefaultsToPassengerRole`,
`register_publishesPassengerRegisteredEvent`, `login_rejectsUnknownUsername`,
`login_rejectsWrongPassword`. No existing test covers rule 4.6 (JWT claim
contents/expiry) or rule 4.7 (`/me` resolution) — worth adding equivalent
coverage on the .NET side even though the Java side never tested it either.

## 8. Non-functional notes

- **BCrypt compatibility matters for a real migration.** If this were a real
  passenger table being migrated (not the dummy H2 DB), the existing password
  hashes are BCrypt-formatted. ASP.NET Core Identity's *default* password
  hasher is PBKDF2, not BCrypt-compatible — using it as-is would invalidate
  every existing password. The .NET side needs either a BCrypt-compatible
  verifier (e.g. `BCrypt.Net-Next`) wired in as a custom `IPasswordHasher`, or
  a deliberate one-time forced password reset. This is exactly the kind of
  migration detail that's invisible until you go looking for it.
- **JWT secret is in `application.yml` in plaintext** — flagged already in
  the migration plan's Section 8 (Security) as something to fix via a proper
  secrets manager on the .NET side; called out again here since this is the
  slice where it's actually used.
- Token expiry (1 hour) is currently a hardcoded constant, not configurable —
  worth deciding whether to externalize it in the .NET version (recommended)
  or preserve the hardcoding intentionally.
- **Three-role model (`PASSENGER`/`AGENT`/`ADMIN`)** vs. the old generic
  Auth/Users slice's two-role model — `AGENT` isn't exercised by any endpoint
  yet in this app (`SecurityConfig` only checks `hasRole("ADMIN")` anywhere),
  but the .NET rewrite should still model all three values faithfully since
  other slices may add `AGENT`-gated routes later.

## 9. Source references

Jump-to references for the manual approval gate (`03_02`) and the .NET code
review (`04_05`). Paths are relative to
`java-api/src/main/java/com/example/airlineapi/` unless noted otherwise.

**Endpoints (§2)**

| Endpoint | File | Lines |
|---|---|---|
| `POST /api/auth/register` | `identity/AuthController.java` | 15 (base path), 24–27 |
| `POST /api/auth/login` | `identity/AuthController.java` | 29–32 |
| `GET /api/passengers/me` | `identity/PassengerController.java` | 10 (base path), 19–22 |

**DTOs (§3)** — all in `identity/dto/IdentityDtos.java`

| DTO | Lines |
|---|---|
| `RegisterRequest` | 11–15 |
| `LoginRequest` | 17–20 |
| `AuthResponse` | 22 |
| `PassengerResponse` | 24 |

**Business rules (§4)**

| Rule | File | Lines |
|---|---|---|
| 4.1 — Uniqueness, username checked first | `identity/IdentityService.java` | 33–38 |
| 4.1 — Repository existence queries | `identity/PassengerRepository.java` | 9–10 |
| 4.1 — DB-level unique constraints | `identity/Passenger.java` | 16–19 |
| 4.2 — Role hardcoded to `PASSENGER` | `identity/IdentityService.java` | 43–48 (esp. 47) |
| 4.2 — Three-value enum | `identity/Role.java` | 3–7 |
| 4.2 — No `role` field on the request DTO | `identity/dto/IdentityDtos.java` | 11–15 |
| 4.3 — Password rule (≥8 chars, ≥1 digit) | `common/PasswordValidator.java` | 14–17 |
| 4.3 — Annotation + default message | `common/ValidPassword.java` | 16–20 (message: 17) |
| 4.3 — Applied to the `password` field | `identity/dto/IdentityDtos.java` | 14 |
| 4.4 — BCrypt encoder bean | `config/SecurityConfig.java` | 23–26 |
| 4.4 — `encode` on register / `matches` on login | `identity/IdentityService.java` | 46, 63 |
| 4.4 — Response excludes the hash | `identity/IdentityService.java` | 77–79 |
| 4.5 — Identical failure for both causes | `identity/IdentityService.java` | 60–65 |
| 4.6 — Token generation (subject, `role` claim, expiry) | `security/JwtService.java` | 24–34 |
| 4.6 — `EXPIRY_SECONDS` hardcoded constant | `security/JwtService.java` | 16 |
| 4.6 — Response assembly | `identity/IdentityService.java` | 67–68 |
| 4.7 — Filter sets principal + authority from claims | `security/JwtAuthFilter.java` | 31–47 (esp. 36–41) |
| 4.7 — Controller reads `Authentication.getName()` | `identity/PassengerController.java` | 19–22 |
| 4.7 — Lookup by username | `identity/IdentityService.java` | 71–75 |
| 4.8 — `@Transactional` boundary | `identity/IdentityService.java` | 31 |
| 4.8 — `publishEvent` inside the transaction | `identity/IdentityService.java` | 54 |
| 4.8 — Event type (carries username, not id) | `identity/event/PassengerRegisteredEvent.java` | 14–26 |

**Error handling (§5)**

| Condition | File | Lines |
|---|---|---|
| `DUPLICATE_USERNAME` | `identity/IdentityService.java` | 34 |
| `DUPLICATE_EMAIL` | `identity/IdentityService.java` | 37 |
| `INVALID_CREDENTIALS` (both paths) | `identity/IdentityService.java` | 61, 64 |
| `VALIDATION_FAILED` (field-level messages) | `common/GlobalExceptionHandler.java` | 19–26 |
| `ApiException` → response mapping | `common/GlobalExceptionHandler.java` | 13–17 |
| 401 with no body (bypasses the handler) | `config/SecurityConfig.java` | 50 (`anyRequest().authenticated()`) |
| Bad token clears context, doesn't reject | `security/JwtAuthFilter.java` | 42–46 |
| `PASSENGER_NOT_FOUND` | `identity/IdentityService.java` | 73 |
| **Race condition** — check/save not atomic | `identity/IdentityService.java` | 33, 36 (checks) vs. 49 (save) |

**Non-functional notes (§8)**

| Note | File | Lines |
|---|---|---|
| BCrypt hasher (PBKDF2 incompatibility) | `config/SecurityConfig.java` | 24–26 |
| JWT secret read from config | `security/JwtService.java` | 20–21 |
| JWT secret in plaintext | `java-api/src/main/resources/application.yml` | 28–32 |
| Expiry hardcoded, not configurable | `security/JwtService.java` | 16 |
| Only `ADMIN` is ever checked (`AGENT` unused) | `config/SecurityConfig.java` | 39–48 |

---

*No code has been written yet — this document is the `03_01` "Understand"
output only. Awaiting manual validation (`03_02`) before moving to `04_01`
(.NET slice design note) for this slice.*
