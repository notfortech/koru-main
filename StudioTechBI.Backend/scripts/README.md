# scripts/

## `setup-db.sh` — provision a fresh test database

Gets a tester from "nothing" to "database migrated, bootstrapped, seeded,
API running, ready to log in and test" in one command.

```bash
./setup-db.sh
```

By default this:
1. Starts a throwaway SQL Server 2022 container in Docker (reused on repeat
   runs — safe to re-run).
2. Runs `dotnet tool restore` + `dotnet ef database update` to apply the real
   EF Core migration files.
3. Starts the API (`StudioTechBI.API`) against that database and waits for it
   to report the database is fully ready — which is also what triggers
   `RoleSeeder`, `AdminUserSeeder`, and, critically,
   `HandwrittenMigrationsBootstrapper.EnsureTablesExistAsync` (see
   `../MIGRATIONS.md`): several current tables/columns are **not** reliably
   created by `dotnet ef database update` alone, only by that bootstrapper,
   which only runs as part of the API's own startup sequence. There is no
   separate CLI for it — actually starting the API is the only way to get a
   fully correct schema today.
4. Prints the connection string, seeded admin login, API URL, and (if it
   created one) the Docker teardown command.

### Options

| Flag | Purpose |
|---|---|
| `--connection-string "<str>"` | Use an existing SQL Server instead of spinning up Docker |
| `--no-docker` | Refuse to fall back to Docker (fails fast if no connection string given) |
| `--keep-running` | Leave the API running after setup instead of stopping it |
| `--port <n>` | API port (default `5099`, chosen to avoid clashing with a tester's own `dotnet run` on 5000/5001) |
| `--timeout <secs>` | How long to wait for "database ready" before giving up (default `300`, covers first-time build) |

### Requirements

- .NET 8 SDK
- Docker (default path) **or** a reachable SQL Server + `--connection-string`

### Re-running

Safe. Migrations no-op when current, the bootstrapper's DDL is guarded
(`IF OBJECT_ID(...) IS NULL`), and the seeders skip existing rows.

### Overriding the seeded admin account

Set before running: `SEED_ADMIN_NAME`, `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`
(defaults: `QA Admin` / `admin@studiotechbi.local` / `QaAdmin!2026`).
