# Athletes Spreadsheet Import ADR

## Problem
- External teams send athlete updates as `.xlsx` files instead of calling the CRUD endpoints row by row.
- Each spreadsheet row mixes sport metadata, athlete identity data, and one physical assessment snapshot.
- Re-importing the same athlete must behave as an upsert, not as duplicate creation.
- The domain currently stores `Sector` and `Category`, but it does not yet persist the spreadsheet `Posição` column.

## Decision
- Add a dedicated authenticated `multipart/form-data` route under `Athletes` that receives `SportName` plus the uploaded `.xlsx` file.
- Resolve spreadsheet columns by normalized header names so accents, casing, spacing, and punctuation do not break imports.
- Create the sport when `SportName` does not exist yet, and enrich existing sports with any new `Sector` or `Category` values found in the file.
- Scope athlete upserts by authenticated owner plus `FullName`, then merge imported assessments by `AssessmentDate` so same-date rows replace the stored snapshot.
- Keep `Posição` as part of the required spreadsheet contract for now, but ignore it during persistence until the domain exposes a matching field.
- Skinfold columns `Crist. ilíaca` (`IliacCrestMm`) and `Sup. Espin` (`SupraspinaleMm`) are optional. `Supra. lli` is no longer read; the stored `SuprailiacMm` field is kept only for legacy data.
- Fail fast with validation details when required headers are missing or row values cannot be parsed into the existing enums and measurements.

## Consequences
- Clients can safely resend the same workbook without manually cleaning previous data.
- Sport catalogs stay synchronized with spreadsheet options instead of rejecting new categories or sectors.
- Import parsing remains deterministic because the route validates the header contract before changing MongoDB.
- Athlete ownership rules continue to apply, so one user cannot upsert another user's records.
- If `Posição` must become searchable later, the domain will need a follow-up slice and migration strategy.

