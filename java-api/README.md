# dummy-api

A small, deliberately realistic Spring Boot REST API used as the source
application for the **Java -> .NET Core migration exercise**. It is not meant
to be a production app -- it exists so the migration process (OpenAPI
extraction, slice grouping/ordering, the Phase 3 rewrite loop, the Phase 3a
logic-extraction toolkit) has real Java code, real business rules, and a
real running app to practice against.

## Modules (maps directly to the plan's example queue)

| Module      | Package                          | Depends on |
|-------------|-----------------------------------|------------|
| Auth/Users  | `com.example.dummyapi.user`       | -- (foundational) |
| Products    | `com.example.dummyapi.product`    | -- |
| Orders      | `com.example.dummyapi.order`      | Users, Products |

This matches the "Auth/Users -> Products -> Orders" example processing order
used in the Phase 2b flowchart.

## Run it

Requires Java 17+ and Maven.

```bash
mvn spring-boot:run
```

The app starts on `http://localhost:8080` with an in-memory H2 database
(no external setup needed; data resets every restart).

## Phase 2a -- extract the API contract

```bash
curl http://localhost:8080/v3/api-docs
```

or open `http://localhost:8080/swagger-ui.html` in a browser. This is the
machine-readable contract the migration plan says to pull before hand-cataloguing
endpoints.

## Run the tests (Phase 3a -- "mine the existing test suite" in practice)

```bash
mvn test
```

`UserServiceTest`, `ProductServiceTest`, and `OrderServiceTest` encode the
business rules below as executable assertions -- read these before reading
the service implementations, exactly as Phase 3a recommends.

## Try the business flow end to end

```bash
# 1. Register a user (defaults to role USER)
curl -s -X POST localhost:8080/api/auth/register -H "Content-Type: application/json" \
  -d '{"username":"alice","email":"alice@example.com","password":"password1"}'

# 2. Log in -> get a JWT
TOKEN=$(curl -s -X POST localhost:8080/api/auth/login -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"password1"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 3. Browse products (public)
curl -s localhost:8080/api/products

# 4. Placing an order requires a product to exist first -- creating one requires ADMIN.
#    Promote alice to ADMIN via the H2 console (see below), log in again for a fresh
#    token, then:
curl -s -X POST localhost:8080/api/products -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"sku":"SKU-1","name":"Widget","description":"a widget","price":19.99,"stockQuantity":5}'

# 5. Place an order (any authenticated user)
curl -s -X POST localhost:8080/api/orders -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"items":[{"productId":1,"quantity":2}]}'

# 6. Walk the order through its state machine
curl -s -X POST localhost:8080/api/orders/1/pay      -H "Authorization: Bearer $TOKEN"
curl -s -X POST localhost:8080/api/orders/1/ship      -H "Authorization: Bearer $TOKEN"   # ADMIN only
curl -s -X POST localhost:8080/api/orders/1/deliver   -H "Authorization: Bearer $TOKEN"   # ADMIN only
# or, from PENDING/PAID instead of the above:
curl -s -X POST localhost:8080/api/orders/1/cancel    -H "Authorization: Bearer $TOKEN"
```

There is no seed/admin-promotion endpoint on purpose (mirrors a realistic
app). To make a test user ADMIN, use the H2 console at
`http://localhost:8080/h2-console` (JDBC URL `jdbc:h2:mem:dummydb`, user
`sa`, empty password):

```sql
UPDATE USERS SET ROLE = 'ADMIN' WHERE USERNAME = 'alice';
```

Then log in again -- the new JWT will carry the ADMIN role claim.

## Business rules deliberately planted for the Phase 3a exercise

These are the kind of rules the migration plan's Phase 3a toolkit is
designed to surface -- worth deliberately hunting for rather than assuming
a straight read of the Controller/Service layer catches everything:

- **`Product.isLowStock()`** -- logic living in an entity getter, not the
  service layer.
- **`app.products.low-stock-threshold`** in `application.yml` -- a business
  rule driven by configuration, not a hardcoded constant.
- **`Order.transitionTo(...)`** -- the full order status state machine
  (PENDING -> PAID -> SHIPPED -> DELIVERED, with CANCELLED reachable from
  PENDING or PAID only) lives on the entity, not in `OrderService`.
- **Cancelling an order restocks items whether it was PENDING or PAID** --
  because stock is reserved at order-creation time, before any payment
  step exists. Easy to assume restocking only applies to paid orders; it
  doesn't.
- **New registrations always get role `USER`** -- role is never
  client-suppliable at signup (see `UserService.register`).
- **Order ownership check** -- a non-admin can only see/pay/cancel their
  own orders (`OrderService.assertOwnerOrAdmin`), enforced in the service,
  not just via URL structure.
- **`@ValidPassword`** -- a custom Bean Validation annotation
  (min 8 chars + at least one digit), easy to miss if you only skim DTOs
  for `@NotBlank`/`@Size`.
- **Unmapped `AccessDeniedException`** -- there's no explicit
  `@ExceptionHandler` for it; Spring Security's filter chain maps it to
  403 automatically. A rule that isn't in `GlobalExceptionHandler` at all
  is still a rule the .NET rewrite needs to reproduce.
- **`OrderItem.unitPriceAtPurchase`** -- price is captured at order time
  so later `Product` price changes don't retroactively change historical
  order totals.

## Note on this build

This project was generated in a sandbox that could not run a live Maven
build/test cycle to verify compilation end to end. Please run `mvn test`
and `mvn spring-boot:run` locally and report back anything that doesn't
compile or behave as described -- happy to fix it.
