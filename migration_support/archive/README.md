# Archive: pre-airline-rename migration artifacts

These files describe the **generic e-commerce domain** (`user`/`product`/`order`) that `java-api/`
modeled before it was rewritten around 5 airline domains (`identity`/`flight`/`booking`/
`notification`/`loyalty`) for the United Airlines demo. Kept as historical reference — the
business-rule discoveries captured in the 3 specs were real, validated work, just against the old
domain shape.

- `migration-tracker.csv`, `api-inventory.csv`, `dummy-api-contract.txt` — Phase 2/2a outputs for the
  old domain.
- `specs/01-auth-users-spec.md`, `specs/02-products-spec.md`, `specs/03-orders-spec.md` — validated
  Phase 3a behavior specs for the old domain's 3 slices.

New versions of all of these get generated fresh in `migration_support/` (not here) once the `02`→`03`
pipeline restarts against the new 5-domain app.
