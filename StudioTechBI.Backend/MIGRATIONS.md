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

Both were written without `dotnet ef migrations add` — there was no `dotnet`/EF CLI
available in the environment they were authored in, so they couldn't be generated or
test-applied against a real SQL Server. They're written defensively (guarded with
`IF OBJECT_ID(...) IS NULL`, and the new `Templates.ModelId` foreign key uses `WITH
NOCHECK` so it won't fail if pre-existing `ModelId` values don't resolve to a real
`SchemaModel` row), and neither has a `*.Designer.cs` partial — the `[Migration(...)]`
attribute is declared directly on the migration class instead, which is sufficient
for `Database.MigrateAsync()` to discover and apply them at runtime. What that
attribute placement does **not** do is keep `ApplicationDbContextModelSnapshot.cs` in
sync — that snapshot still has no knowledge of `ReportDesignerConsents`,
`SchemaModels`, or `SchemaModelFields`.

**Practical effect / open follow-up:** the next time anyone with the `dotnet` SDK
runs `dotnet ef migrations add <Name>`, EF will diff against the stale snapshot,
conclude those three tables don't exist yet, and generate a migration with real
`migrationBuilder.CreateTable(...)` calls for them — which will fail when applied,
since the tables already exist in the database by then. Whoever hits this needs to
either strip the erroneous `CreateTable` calls out of that generated migration before
applying it, or reconcile `ApplicationDbContextModelSnapshot.cs` by hand first so the
next `migrations add` diffs cleanly. This has been left as a documented follow-up
rather than fixed now, since it only matters once EF tooling is available to do it
properly — attempting the snapshot edit by hand carries more risk than leaving it
for whoever has the real tooling.
