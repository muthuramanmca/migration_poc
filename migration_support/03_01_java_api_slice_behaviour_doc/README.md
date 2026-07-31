# 03_01 — Java API Slice Behaviour Docs

One behavior-spec markdown file per slice, named `<order>-<slice-name>-spec.md`
(e.g. `01-identity-spec.md`), created as each slice is drafted.

This is the **only** copy of a slice's behavior doc — draft and approved specs
live in the same file here, not in separate folders. Approval status is
tracked exclusively in `../migration-tracker.csv`'s `Status` column
(`Spec Ready for Review` → `Spec Validated`), so there's a single source of
truth for "is this approved yet" instead of two copies that can drift out of
sync. See `../README.md` for the full workflow.
