# Database setup – Users table and migrations

## Confirmation: Users table is created by the backend

The **Users** table (and all other tables) are defined in the initial migration and are created automatically when the API starts.

### Where it’s defined

- **Migration:** `StudioTechBI.Infrastructure/Migrations/20240101000000_InitialCreate.cs`
- **Users table** (around lines 84–106): `Id`, `Email`, `PasswordHash`, `FirstName`, `LastName`, `PhoneNumber`, `IsActive`, `LastLoginAt`, `RefreshToken`, `RefreshTokenExpiryTime`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`
- **Related:** `Roles`, `UserRoles`, `Companies`, `Organizations`, etc. are in the same migration.

### When it’s created

- On every API startup, `Program.cs` runs **`await db.Database.MigrateAsync()`**.
- That applies pending migrations, which:
  - Creates the SQLite file (e.g. `StudioTechBIDb.db`) if it doesn’t exist.
  - Creates all tables, including **Users**, if they don’t exist.
  - Updates the schema if you add new migrations later.

So the **Users table is created in the backend on first run** (and kept up to date on later runs).

### How to confirm

1. **Development**
   - Set `ASPNETCORE_ENVIRONMENT=Development` (or use the Development launch profile).
   - Ensure `appsettings.Development.json` has:
     - `"DefaultConnection": "Data Source=StudioTechBIDb.db"`
   - Run the API from `StudioTechBI.API`:
     - `dotnet run`
   - In the console you should see:
     - `Database migrations applied. Tables (Users, Roles, UserRoles, etc.) are ready.`
     - `Roles seeded (Admin, Accountant, Client).`
   - Then call `POST /api/Auth/register`; it should succeed (no “no such table: Users”).

2. **Optional: inspect SQLite**
   - After a successful run, the DB file is under the API project (or `bin\Debug\net8.0` if using the default content root).
   - Open `StudioTechBIDb.db` with a SQLite tool and run: `SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;`  
     You should see `Users`, `Roles`, `UserRoles`, etc.

### If the build fails

- If you see “Access denied” or file lock errors, close other instances of the API/IDE and try again.
- If you see “no such table: Users” at runtime, delete any existing `StudioTechBIDb.db`, then run the API again so migrations run on a fresh database.
