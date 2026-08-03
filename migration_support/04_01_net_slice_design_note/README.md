# 04_01 — .NET Slice Design Notes

One design-note markdown file per slice, named `<order>-<slice-name>-design.md`,
written once a slice's behavior doc is `Spec Validated` and before any .NET
code is generated (`04_02`).

Per `Java-to-DotNetCore-Migration-Plan.md` Section 4, a design note is
contract-level only — endpoint route, DTO shapes, service interface
signature, EF Core entity changes — no method bodies. This folder is new:
previously these decisions were only ever discussed in chat, never
persisted. Keeping them as files gives a real audit trail of what was
decided before code existed, alongside the behavior docs.

## Design Patterns section (required in every design note)

Every design note includes a "Design Patterns" section with three parts.
Keep each part short — this is a list with one-line justifications, not an
essay.

1. **Reused from Java** — the structural idioms carried over as-is
   (Controller → Service → Repository layering, DTOs as simple
   request/response records, exception-handler middleware). Pull names from
   `Java-to-DotNetCore-Migration-Plan.md` Section 2's Spring→ASP.NET Core
   mapping table. This list is expected to be short/trivial — `dotnet-api`'s
   service boundaries were deliberately redesigned independently of
   `java-api`'s (see `dotnet-api/docs/adr/0001-microservices-skeleton.md`),
   so don't force this section to look substantial.
   Do **not** re-decide Java behavioral quirks/bugs here (e.g. the
   low-stock-threshold inconsistency, missing booking-ownership checks) —
   those are decided once, in the slice's `03_01` behavior doc. This section
   just cites that decision if it affects the contract shape.

2. **Already mandated project-wide** — patterns fixed for every service in
   `dotnet-api/docs/adr/0001-microservices-skeleton.md` (outbox, saga,
   RS256/JWKS, database-per-service). One line each, cited not re-justified,
   e.g. "publishes `BookingCreated` via the outbox, per ADR 0001." Nothing
   here is a decision point — it's a reminder that the note is aware of the
   constraint.

3. **New for this slice** — a genuine per-slice choice not covered by #1 or
   #2 (e.g. Specification pattern for Booking's rule set, Strategy for fare
   pricing). Max 1–3 candidates, each with a one-line justification tied to
   a concrete need in *this slice's* `03_01` spec — not "best practice" in
   the abstract. This is the only part of the section where the reviewing
   developer is actually being asked to confirm yes/no; skip it entirely for
   simple CRUD-shaped slices where nothing qualifies.

   **This is the manual gate (`04_01b` in the migration plan) — flag it visibly.**
   Bound it with a `[!IMPORTANT]` callout (GitHub-flavored alert syntax) stating
   that `04_02` doesn't start until it's confirmed, and horizontal rules (`---`)
   before/after so it reads as distinct from the rest of the note, which is just
   citing decisions already made upstream. See `01-identity-design.md` §6.3 for
   the pattern to follow.
