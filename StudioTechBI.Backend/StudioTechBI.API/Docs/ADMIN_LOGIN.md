# Admin login – current architecture (as is)

## Overview

- **Endpoint:** `POST /api/admin/login`
- **Body:** `{ "email": "...", "password": "..." }`
- **Admin authentication uses only the AdminUsers table.** Users, Roles, and UserRoles are not used for admin login.

---

## Table: AdminUsers

| Column        | Type     | Notes |
|---------------|----------|--------|
| Id            | Guid     | PK |
| Name          | string   | Display name |
| Email         | string   | Login email (compared case-insensitively) |
| PasswordHash  | string   | BCrypt hash (work factor 11) |
| Role          | int      | 0 = SuperAdmin, 1 = OperationsAdmin, 2 = SupportAdmin (AdminRole enum) |
| IsActive      | bool     | Must be true to login |
| IsDeleted     | bool     | Must be false (BaseEntity) |
| CreatedAt     | DateTime | BaseEntity |
| CreatedDate   | DateTime | AdminUser-specific |

---

## Flow (as is)

1. Request comes to `POST /api/admin/login` with email and password.
2. **AdminAuthService** looks up a row in **AdminUsers** where:
   - `Email` matches request email (case-insensitive),
   - `IsActive == true`,
   - `IsDeleted == false`.
3. Password is verified with `BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash)` (stored hash is trimmed before verify).
4. If valid, a JWT is issued with claims: NameIdentifier, AdminId, Email, Name, Role (admin.Role.ToString()).
5. **PortalAdminPolicy** allows roles: `SuperAdmin`, `OperationsAdmin`, `SupportAdmin` (must match AdminRole enum names).

---

## Default seeded admin

When the application starts, if **AdminUsers** has no rows, **AdminUserSeeder** inserts one:

- **Email:** `admin@studiotechbi.com`
- **Password:** `Admin@123`
- **Role:** SuperAdmin
- **Table:** AdminUsers

Seeding is skipped if `AdminUsers` already has any row.

---

## Creating admin users in the backend

Create rows only in **AdminUsers** (not in Users/UserRoles).

1. **BCrypt hash** (work factor 11, same as seeder):

   ```csharp
   BCrypt.Net.BCrypt.HashPassword("YourPassword", workFactor: 11)
   ```

2. **Insert** (SQL example; adjust columns if your migration differs):

   ```sql
   INSERT INTO AdminUsers (Id, Name, Email, PasswordHash, Role, IsActive, IsDeleted, CreatedAt, CreatedDate)
   VALUES (
     NEWID(),
     'Your Name',
     'admin@yourcompany.com',
     '$2a$11$...',   -- BCrypt hash from step 1
     0,              -- 0=SuperAdmin, 1=OperationsAdmin, 2=SupportAdmin
     1,
     0,
     GETUTCDATE(),
     GETUTCDATE()
   );
   ```

3. **Email:** Stored value is compared case-insensitively; storing lowercase is recommended.
4. **PasswordHash:** Must be valid BCrypt (e.g. starts with `$2a$` or `$2b$`). Code trims the stored value before verify.

---

## Related

- **Admin “me”:** `GET /api/admin/me` (requires PortalAdminPolicy). Uses claim `AdminId` or `NameIdentifier` and loads the same admin from **AdminUsers** by Id.
- **Policies:** `PortalAdminPolicy` requires role `SuperAdmin`, `OperationsAdmin`, or `SupportAdmin` (from AdminRole enum).
