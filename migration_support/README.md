# Migration Tracker — How This Works

## Files in this folder

- **`migration-tracker.csv`** — the file you actually update. One row per **slice**
  (the migration unit — see Phase 2b of the plan: related endpoints sharing a
  Controller/Service/Repository move together, not one by one). This is where
  you pick what's next and mark status.
- **`api-inventory.csv`** — read-only reference. One row per individual API
  endpoint (14 total), grouped by slice, pulled directly from the
  `dummy-api-contract.txt` OpenAPI export. Use it to see exactly which
  endpoints are inside a slice before selecting it.
- **`dummy-api-contract.txt`** — the raw OpenAPI contract you exported
  (Phase 2a), source of truth for the inventory above.
- **`specs/`** — will hold one behavior-spec document per slice, created as
  we migrate each one (doesn't exist yet — created when the first spec is written).

## Migration order (`02` — dependency-based, already applied in the tracker)

1. **Auth/Users** — foundational, no dependencies. Migrate first.
2. **Products** — no dependencies on other slices. Can go second (or in
   parallel with Auth/Users if you wanted, but doing it sequentially keeps
   review load manageable).
3. **Orders** — depends on both Auth/Users and Products, since it calls into
   user identity and product stock. Must go last.

## Status values (use these exact strings in the Status column)

| Status | Meaning |
|---|---|
| `Not Started` | Nothing done yet |
| `Spec In Progress` | I'm drafting the Phase 3a behavior spec |
| `Spec Ready for Review` | Spec drafted, waiting on your manual validation |
| `Rework Needed` | You reviewed it and found gaps/errors — back to drafting |
| `Spec Validated` | You've confirmed the spec is correct — this is as far as we're going today |
| `Code In Progress` | (future) .NET implementation underway |
| `Code Complete` | (future) implementation + tests done |
| `Cutover Done` | (future) traffic shifted for this slice |

Right now we're stopping at **`Spec Validated`** for each slice — no code
generation yet, per your instruction that we're doing spec creation only.

## The workflow, step by step

1. You tell me which slice to work on (or just say "next" and I'll take the
   one at the top of `migration-tracker.csv` that's still `Not Started`).
2. I set that row's Status to `Spec In Progress`, read the relevant Java
   source directly from `java-api/`, and write the behavior spec to
   `specs/<order>-<slice-name>-spec.md` (`03_01` — create slice behaviour doc,
   per `Java-to-DotNetCore-Migration-Plan.md` Section 4).
3. I set Status to `Spec Ready for Review` and post the spec here in chat
   for you to read.
4. You do the manual validation. If something's wrong, tell me what and I
   set Status back to `Rework Needed`, fix it, and resubmit.
5. Once you confirm it's correct, I set Status to `Spec Validated` and stop.
6. You tell me to move to the next slice, and we repeat from step 1.

## Note on file format

This is CSV, not a real `.xlsx`, because the sandbox that runs the Python
tooling needed to build a formatted Excel file (dropdowns, conditional
formatting) wasn't available this session. CSV opens directly in Excel and
you can edit/filter/sort it normally — if you'd rather have a proper `.xlsx`
with data-validation dropdowns on the Status column, say so and I'll build
one once the sandbox is back, using this CSV as the source data.
