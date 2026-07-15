# EF Core Migrations — Deployment & Known Follow-Ups

## How migrations get applied

There is no separate "run migrations" deploy step. `StartupDbTasksHostedService`
(`StudioTechBI.API/Services/StartupDbTasksHostedService.cs`) runs as a background
service every time the app starts, and calls `db.Database.MigrateAsync()` before
marking the app ready to serve `/api` traffic. Seed data (`RoleSeeder`,
`AdminUserSeeder`, `SchemaModelSeeder`, ...) runs immediately after, in the same
startup pass.

Deploy triggers this automatically: `.github/workflows/main_studiotechbi-api.yml`
deploys to the `StudioTechbi-api` Azure Web App on every push to `main` — no manual
gate, no staging step. So the real sequence is: **merge to `main` → GitHub Actions
deploys → app restarts → migrations + seeding run automatically against production
Azure SQL.** No one needs to run SQL by hand for a normal migration.

This only applies when `UseDemoStorage` is `false` (the production setting — see
`appsettings.json`). When `true`, the app seeds demo data instead and does not run
the SQL Server migration path at all.

## Before merging a migration-carrying PR

1. **Confirm the app's DB connection has DDL rights** (CREATE TABLE / ALTER TABLE /
   CREATE INDEX), not just DML. If the Azure SQL connection is a locked-down
   app-only user, migrations fail — and `StartupDbTasksHostedService` deliberately
   does **not** crash the app when that happens (the failure is caught and logged as
   non-fatal), so it fails silently unless someone checks logs.
2. **There is no staging environment in this pipeline.** Merging to `main` deploys
   straight to production. Sanity-check before merging, not after.

## After merging a migration-carrying PR

Check the App Service log stream for lines from `StartupDbTasksHostedService`:

- Success: `"Migrations applied. Seeding roles/admin..."` → `"Database ready
  (migrations + seed complete)."`
- Failure: `"DB init failed after retries..."` or `"Startup DB tasks failed
  (non-fatal)"`

Fastest no-DB-access sanity check for the SchemaModel library specifically: hit
`GET /api/schema-models` once deployed. If it returns the seeded reference models,
both the migration and the seeder ran successfully.

## Hand-authored migrations (no EF CLI tooling was available when these were written)

- `20260712120000_AddReportDesignerConsent.cs`
- `20260712130000_AddSchemaModelLibrary.cs`
- `20260715020000_AddSchemaModelFieldAliases.cs` — adds the column-alias learning
  table. Same posture as the two above: written without `dotnet ef migrations add`
  (still no EF CLI available), documentation/parity only —
  `HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync` is what actually creates
  `SchemaModelFieldAliases` in production. Not yet run against a real deploy at the
  time this was written; verify via the same `StartupDbTasksHostedService` log check
  above after the next deploy, and spot-check with `GET /api/admin/schema-model-field-aliases/pending`
  (should return `200` with an empty list, not a SQL error, once the table exists).

Both were written without `dotnet ef migrations add` — there was no `dotnet`/EF CLI
available in the environment they were authored in, so they couldn't be generated or
test-applied against a real SQL Server. They're written defensively (guarded with
`IF OBJECT_ID(...) IS NULL`, and the new `Templates.ModelId` foreign key uses `WITH
NOCHECK` so it won't fail if pre-existing `ModelId` values don't resolve to a real
`SchemaModel` row), and neither has a `*.Designer.cs` partial — instead the
`[Migration(...)]` attribute is declared directly on the migration class.

**This was tried in production on 2026-07-13 and did not work as expected.**
`Database.MigrateAsync()` logged `"Migrations applied."` on every retry, but the very
next query against `SchemaModels` failed with `Invalid object name 'SchemaModels'` —
i.e. EF reported success while never actually creating the table. Root cause was not
confirmed (no EF CLI available to reproduce/debug it properly); missing `[DbContext]`
on the migration class is the leading theory and has since been added to both files,
but that fix has **not** been re-verified against real tooling either — treat it as a
plausible improvement, not a confirmed fix.

**Consequence that made this more than a minor bug:** `SchemaModelSeeder.SeedAsync`
ran immediately after the (silently no-op) migration step, inside the same retry loop
that gates `_readiness.MarkDatabaseReady()`. When it hit the missing table, the
exception propagated out of the retry loop entirely after 5 attempts, so
`MarkDatabaseReady()` was never called — which took down **all** `/api/*` traffic,
including login, since those routes are gated on that readiness flag. A reference-data
seeding failure should never have been able to do that.

**Fix applied (same day):**
1. `HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync`
   (`StudioTechBI.Infrastructure/Data/HandwrittenMigrationsBootstrapper.cs`) runs the
   exact same guarded DDL directly via `ExecuteSqlRawAsync`, independent of whether EF
   ever recognizes the two migrations as pending. This is now the real source of truth
   for whether `ReportDesignerConsents`, `SchemaModels`, `SchemaModelFields`, and the
   `Templates.ModelId` FK actually exist — not the migration files themselves.
2. `StartupDbTasksHostedService.SeedSchemaModelsNonFatalAsync` wraps
   `SchemaModelSeeder.SeedAsync` in its own try/catch, structurally separate from the
   critical path (connect → migrate → `RoleSeeder` → `AdminUserSeeder`) that gates
   readiness. A schema-model seeding failure now logs a warning and lets the app
   become ready anyway — it can't take down login again, regardless of cause.

## Incident 2 (same day, 2026-07-13): SchemaModel seeding failed for a different reason

Once the fix above shipped, `SchemaModels`/`SchemaModelFields` were created correctly
and `Templates.ModelId` got a second FK pointing at `SchemaModels` as designed — but
seeding still failed, this time with:

```
The INSERT statement conflicted with the FOREIGN KEY constraint "FK_Templates_Models".
The conflict occurred in database "studiotechbi-prod-db", table "dbo.Models", column
'ModelId'.
```

**There is a pre-existing `dbo.Models` table and `FK_Templates_Models` constraint
already in production, on the same `Templates.ModelId` column — and neither is mapped
anywhere in this codebase.** Nothing in the EF model, migrations history, or any
`.cs` file references a `dbo.Models` table, so there was no way to see this constraint
from the code alone; it only surfaced once real inserts hit it. `Templates.ModelId`
was already "owned" by whatever created that table (unknown — possibly a different
service sharing this database, a manual DBA script, or an older version of this app
not reflected in current git history). Adding a second FK on that same column meant
every insert had to satisfy both constraints at once, which is impossible for rows
pointing at `SchemaModels`.

**Fix:** stopped reusing `Templates.ModelId` for `SchemaModel` linkage entirely.
`Templates.SchemaModelId` is now a separate, new nullable column with its own FK
(`FK_Templates_SchemaModels_SchemaModelId`) to `SchemaModels`. `Templates.ModelId` and
its relationship to the mystery `dbo.Models` table are left completely untouched — not
reinterpreted, not migrated, not queried by any of this session's code.
`HandwrittenMigrationsBootstrapper` also now drops the mistaken
`FK_Templates_SchemaModels_ModelId` if it's still present from the first attempt.

**Open question this raises:** what is `dbo.Models`, and does `TemplateService.cs`'s
existing `ModelId`/`ParseModelId` admin-CRUD usage (which predates this session and
was left untouched) actually populate or depend on it? Worth a DBA/team check —
nobody working on this had visibility into that table from the codebase alone.

**Still-open follow-up:** `ApplicationDbContextModelSnapshot.cs` has no knowledge of
`ReportDesignerConsents`, `SchemaModels`, `SchemaModelFields`, or (as of this addition)
`SchemaModelFieldAliases`. The next time anyone with the `dotnet` SDK runs
`dotnet ef migrations add <Name>`, EF will diff against that stale snapshot, conclude
those tables don't exist yet, and generate a migration with real
`migrationBuilder.CreateTable(...)` calls for them — which will fail when applied,
since the tables already exist by then (created by the bootstrapper). Whoever hits
this needs to either strip those `CreateTable` calls out of the generated migration
before applying it, or reconcile the snapshot by hand first so the next
`migrations add` diffs cleanly. Left as documentation rather than fixed now — the
snapshot edit is exactly the kind of thing that's low-risk with real EF tooling to
verify against, and higher-risk without it.
