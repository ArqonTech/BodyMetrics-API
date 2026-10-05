# Athletes CRUD ADR

## Problem
- The main aggregate is `Athlete`, and it depends on `Sport` rules plus time-series physical assessments.
- Athlete creation also needs profile photo storage without writing raw image bytes to MongoDB.
- Physical assessments need nullable anthropometric fields but still need ordering in time.
- Every athlete and physical assessment must belong to the authenticated user extracted from the token.
- Athlete listings now need pagination and filters without leaking data across users.

## Decision
- Store athletes in MongoDB with embedded `PhysicalAssessment` documents.
- Require each assessment to carry an `AssessmentDate` so the list can behave as a time series.
- Upload profile photos through an `IAthletePhotoStorage` abstraction backed by Azure Blob Storage.
- Persist only photo metadata and blob path, then generate access URLs on reads.
- Keep each athlete slice with one artifact per file (command/query, handler, validator, and contract records separated).
- In `AthletesController`, resolve handlers per action with `[FromServices]` instead of constructor-wide injection.
- Persist `OwnerUserId` on the aggregate and scope every read, update, and delete to that owner.
- Expose paged athlete listings with filters for `FullName`, `SportId`, `Sector`, `Category`, and `Phase`.
- Keep listing behavior aligned with groups: without `groupId`, `GET /api/athletes` includes grouped and ungrouped athletes by default (`includeGrouped=false` keeps only ungrouped).
- Treat `FullName` filtering in listings as autocomplete-style partial search, matching the beginning of the full name or any subsequent name token.
- `Skinfolds` include `IliacCrestMm` (crista ilíaca) and `SupraspinaleMm` (supraespinal) alongside the legacy `SuprailiacMm`.
- Treat all physical assessment measurements (`GeneralMeasurements`, `Skinfolds`, and `Circumferences`) as optional nullable fields; validate `> 0` only when a value is explicitly provided.

## Consequences
- Athlete reads stay simple because assessments are loaded with the aggregate.
- Blob storage can be replaced in tests while production keeps Azure Blob semantics.
- Sector and category validation must check the referenced sport before saving athletes.
- A valid token is mandatory for every athlete endpoint and cross-user access returns `404`.
- List responses now return metadata (`Page`, `PageSize`, `TotalCount`, `TotalPages`) for clients.
- Search bars can query `/api/athletes` with short partial names like `An` and still retrieve `Andre`, `Andress`, or `Antonio`.

