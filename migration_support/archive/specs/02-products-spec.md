# Behavior Spec — Slice 2: Products

**Migration order:** 2 of 3 (no dependencies on other slices)
**Java source:** `Product`, `ProductRepository`, `ProductService`, `ProductController`, `ProductDtos`
**Status:** Spec Ready for Review

---

## 1. Purpose

Product catalog: browse/read (public), create and adjust stock
(admin-only). No entity lifecycle/state machine here (unlike Orders) — a
`Product` is created once and only its `stockQuantity` changes afterward.

## 2. Endpoints

| Method | Path | Operation ID | Auth | Request | Response | Success |
|---|---|---|---|---|---|---|
| GET | `/api/products` | `list` | Public | — | `ProductResponse[]` | 200 |
| GET | `/api/products/{id}` | `get` | Public | — | `ProductResponse` | 200 |
| POST | `/api/products` | `create` | **ADMIN only** | `ProductRequest` | `ProductResponse` | 201 |
| PUT | `/api/products/{id}/stock` | `adjustStock` | **ADMIN only** | `StockAdjustmentRequest` | `ProductResponse` | 200 |

Enforced by `SecurityConfig`: `GET /api/products/**` is explicitly public;
`POST`/`PUT` under `/api/products/**` require `ROLE_ADMIN`. There is no
"update product details" endpoint (name/price/description) — only
creation and stock adjustment exist.

## 3. DTOs (field-level)

- **`ProductRequest`**: `sku` (required, non-blank), `name` (required,
  non-blank), `description` (optional, no constraint), `price` (required,
  `>= 0.01`, i.e. must be positive — zero and negative rejected), `stockQuantity`
  (`>= 0` — negative initial stock rejected, zero allowed).
- **`StockAdjustmentRequest`**: `delta` (plain `int`, **no validation
  annotations at all**). One endpoint handles both increasing and
  decreasing stock via a signed delta — there's no separate
  increase/decrease endpoint.
- **`ProductResponse`**: `id`, `sku`, `name`, `description`, `price`,
  `stockQuantity`, `lowStock` (boolean, computed — see rule 4.2).

## 4. Business rules

**4.1 — SKU must be unique.**
| Given | When | Then |
|---|---|---|
| A product with SKU "SKU-1" exists | POST /products with SKU "SKU-1" | 409, code `DUPLICATE_SKU` |
Same race-condition caveat as Auth/Users' username/email checks: the
existence check and the `save()` aren't atomic, so a concurrent duplicate
create could still slip past the check and fail at the DB's unique
constraint instead, uncaught, as an unhandled 500.

**4.2 — `lowStock` is computed as `stockQuantity < threshold`, where the threshold comes from config (`app.products.low-stock-threshold`, currently `10`) — ⚠️ but only on some code paths.**
This is the most important finding in this slice. The threshold lives on
the entity as a `@Transient` field defaulted to `10` in Java, and is only
ever overwritten with the *actual configured value* by
`ProductService.findOrThrow()`. That method backs `getById` and
`adjustStock` — but **`listAll()` calls `productRepository.findAll()`
directly and never goes through `findOrThrow()`**, so every product
returned by `GET /api/products` computes `lowStock` against the
hard-coded Java default (`10`), not the configured value.

Right now this is invisible because the configured value *happens* to
also be `10`. If someone changed `app.products.low-stock-threshold` to,
say, `5`, `GET /api/products` would still flag anything under 10 as low
stock, while `GET /api/products/{id}` for the same product would only
flag it under 5 — two endpoints disagreeing about the same product's
`lowStock` value. No existing test catches this (see Section 7) — it only
surfaced from reading the entity and service together, which is exactly
the "hunt for logic in unexpected places" step in Phase 3a. **Decision
needed for the .NET rewrite: reproduce this inconsistency, or fix it so
both endpoints use the same threshold consistently (recommended).**

**4.3 — Stock adjustment cannot take quantity negative, and applies to both directions through one endpoint.**
| Given | When | Then |
|---|---|---|
| Product has stock 5 | PUT .../stock with `delta: -10` | 409, code `INSUFFICIENT_STOCK`, message contains "negative" |
| Product has stock 5 | PUT .../stock with `delta: 3` | 200, stock becomes 8 |
| Product has stock 5 | PUT .../stock with `delta: -5` | 200, stock becomes 0 (zero is allowed, only negative is rejected) |
Because `StockAdjustmentRequest.delta` has no validation, an empty/missing
`delta` in the request body deserializes to `0` (Java's default for a
primitive `int`) rather than triggering a 400 — a silent no-op rather
than a validation error. `ProductRequest`, by contrast, validates every
field. Worth deciding whether to preserve this asymmetry.

**4.4 — Product lookup failure.**
`getById` and `adjustStock` both go through `findOrThrow`: unknown ID →
404, code `PRODUCT_NOT_FOUND`.

## 5. Error handling

| Condition | Status | Code | Where enforced |
|---|---|---|---|
| Duplicate SKU on create | 409 | `DUPLICATE_SKU` | `ProductService.create` |
| Stock adjustment would go negative | 409 | `INSUFFICIENT_STOCK` | `ProductService.adjustStock` — **note:** the Orders slice (not yet migrated) will very likely reuse this same `INSUFFICIENT_STOCK` code for a related-but-distinct case (not enough stock to fulfill an order line). Decide during Orders' spec whether that's intentional shared vocabulary or should be split into two codes. |
| Product not found | 404 | `PRODUCT_NOT_FOUND` | `ProductService.findOrThrow` |
| `@Valid` failure on `ProductRequest` (blank sku/name, price ≤ 0, negative initial stock) | 400 | `VALIDATION_FAILED` | `GlobalExceptionHandler` |
| Bad/missing `delta` on `StockAdjustmentRequest` | *(none — silently defaults to 0)* | — | No validation exists; not an error path today |

## 6. Dependencies

- **Outbound:** none.
- **Inbound:** `ProductService.findOrThrow(Long id)` is called directly by
  `OrderService` (a different package/slice) to check stock and
  reserve/restock during order creation and cancellation. It's `public`
  for exactly this reason. **When we design the .NET version of this
  slice, it needs an equivalent public lookup method — the Orders slice
  migration will depend on it existing.**

## 7. Tests already covering this behavior (Phase 3a — mined from `ProductServiceTest`)

`create_rejectsDuplicateSku`, `create_flagsLowStockWhenBelowThreshold`,
`create_doesNotFlagLowStockAboveThreshold`, `adjustStock_rejectsNegativeResultingQuantity`.
None of these exercise `listAll()` at all — which is exactly why the
`findOrThrow`-vs-`findAll` threshold inconsistency in rule 4.2 was never
caught by the Java test suite either. Worth adding a test for `listAll()`
low-stock behavior on the .NET side regardless of which way the
inconsistency gets resolved.

## 8. Non-functional notes

- No update-product-details endpoint exists (name/price/description are
  immutable after creation in this app) — confirm whether that's
  intentional scope or a gap worth closing in the rewrite.
- `StockAdjustmentRequest` having zero validation is inconsistent with
  every other mutating DTO in the app; worth flagging as a deliberate
  decision point rather than carrying it forward by default.

---

*Phase 3a "Understand" output only — no code written yet. Awaiting manual
validation before moving to the next slice.*
