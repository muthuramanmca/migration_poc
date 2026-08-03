# Design Note — Slice 1: Identity

**Behavior spec:** `migration_support/03_01_java_api_slice_behaviour_doc/01-identity-spec.md` (Spec Validated)
**Status:** Design Note Ready
**Projects touched:** `Identity.Api`, `Identity.Application`, `Identity.Domain`, `Identity.Infrastructure`
**Also touched (config only, no code changes to these projects):** `Gateway` (appsettings — self-referencing Authority), `BuildingBlocks.Security` (already built, consumed as-is)

Contract-level only, per `04_01_net_slice_design_note/README.md` — routes, DTO shapes, service
interface signatures, EF Core entity/config changes. No method bodies; those land in `04_02`.

---

## 1. Endpoints

The Gateway's `identity-route` matches `/api/identity/{**catch-all}` with **no path-rewrite
transform** (`Gateway/appsettings.json`), so it forwards the full incoming path unchanged.
Identity.Api's own local routes must therefore literally include the `/api/identity` prefix —
they are not `/api/auth/...` like Java.

| Method | Local path (Identity.Api) | Operation | Auth | Request | Response | Success |
|---|---|---|---|---|---|---|
| POST | `/api/identity/auth/register` | Register | Public | `RegisterRequest` | `PassengerResponse` | 201 |
| POST | `/api/identity/auth/login` | Login | Public | `LoginRequest` | `AuthResponse` | 200 |
| GET | `/api/identity/passengers/me` | Me | Any authenticated passenger | — | `PassengerResponse` | 200 |
| GET | `/.well-known/jwks.json` | JWKS | Public | — | JWKS JSON | 200 | *(already built)* |
| GET | `/.well-known/openid-configuration` | OIDC discovery | Public | — | OIDC metadata JSON | 200 | *(new — see §5)* |

## 2. DTOs (field-level, C# records — `Identity.Application`)

```
RegisterRequest  { string Username; string Email; string Password; }
LoginRequest     { string Username; string Password; }
AuthResponse     { string Token; long ExpiresInSeconds; }
PassengerResponse{ Guid Id; string Username; string Email; string Role; }
```

Matches spec §3 field-for-field. `RegisterRequest` has no `Role` field — same as Java,
preserves rule 4.2 (role is never client-suppliable). `PassengerResponse` never carries
`PasswordHash`.

**File layout — needs your call (see §6.3.3):** one `IdentityDtos.cs` with nested records
(mirrors Java's single-file-per-slice convention) vs. one file per record under `Dtos/`.

## 3. Entity / EF Core changes

- **`User` (`Identity.Domain`)** — already has `Id (Guid)`, `Username`, `Email`
  (`[SensitiveData]`), `PasswordHash`, `Role (string)`, `CreatedAtUtc`. Covers every spec field;
  **no schema change needed.** `Id` is `Guid`, not Java's auto-increment `Long` — already the
  established convention across every service, not re-litigated here.
- **`Role` value convention** — stored/returned as `Roles.Passenger` / `Roles.Agent` /
  `Roles.Admin` (`BuildingBlocks.Security.Roles`, titlecase), not Java's uppercase enum name
  (`"PASSENGER"`). Deliberate: these constants are already the shared convention other services
  will authorize against; Identity is just the first slice to actually populate them.
- **`RefreshToken`** — exists, unused by this slice's three endpoints (spec has no refresh
  endpoint). Out of scope for this pass, left as-is.
- **`IdentityDbContext` — needs the outbox enabled.** ADR 0001 states *"Identity ... publish[es]
  nothing yet"* and skips the outbox for it. That's now stale: spec rule 4.8 confirms Identity
  fires `PassengerRegisteredEvent`, consumed by Loyalty. Add
  `AddEntityFrameworkOutbox<IdentityDbContext>` in `04_02`, same as Booking/FlightInventory —
  this is the direct fix for the "fires inside the open transaction, before commit" antipattern
  the spec flags in rule 4.8, consistent with why the outbox exists at all.

## 4. Service interface (signatures only)

```
IIdentityService (Identity.Application)
    Task<PassengerResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse>      LoginAsync(LoginRequest request, CancellationToken ct);
    Task<PassengerResponse> GetByUsernameAsync(string username, CancellationToken ct);
```

One service backing both `AuthController`-equivalent and `PassengersController`-equivalent
endpoints, matching Java's single `IdentityService`.

**`IUserRepository` needs two additions** — it currently has `GetByIdAsync`,
`GetByUsernameAsync`, `AddAsync` only; there is **no email lookup at all**, but rule 4.1 requires
one:

```
Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
```

## 5. Cross-cutting gaps this note surfaces (required before `04_02`, not optional)

These aren't pattern choices — they're contract-level holes the current skeleton has relative to
what this slice's approved spec actually needs:

1. **`IUserRepository` has no `ExistsByEmailAsync`** (§4 above) — rule 4.1 can't be implemented
   without it.
2. **`GET /.well-known/openid-configuration` doesn't exist yet.** `JwtBearerOptions.Authority`
   (used by every service, `BuildingBlocks.Security.JwtAuthenticationExtensions`) triggers OIDC
   metadata discovery against `{Authority}/.well-known/openid-configuration` to *find* the JWKS
   `jwks_uri` — it doesn't hit `/.well-known/jwks.json` directly. Only the JWKS endpoint exists
   today, so every service's token validation (Gateway included) will fail metadata discovery
   until this is added.
3. **Identity.Api isn't a JWT resource server yet.** `Program.cs` currently says *"not (yet) a
   resource server — no JwtBearer auth wired here."* But `/me` requires authentication, and
   ADR 0001's zero-trust rule says every service validates independently, including the issuer.
   Identity.Api needs `AddBuildingBlocksJwtAuthentication` + `UseAuthentication`/
   `UseAuthorization`, same as `Booking.Api` already does — plus a self-referencing
   `Identity:Authority` value (e.g. `https://localhost:5001`) added to
   `Identity.Api/appsettings.json`, which isn't there today.
4. **Outbox on `IdentityDbContext`** — see §3.

## 6. Design Patterns

### 6.1 Reused from Java
- Layering: Controller (`Identity.Api`) → Service (`Identity.Application`) → Repository
  (`Identity.Infrastructure`) — same shape as Java's Controller/Service/Repository, per the
  migration plan's Section 2 mapping.
- DTOs as plain request/response records (Java records → C# `record`).
- Password hashing stays **BCrypt** (`BCrypt.Net-Next`), not ASP.NET Core's default PBKDF2 —
  preserves spec rule 4.4 exactly and is the direct implementation of the migration plan's
  non-functional note on BCrypt-hash compatibility.
- Error codes and status mapping (rule set in spec §5: `DUPLICATE_USERNAME`, `DUPLICATE_EMAIL`,
  `INVALID_CREDENTIALS`, `VALIDATION_FAILED`, `PASSENGER_NOT_FOUND`) carry over unchanged, thrown
  as `ApiException` and rendered by the already-built `GlobalExceptionMiddleware` — same role as
  Java's `GlobalExceptionHandler`.
- Behavioral rules already locked in the approved spec (username-checked-before-email,
  identical login-failure message, no live role re-check on `/me`) are **not** re-decided here —
  cited from `03_01`, not restated.

### 6.2 Already mandated project-wide (cite ADR 0001, not re-justified)
- RS256 + JWKS instead of Java's HS256 (`RsaSigningKeyProvider`, already built).
- Zero-trust JWT validation on every service, Identity.Api included (§5.3).
- Outbox for any event published as part of a local DB write (§3) — now applies to Identity too.
- Guid primary keys, database-per-service, `[SensitiveData]` redaction on `Email` — all already
  applied on `User`.
- MFA hook point (`IMfaChallengeProvider`) — `LoginAsync` should call
  `IsChallengeRequiredAsync` (returns `false` via the `NoOp` implementation today) to keep the
  hook meaningful, per ADR 0001's "wired into the shape now" intent. No spec rule exercises this
  — it's plumbing, not a new decision.

---

> [!IMPORTANT]
> **Manual gate (`04_01b`) — needs your sign-off before `04_02` generates any code.**
> The rest of this design note (§1–6.2) follows directly from the approved spec and
> project-wide conventions; these three items are genuine per-slice decisions nothing
> upstream already answers.

### 6.3 New for this slice
1. **DTO/password validation approach.** Data Annotations + a custom `ValidationAttribute`
   (closest 1:1 idiom to Java's `@ValidPassword`) vs. FluentValidation (more testable in
   isolation; since no ADR precedent exists yet, whichever is picked here becomes the de facto
   convention for every later slice's DTOs). **Recommend FluentValidation**, for that
   project-wide-precedent reason — confirm or override.
2. **Close the registration race condition**, or preserve it. Spec §5 flags as
   "not currently handled": concurrent registrations with the same username can both pass the
   `Exists` check and then hit the DB unique constraint as an unhandled 500. Recommend catching
   `DbUpdateException` around `SaveChangesAsync` in `RegisterAsync` and translating a
   unique-constraint violation into the same `DUPLICATE_USERNAME`/`DUPLICATE_EMAIL` 409 — a
   deliberate improvement over Java, cheap to do since EF Core surfaces this cleanly. Confirm you
   want it closed now vs. carried forward as-is for behavioral parity with the spec.
3. **DTO file layout** — single `IdentityDtos.cs` (mirrors Java, easiest side-by-side diffing)
   vs. one file per record under `Dtos/` (more idiomatic C#). No strong recommendation either
   way — pick based on which you'll value more once Booking/Flight DTOs also exist.

---

## 7. Dependencies
Same as spec §6: no outbound dependencies (foundational slice). Inbound: Loyalty's
`LoyaltyEventListener` consumes `PassengerRegisteredEvent` once the outbox is enabled (§3);
every other service depends on the JWKS/OIDC endpoints this slice serves (§5.2).

## 8. Not in scope for this slice's `04_02`
`RefreshToken` rotation/reuse-detection, a real MFA provider, and RSA key
persistence/rotation — all already deferred by ADR 0001, unchanged by this note.

---

*No code has been written yet — this document is the `04_01` design output only. Once you
confirm §6.3 (`04_01b`, the manual gate above), `04_02` generates the actual
Controller/Service/Repository/DTO/validator code from this note plus the approved spec.*
