# Behavior Spec — Slice 3: Orders

**Migration order:** 3 of 3 — depends on Auth/Users (identity) and Products (stock/pricing)
**Java source:** `Order`, `OrderItem`, `OrderStatus`, `OrderRepository`, `OrderService`, `OrderController`, `OrderDtos`, `OrderCreatedEvent`, `OrderEventListener`
**Status:** Spec Ready for Review

---

## 1. Purpose

Places orders against the product catalog, reserves/restocks inventory,
and drives the order through a fixed status lifecycle. The most complex
slice: it's the only one with a state machine, the only one with an
integration event, and the only one that calls into another slice's
service (`ProductService`) mid-transaction.

## 2. Endpoints

| Method | Path | Operation ID | Auth | Ownership check? |
|---|---|---|---|---|
| GET | `/api/orders` | `list_1` | Authenticated | Query-filtered (see 4.5) |
| POST | `/api/orders` | `create_1` | Authenticated | N/A (creates own order) |
| GET | `/api/orders/{id}` | `get_1` | Authenticated | Yes — owner or admin |
| POST | `/api/orders/{id}/pay` | `pay` | Authenticated | Yes — owner or admin |
| POST | `/api/orders/{id}/ship` | `ship` | **ADMIN only** | **No ownership check at all** |
| POST | `/api/orders/{id}/deliver` | `deliver` | **ADMIN only** | **No ownership check at all** |
| POST | `/api/orders/{id}/cancel` | `cancel` | Authenticated | Yes — owner or admin |

`ship`/`deliver` don't even receive the `Authentication` object in the
controller — they're gated purely by `SecurityConfig`'s role rule, with no
per-order ownership logic. This is a deliberate asymmetry: paying/cancelling
are customer actions (restricted to the order's owner, or an admin acting
on their behalf), while shipping/delivering are fulfilment-staff actions
where any admin can act on any order — not a gap, but worth stating
explicitly so it isn't "fixed" by accident during the rewrite.

## 3. State machine (lives on the `Order` entity, not the service)

```
PENDING --pay--> PAID --ship--> SHIPPED --deliver--> DELIVERED
   |                |
   +---cancel-------+---cancel---> CANCELLED
```

- `SHIPPED` and `DELIVERED` can never reach `CANCELLED` — once shipped, an
  order cannot be cancelled through this API.
- `DELIVERED` and `CANCELLED` are fully terminal (no transitions out).
- Any disallowed transition → 409, code `INVALID_ORDER_STATE`, e.g. calling
  `ship` on a `PENDING` order (must be `pay`'d first).

## 4. Business rules

**4.1 — Order creation validates and reserves stock per line item, inside one transaction.**
For each `OrderLineRequest`: look up the product (`ProductService.findOrThrow`
— cross-slice call into the already-migrated Products slice), reject with
409 `INSUFFICIENT_STOCK` if requested quantity exceeds current stock,
otherwise decrement stock immediately and add an `OrderItem` capturing
`unitPriceAtPurchase` from the product's *current* price at that moment.
**Critical for the .NET rewrite:** the whole method is one transaction. If
line 3 of a 3-item order fails on insufficient stock, lines 1–2's stock
decrements must roll back too — in the Java version this happens for free
via JPA dirty-checking + Spring's rollback-on-unchecked-exception inside
`@Transactional`, with **no explicit `save()` call on the `Product`
entities at all**. An EF Core implementation needs the equivalent: don't
call `SaveChangesAsync()` per line item, only once at the end (or wrap in
an explicit transaction) — otherwise a partial multi-item order could
leave some products' stock decremented and others not.

**4.2 — Order total is computed once at creation and persisted; line totals are recomputed on every read.**
`Order.totalAmount` is calculated by `recalculateTotal()` when items are
added and stored in the DB. Each `OrderItemResponse.lineTotal`, by
contrast, is **not stored** — it's recomputed from
`unitPriceAtPurchase * quantity` every time the response is built. No
drift risk today because order items are never modified after creation in
this app — worth re-checking if an "edit order" feature is ever added.

**4.3 — Order-created event fires synchronously, before the transaction commits.**
`OrderService.create` calls `eventPublisher.publishEvent(...)` using a
plain `@EventListener` (not `@TransactionalEventListener(phase = AFTER_COMMIT)`).
Spring's default `ApplicationEventPublisher` invokes listeners
**synchronously, in-line, still inside the open transaction** — the DB
commit only happens after the `@Transactional` method returns. This means
if the commit itself failed for any reason after the event fired (rare,
but possible — e.g., a constraint violation flushed at commit time), the
"order created" notification would already have gone out for an order
that never actually persisted. Worth a deliberate decision for .NET: fire
the equivalent notification after a successful commit (e.g., via an
outbox pattern, or publishing after `SaveChangesAsync()` returns
successfully), which would actually be *more* correct than the current
Java behavior — flagging as an improvement opportunity, not just a
like-for-like port target.

**4.4 — Cancelling always restocks, regardless of whether the order was PENDING or PAID.**
Because stock is reserved (decremented) at order-creation time — before
any payment step exists — cancelling from either `PENDING` or `PAID`
restocks identically. `transitionTo(CANCELLED)` runs first (which is what
actually enforces "only PENDING or PAID can be cancelled" — see the state
machine), then every item's product is restocked via
`ProductService.findOrThrow` + `product.increaseStock(...)`, again with
**no explicit save** — relying on the same transactional dirty-checking
flush as rule 4.1.

**4.5 — List results are scoped by identity two different ways depending on endpoint.**
`GET /api/orders` (list) filters **at the query level**: admins get
`findAll()`, everyone else gets `findByUsername(requester)` — the
non-owner's orders are never fetched from the DB at all. `GET /{id}`,
`pay`, and `cancel` instead fetch the order first and then call
`assertOwnerOrAdmin`, throwing `AccessDeniedException` if the requester
isn't the owner or an admin. Two structurally different techniques
enforcing the same "you only see your own data" principle — worth
implementing both correctly rather than assuming one pattern covers both
cases.

## 5. Error handling

| Condition | Status | Code | Where enforced |
|---|---|---|---|
| Insufficient stock during order creation | 409 | `INSUFFICIENT_STOCK` | `OrderService.create` — shares this code with Products' `adjustStock` (see Products spec, Section 5) |
| Invalid state transition (pay/ship/deliver/cancel out of order) | 409 | `INVALID_ORDER_STATE` | `Order.transitionTo` |
| Order not found | 404 | `ORDER_NOT_FOUND` | `OrderService.findOrThrow` (private, order-local) |
| Referenced product not found (create or cancel) | 404 | `PRODUCT_NOT_FOUND` | Propagated from `ProductService.findOrThrow` |
| Non-owner, non-admin accessing `get`/`pay`/`cancel` | 403 | *(no error body from this app's code)* | `AccessDeniedException` thrown in `assertOwnerOrAdmin`, mapped to 403 by **Spring Security's default handler** — same "invisible" framework-level mapping already noted in the Auth/Users spec, not present anywhere in `GlobalExceptionHandler` |
| `@Valid` failure (empty `items` list, missing `productId`, `quantity < 1`) | 400 | `VALIDATION_FAILED` | `GlobalExceptionHandler` |

## 6. Dependencies

- **Outbound:** `ProductService.findOrThrow` (Products slice — must exist
  first, confirmed available per that slice's spec) for stock
  checks/reservation/restock. Order's `username` field is populated from
  `Authentication.getName()`, which only works because the Auth/Users
  slice's JWT issuing/validation is already in place.
- **Inbound:** none — nothing else in the app depends on Orders.

This is exactly why Orders is migrated last (Phase 2b dependency
ordering): it's the only slice that can't be built or meaningfully tested
without both other slices already existing.

## 7. Tests already covering this behavior (Phase 3a — mined from `OrderServiceTest`)

`create_rejectsWhenStockInsufficient`, `create_decrementsStockAndPublishesEvent`,
`cancel_restocksItemsWhetherPendingOrPaid`, `shipDirectlyFromPending_isRejected`.

**⚠️ One test's name overpromises what it actually checks.**
`cancel_restocksItemsWhetherPendingOrPaid` never transitions the order to
`PAID` before cancelling — it builds `new Order("carl")`, which defaults
to `PENDING`, and cancels directly from there. It only exercises the
`PENDING → CANCELLED` restock path; the `PAID → CANCELLED` path (rule 4.4)
is **not actually covered** despite the test name. This is a direct
example of why Phase 3a says to verify what a test's assertions do, not
just trust its name — worth adding the missing `PAID`-path test on the
.NET side rather than assuming Java's suite already proves it.

**Also not covered by any existing test:** the full `pay → ship → deliver`
happy path, the `assertOwnerOrAdmin` 403 paths (get/pay/cancel by a
non-owner), and the admin-sees-all vs. user-sees-own branching in
`listForUser`. All worth adding for the .NET version.

## 8. Non-functional notes

- Stock mutations on `Product` during order creation/cancellation rely
  entirely on JPA's automatic dirty-checking flush at transaction commit
  — there is no explicit `save()` call anywhere in this flow. EF Core's
  change tracking is analogous, but the .NET implementation must actually
  call `SaveChangesAsync()` once at the right point in the same unit of
  work, or these mutations will silently not persist.
- The event-timing issue in rule 4.3 is a good candidate to *improve*
  rather than replicate — worth flagging as a deliberate design decision
  when we get to Phase 3 step 2 (Design) for this slice, not something to
  quietly fix without noting it.

---

*Phase 3a "Understand" output only — no code written yet. This completes
the spec pass for all three slices (Auth/Users, Products, Orders).
Awaiting your validation.*
