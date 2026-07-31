# Migration Tracker — How This Works

**Status: awaiting regeneration.** `java-api/` was rewritten around 5 airline domains (`identity`,
`flight`, `booking`, `notification`, `loyalty`) replacing the old `user`/`product`/`order` domain.
The tracker/inventory/contract/specs described below don't exist yet for this domain — the old
domain's versions have been moved to `archive/` (see `archive/README.md`) as historical reference.
Fresh versions get created here the next time the `02`→`03` pipeline runs (Phase 2a contract export →
Phase 2b slice grouping/ordering → per-slice behavior specs).

## Files/folders in this folder (once regenerated)

- **`migration-tracker.csv`** — the file you actually update. One row per **slice**
  (the migration unit — see Phase 2b of the plan: related endpoints sharing a
  Controller/Service/Repository move together, not one by one). This is where
  you pick what's next and mark status.
- **`java-api-inventory.csv`** — read-only reference. One row per individual API
  endpoint, grouped by slice, pulled directly from the OpenAPI export. Use it
  to see exactly which endpoints are inside a slice before selecting it.
- **`java-api-contract.txt`** — the raw OpenAPI contract you exported
  (Phase 2a), source of truth for the inventory above.
- **`03_01_java_api_slice_behaviour_doc/`** — one behavior-spec document per
  slice (`03_01`). Draft and approved specs live in the **same file** here —
  approval status is tracked only in `migration-tracker.csv`'s `Status`
  column, not by moving files between folders, so there's one source of
  truth instead of two copies that can drift apart.
- **`04_01_net_slice_design_note/`** — one design-note document per slice
  (`04_01`), written once a slice is `Spec Validated` and before any .NET
  code is generated. New addition — previously these decisions only ever
  existed in chat, never persisted.

## Migration order (`02` — dependency-based, to be re-applied for the new domain)

The new domain's dependency graph (per `dotnet-api/docs/adr/0001-microservices-skeleton.md`'s
counterpart design) is a diamond, not a line: `identity` (foundational) → `flight` (no deps) →
`booking` (needs identity+flight) → `loyalty` (needs identity+booking) → `notification` (needs
booking). Confirm this against the real OpenAPI export when regenerating the tracker, rather than
assuming it's exactly right — that's what Phase 2a/2b are for.

## Status values (use these exact strings in the Status column)

| Status | Meaning |
|---|---|
| `Not Started` | Nothing done yet, not queued either |
| `Queued for Drafting` | You've flagged this slice to be drafted in the next batch — see "Batch drafting" below |
| `Spec In Progress` | I'm drafting the Phase 3a behavior spec |
| `Spec Ready for Review` | Spec drafted, waiting on your manual validation |
| `Rework Needed` | You reviewed it and found gaps/errors — back to drafting |
| `Spec Validated` | You've confirmed the spec is correct |
| `Design Note Ready` | (future) `04_01` design note written, awaiting your review |
| `Code In Progress` | (future) .NET implementation underway |
| `Code Complete` | (future) implementation + tests done |
| `Cutover Done` | (future) traffic shifted for this slice |

## The workflow, step by step

1. You tell me which slice(s) to work on — either one at a time ("do identity next"), or by
   marking one or more rows `Queued for Drafting` in `migration-tracker.csv` yourself and telling me
   "draft everything queued" (see "Batch drafting" below).
2. For each slice: I set its Status to `Spec In Progress`, read the relevant Java source directly
   from `java-api/`, and write the behavior spec to
   `03_01_java_api_slice_behaviour_doc/<order>-<slice-name>-spec.md` (`03_01`, per
   `Java-to-DotNetCore-Migration-Plan.md` Section 4).
3. I set Status to `Spec Ready for Review` and post the spec here in chat for you to read.
4. You do the manual validation. If something's wrong, tell me what and I set Status back to
   `Rework Needed`, fix it, and resubmit.
5. Once you confirm it's correct, I set Status to `Spec Validated`.
6. Once you're ready for the .NET side, I write `04_01_net_slice_design_note/<order>-<slice-name>-design.md`
   and we continue into `04_02` (generate code) per the migration plan.

### Batch drafting

Writing a behavior spec requires actually reading and understanding the Java business logic — it's
not a mechanical transform, so this isn't a background script. What it does let you do: mark several
slices `Queued for Drafting` in the CSV yourself (Excel or any editor), then tell me to process the
queue in one go. I'll draft all of them in sequence, setting each to `Spec In Progress` then
`Spec Ready for Review` as I go, and present them together for your review — your manual validation
gate (step 4 above) still applies individually to each one afterward. This only batches the
*drafting* step, not the approval step.

## Note on file format

This is CSV, not `.xlsx` — but a proper Excel file with data-validation dropdowns on the `Status`
column (and anything else useful) is buildable now if you want it; say so and I'll generate one from
this CSV as the source data once it exists.
