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
