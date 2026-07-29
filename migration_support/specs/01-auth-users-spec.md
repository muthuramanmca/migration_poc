# Behavior Spec — Slice 1: Auth/Users

**Migration order:** 1 of 3 (foundational — no dependencies)
**Java source:** `AuthController`, `UserController`, `UserService`, `User`, `Role`, `UserRepository`, `UserDtos`, `ValidPassword`/`PasswordValidator`, `JwtService`, `JwtAuthFilter`, `SecurityConfig` (auth-related rules only)
**Status:** Spec Ready for Review

---

## 1. Purpose

Registers new users, authenticates existing ones, and issues/validates JSON
Web Tokens used for every other protected endpoint in the app. No entity
lifecycle here (unlike Orders) — a `User` is created once and never
transitions through states.

## 2. Endpoints

| Method | Path | Operation ID | Auth | Request | Response | Success |
|---|---|---|---|---|---|---|
| POST | `/api/auth/register` | `register` | Public | `RegisterRequest` | `UserResponse` | 201 |
| POST | `/api/auth/login` | `login` | Public | `LoginRequest` | `AuthResponse` | 200 |
| GET | `/api/users/me` | `me` | Any authenticated user | — | `UserResponse` | 200 |

Registration and login are the **only** unauthenticated endpoints in the
entire app besides `GET /api/products/**` — everything else requires a
valid bearer token, and that token is minted here.

## 3. DTOs (field-level)

- **`RegisterRequest`**: `username` (string, required, 3–30 chars), `email`
  (string, required, must be a valid email format), `password` (string,
  required, custom-validated — see rule 4.3). **There is no `role` field.**
  Role can never be client-supplied at signup — see rule 4.2.
- **`LoginRequest`**: `username` (required, non-blank), `password`
  (required, non-blank — no format/strength check on login, only on
  registration).
- **`AuthResponse`**: `token` (JWT string), `expiresInSeconds` (long,
  currently always `3600` — see rule 4.6).
- **`UserResponse`**: `id`, `username`, `email`, `role` (`"USER"` or
  `"ADMIN"`). Never includes `passwordHash`.

## 4. Business rules

**4.1 — Username and email must be unique (case-sensitive, as implemented).**
| Given | When | Then |
|---|---|---|
| A user with username "alice" already exists | POST /register with username "alice" | 409, code `DUPLICATE_USERNAME`, message contains "Username" |
| A user with email "a@x.com" already exists | POST /register with that email (different username) | 409, code `DUPLICATE_EMAIL`, message contains "Email" |
| Username check passes | Email check runs next | Username is checked **before** email — if both are duplicates, the response is always `DUPLICATE_USERNAME`, never `DUPLICATE_EMAIL` |

**4.2 — New registrations always get role `USER`; role is never client-suppliable.**
The request DTO has no `role` field at all, so there's no input to smuggle a
role through — the service hardcodes `Role.USER` when constructing the
entity. There is no self-service or admin-facing promotion endpoint in this
slice; becoming `ADMIN` isn't reachable through any API in this app.

**4.3 — Password rule (registration only): minimum 8 characters, at least one digit.**
Enforced by the custom `@ValidPassword` Bean Validation annotation
(`PasswordValidator`), not by the service layer — this is exactly the kind
of rule that's invisible if you only skim the DTO for `@NotBlank`/`@Size`.
No maximum length, no special-character requirement, no uppercase
requirement. Login does **not** re-validate password format — only
presence (`@NotBlank`).

**4.4 — Password is hashed with BCrypt before storage; plaintext is never persisted or returned.**
`passwordEncoder.encode(...)` on register, `passwordEncoder.matches(...)`
on login. `UserResponse` never includes the hash.

**4.5 — Login failure is intentionally indistinguishable between "unknown username" and "wrong password."**
| Given | When | Then |
|---|---|---|
| Username "ghost" doesn't exist | POST /login with username "ghost" | 401, code `INVALID_CREDENTIALS`, message "Invalid username or password" |
| Username "carl" exists, wrong password supplied | POST /login | 401, same code, same message |
Both paths produce the exact same status/code/message — a deliberate
security property (not leaking which field was wrong) that's easy to lose
in a rewrite if someone "helpfully" makes the messages more specific.

**4.6 — Successful login issues a JWT: subject = username, claim `role` = role name, 1-hour expiry.**
Token is signed HS256 with a secret from `app.jwt.secret` (config, not
hardcoded — but see non-functional note below). `AuthResponse.expiresInSeconds`
is always `3600`; this is a **hardcoded constant** (`JwtService.EXPIRY_SECONDS`),
not read from config — unlike the secret itself. No refresh token, no
sliding expiration, no server-side session/token store — fully stateless.

**4.7 — `GET /me` resolves the current user via the JWT's subject claim, not a path parameter.**
`JwtAuthFilter` parses the bearer token, sets `SecurityContext` with
principal = username and authority `ROLE_<role>` (from the token's `role`
claim, not re-checked against the database). `UserController.me()` reads
`Authentication.getName()` and looks the user up by username. **Consequence:**
if a user's role changes in the database after a token was issued, the
token still carries the old role until it expires — role changes are not
live.

## 5. Error handling

| Condition | Status | Code | Where enforced |
|---|---|---|---|
| Duplicate username | 409 | `DUPLICATE_USERNAME` | `UserService.register` (explicit `ApiException`) |
| Duplicate email | 409 | `DUPLICATE_EMAIL` | `UserService.register` |
| Bad credentials (either cause) | 401 | `INVALID_CREDENTIALS` | `UserService.login` |
| `@Valid` failure (bad email format, short username, weak password, blank fields) | 400 | `VALIDATION_FAILED` | `GlobalExceptionHandler` (`MethodArgumentNotValidException`), field-level messages included |
| Missing/invalid/expired bearer token on a protected endpoint | 401 | *(no error body from this app's code)* | **Spring Security's default entry point** — `JwtAuthFilter` just clears the security context on a bad token; the 401 itself comes from `anyRequest().authenticated()` rejecting an unauthenticated request. Not visible anywhere in `GlobalExceptionHandler`. |
| User looked up by username not found (`UserService.getByUsername`) | 404 | `USER_NOT_FOUND` | Only reachable in practice if a valid, unexpired token references a username that's since been deleted from the DB — an edge case worth a test on the .NET side even though it's hard to hit manually. |

**Edge case not currently handled:** `existsByUsername`/`existsByEmail`
checks and the subsequent `save()` are not atomic — a race between two
concurrent registrations with the same username could both pass the
existence check and then hit the DB's unique constraint on `save()`,
which isn't caught anywhere here and would surface as an unhandled 500
rather than a clean 409. Worth deciding deliberately for the .NET rewrite
(e.g., catch the DB constraint violation too, or accept the current gap).

## 6. Dependencies

- **Outbound:** none — this is the foundational slice (Phase 2b ordering:
  migrate first).
- **Inbound:** every other protected endpoint in the app depends on the JWT
  this slice issues and the `JwtAuthFilter`/`SecurityConfig` wiring that
  validates it on every request.

## 7. Tests already covering this behavior (Phase 3a — mined from `UserServiceTest`)

Existing tests already assert rules 4.1, 4.2, and 4.5 above:
`register_rejectsDuplicateUsername`, `register_rejectsDuplicateEmail`,
`register_newUserDefaultsToUserRole`, `login_rejectsUnknownUsername`,
`login_rejectsWrongPassword`. No existing test covers rule 4.6 (JWT claim
contents/expiry) or rule 4.7 (`/me` resolution) — worth adding equivalent
coverage on the .NET side even though the Java side never tested it either.

## 8. Non-functional notes

- **BCrypt compatibility matters for a real migration.** If this were a
  real user table being migrated (not the dummy H2 DB), the existing
  password hashes are BCrypt-formatted. ASP.NET Core Identity's *default*
  password hasher is PBKDF2, not BCrypt-compatible — using it as-is would
  invalidate every existing password. The .NET side needs either a
  BCrypt-compatible verifier (e.g. `BCrypt.Net-Next`) wired in as a custom
  `IPasswordHasher`, or a deliberate one-time forced password reset. This
  is exactly the kind of migration detail that's invisible until you go
  looking for it.
- **JWT secret is in `application.yml` in plaintext** — flagged already in
  the migration plan's Section 8 (Security) as something to fix via a
  proper secrets manager on the .NET side; called out again here since
  this is the slice where it's actually used.
- Token expiry (1 hour) is currently a hardcoded constant, not
  configurable — worth deciding whether to externalize it in the .NET
  version (recommended) or preserve the hardcoding intentionally.

---

*No code has been written yet — this document is the Phase 3a "Understand"
output only. Awaiting manual validation before moving to Phase 3 step 2
(Design) for this slice, per your instruction that we're doing spec
creation only for now.*
