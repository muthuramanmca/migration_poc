# 03_01 — Java API Slice Behaviour Docs

One behavior-spec markdown file per slice, named `<order>-<slice-name>-spec.md`
(e.g. `01-identity-spec.md`), created as each slice is drafted.

This is the **only** copy of a slice's behavior doc — draft and approved specs
live in the same file here, not in separate folders. Approval status is
tracked exclusively in `../migration-tracker.xlsx`'s `Status` column
(`Spec Ready for Review` → `Spec Validated`), so there's a single source of
truth for "is this approved yet" instead of two copies that can drift out of
sync. See `../README.md` for the full workflow.

## Source references section (required in every spec)

Every spec ends with a "Source references" section: tables mapping each
endpoint, DTO, business rule, error condition, and non-functional note back to
its Java file and line range. Paths are given relative to
`java-api/src/main/java/com/example/airlineapi/` (note any exceptions inline,
e.g. `application.yml`). See `01-identity-spec.md` §9 for the established
format.

Keep the references **collected in this one section**, not scattered through
the narrative — the body of the spec stays readable plain language, and line
numbers all live in one place if they ever need re-checking. They exist to
serve the manual approval gate (`03_02`, verifying the spec against the real
code) and the .NET code review (`04_05`, checking the rewrite against the
original).

Line numbers are safe to rely on here because `java-api/` is a frozen PoC
source app, not a codebase under active development. If that ever changes,
re-verify this section before trusting it.
