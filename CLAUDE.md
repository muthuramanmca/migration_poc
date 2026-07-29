# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A **Java → .NET Core migration PoC** for United Airlines. The source app is a synthetic Spring Boot 3 REST API (`java-api/`) standing in for a real codebase. The target .NET Core solution lives in the sibling `dotnet-api/` folder (created once code generation begins). The goal is to build a repeatable Claude-assisted migration playbook using a Strangler Fig pattern — one bounded slice at a time, spec-first before any code generation.

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
mvn test -Dtest=OrderServiceTest

# Run a single test method
mvn test -Dtest=OrderServiceTest#create_rejectsWhenStockInsufficient

# Swagger UI (when app is running)
# http://localhost:8081/swagger-ui.html

# OpenAPI JSON contract (Phase 2a source of truth)
# http://localhost:8081/v3/api-docs

# H2 console (in-memory DB, wiped on restart)
# http://localhost:8081/h2-console  (JDBC URL: jdbc:h2:mem:dummydb)
```

---

## Migration workflow and current state

The migration follows a strict spec-first discipline: **no .NET code is written until the behavior spec for a slice is validated**. The tracker is the source of truth for what's next.

**Current status** (`migration_support/migration-tracker.csv`):

| Slice | Order | Status |
|---|---|---|
| Auth/Users | 1 | Spec Validated |
| Products | 2 | Spec Validated |
| Orders | 3 | Spec Ready for Review |

**The workflow per slice** (step IDs match `Java-to-DotNetCore-Migration-Plan.md` Section 4):
1. Read the relevant Java source from `java-api/src/main/java/com/example/dummyapi/<domain>/`
2. Write the behavior spec to `migration_support/specs/<N>-<slice>-spec.md` (`03_01` — create slice behaviour doc)
3. Set `migration-tracker.csv` status to `Spec Ready for Review` and present it in chat
4. User validates; status becomes `Spec Validated` (or `Rework Needed` if corrections are needed) — this is `03_02`, the manual approval gate
5. Only after all specs are validated does .NET code generation begin (`04_02` — generate code)

---

## Java source architecture

**Package layout** (`com.example.dummyapi`):

- `user/` — `User` entity, `Role` enum, `UserService`, `UserRepository`, `AuthController` (register/login), `UserController` (/me), `UserDtos` (all DTOs as inner records)
- `product/` — `Product` entity, `ProductService`, `ProductRepository`, `ProductController`, `ProductDtos`
- `order/` — `Order` entity, `OrderItem`, `OrderStatus` enum, `OrderService`, `OrderRepository`, `OrderController`, `OrderDtos`, `event/OrderCreatedEvent`, `event/OrderEventListener`
- `security/` — `JwtService` (JJWT 0.12.x, HS256, 1-hour expiry), `JwtAuthFilter` (stateless bearer-token filter)
- `config/` — `SecurityConfig` (public routes: register, login, GET /api/products/**), `OpenApiConfig`
- `common/` — `ApiError`/`ApiException` (the error envelope), `GlobalExceptionHandler` (`@ControllerAdvice`), `PasswordValidator`/`ValidPassword` (custom Bean Validation)

**Cross-cutting patterns:**
- All API errors go through `GlobalExceptionHandler` → returns `ApiError { code, message, details }` — **except** Spring Security's own 401/403 rejections, which bypass it entirely and produce no body
- DTOs are all Java records defined as static inner classes inside `*Dtos.java` files (e.g., `UserDtos.RegisterRequest`)
- No Lombok — all boilerplate is explicit

**Cross-slice dependency:** `OrderService` calls `ProductService.findOrThrow(Long id)` directly. This is the only inter-slice coupling in the source app, and why Orders must be migrated last.

---

## Key behavioral findings from the specs

These are non-obvious facts that must be preserved (or deliberately changed) in the .NET rewrite:

### Auth/Users
- Username uniqueness is checked **before** email — if both are duplicate, only `DUPLICATE_USERNAME` is returned
- Login errors are **intentionally identical** for "no such user" vs "wrong password" — do not make them more specific
- `GET /me` roles come from the JWT claim, **not a DB re-read** — role changes don't take effect until token expiry
- For a real user-table migration: existing BCrypt hashes are incompatible with ASP.NET Core Identity's default PBKDF2 hasher

### Products
- `ProductResponse.lowStock` is computed inconsistently: `GET /api/products/{id}` uses the configured threshold; `GET /api/products` (list) uses a hardcoded `10` because `listAll()` skips `findOrThrow()`. Both currently agree (configured value is also 10), but they would diverge if the config changed. **Decision needed:** replicate the bug or fix it.
- `StockAdjustmentRequest.delta` has **no validation** — a missing body field silently becomes `0` (no-op), unlike every other mutating DTO in the app

### Orders
- Order creation reserves stock per line item **in a single transaction** with no intermediate `save()` calls — EF Core rewrite must call `SaveChangesAsync()` once at the end, not per item
- The order-created event fires **synchronously inside the open transaction** (before commit) — an improvement opportunity: fire it after a successful `SaveChangesAsync()` instead
- `ship` and `deliver` have **no per-order ownership check** — any admin can act on any order; this is intentional, not a bug
- `cancel` always restocks, whether the order was `PENDING` or `PAID` (stock was reserved at creation, before payment)
- Cancellation is blocked once an order is `SHIPPED` or `DELIVERED` — state transitions live on the `Order` entity's `transitionTo()` method, not in the service

**State machine:** `PENDING → PAID → SHIPPED → DELIVERED`; `PENDING` or `PAID` may go to `CANCELLED`; `SHIPPED`/`DELIVERED`/`CANCELLED` are terminal.

---

## Migration plan reference

`Java-to-DotNetCore-Migration-Plan.md` is the master document. Key sections:
- **Section 2** — Java → .NET concept mapping table (Spring annotations → ASP.NET Core equivalents)
- **Section 4** — The 6-phase plan with Mermaid diagrams (currently in Phase 3)
- **Section 3a** — The spec-writing toolkit (decision tables, Gherkin, state diagrams, mining test suites)
- **Section 8** — Concrete .NET architectural choices: Nullable reference types, EF Core query discipline (`AsNoTracking`), `System.Text.Json`, Polly for resilience, OpenTelemetry

The planned .NET solution structure (Phase 0, not yet created):
```
Api/          ← Controllers/Minimal API, DI wiring, Program.cs
Application/  ← Services, business logic, DTOs, validators
Domain/       ← Entities, enums, domain logic
Infrastructure/ ← EF Core DbContext, repositories, external clients
Tests/        ← Unit + integration
```

---

## Files to read when working on a slice

When generating specs or .NET code for a slice, always read these together (not one at a time):

**Auth/Users:** `user/AuthController.java`, `user/UserController.java`, `user/UserService.java`, `user/User.java`, `user/Role.java`, `user/dto/UserDtos.java`, `common/PasswordValidator.java`, `common/ValidPassword.java`, `security/JwtService.java`, `security/JwtAuthFilter.java`, `config/SecurityConfig.java`

**Products:** `product/ProductController.java`, `product/ProductService.java`, `product/Product.java`, `product/ProductRepository.java`, `product/dto/ProductDtos.java`

**Orders:** `order/OrderController.java`, `order/OrderService.java`, `order/Order.java`, `order/OrderItem.java`, `order/OrderStatus.java`, `order/dto/OrderDtos.java`, `order/event/OrderCreatedEvent.java`, `order/event/OrderEventListener.java` — plus `product/ProductService.java` for the cross-slice call
