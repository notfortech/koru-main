# StudioTechBI Domain Entities

## Overview

This document describes all domain entities in the StudioTechBI accounting intelligence platform, their properties, relationships, and database schema.

## Entity Hierarchy

All entities inherit from `BaseEntity` which provides:
- `Id` (Guid): Primary key
- `CreatedAt` (DateTime): Creation timestamp
- `UpdatedAt` (DateTime?): Last modification timestamp
- `CreatedBy` (string?): User who created the entity
- `UpdatedBy` (string?): User who last modified the entity
- `IsDeleted` (bool): Soft delete flag

## Core Accounting Entities

### User

**Purpose**: Represents system users with authentication credentials.

**Properties**:
```csharp
public string Email { get; set; }              // Unique email address
public string PasswordHash { get; set; }        // BCrypt hashed password
public string FirstName { get; set; }           // User first name
public string LastName { get; set; }            // User last name
public string? PhoneNumber { get; set; }        // Optional phone number
public bool IsActive { get; set; }              // Account status
public DateTime? LastLoginAt { get; set; }      // Last login timestamp
public string? RefreshToken { get; set; }       // JWT refresh token
public DateTime? RefreshTokenExpiryTime { get; set; }  // Refresh token expiry
```

**Relationships**:
- HasMany: `UserRoles` (1 User -> M UserRole)
- HasMany: `CompanyUsers` (1 User -> M CompanyUser)

**Database Constraints**:
- Email: UNIQUE, NOT NULL, Max 256 chars
- PasswordHash: NOT NULL, Max 500 chars
- FirstName, LastName: NOT NULL, Max 100 chars each
- PhoneNumber: Max 20 chars

**Indexes**:
- IX_Users_Email (unique)

---

### Company

**Purpose**: Represents client organizations in the system.

**Properties**:
```csharp
public string Name { get; set; }                    // Company name
public string? ABN { get; set; }                    // Australian Business Number
public string? Industry { get; set; }               // Industry classification
public string? Country { get; set; }                // ISO country code (2 chars)
public bool BankIntegrationEnabled { get; set; }    // Bank integration status
```

**Relationships**:
- HasMany: `CompanyUsers` (1 Company -> M CompanyUser)
- HasMany: `BankConnections` (1 Company -> M BankConnection)
- HasMany: `BankTransactions` (1 Company -> M BankTransaction)

**Database Constraints**:
- Name: NOT NULL, Max 256 chars
- ABN: Max 50 chars
- Industry: Max 100 chars
- Country: Max 2 chars (ISO standard)

**Notes**:
- BankIntegrationEnabled defaults to false
- Deletion cascades to CompanyUsers and BankConnections

---

### CompanyUser

**Purpose**: Junction entity linking Users to Companies (many-to-many relationship).

**Properties**:
```csharp
public Guid CompanyId { get; set; }    // Foreign key to Company
public Guid UserId { get; set; }       // Foreign key to User
public int Role { get; set; }          // Role enum (Admin=1, Accountant=2, Client=3)
```

**Relationships**:
- BelongsTo: `Company` (M CompanyUser -> 1 Company)
- BelongsTo: `User` (M CompanyUser -> 1 User)

**Database Constraints**:
- CompanyId, UserId: NOT NULL, Foreign Keys
- Role: NOT NULL, enum value (1-3)
- Unique constraint on (CompanyId, UserId)

**Indexes**:
- IX_CompanyUsers_CompanyId_UserId (unique)
- IX_CompanyUsers_UserId

**Cascade Rules**:
- Delete Company -> Delete all CompanyUsers
- Delete User -> Delete all CompanyUsers

**Role Values**:
```csharp
public enum UserRoleType
{
    Admin = 1,         // Full company access
    Accountant = 2,    // Can manage transactions
    Client = 3         // Read-only access
}
```

---

## Bank Integration Entities

### BankConnection

**Purpose**: Represents integrated bank accounts connected to a Company.

**Properties**:
```csharp
public Guid CompanyId { get; set; }           // Foreign key to Company
public string ProviderName { get; set; }      // Bank provider (e.g., "Westpac", "ANZ")
public string ConnectionId { get; set; }      // External provider connection ID
public int Status { get; set; }               // Status enum (Active, Inactive, etc.)
public DateTime? LastSyncDate { get; set; }   // Last successful sync timestamp
```

**Relationships**:
- BelongsTo: `Company` (M BankConnection -> 1 Company)
- HasMany: `BankTransactions` (1 BankConnection -> M BankTransaction)

**Database Constraints**:
- CompanyId: NOT NULL, Foreign Key
- ProviderName: NOT NULL, Max 100 chars
- ConnectionId: NOT NULL, Max 256 chars (unique per provider)
- Status: NOT NULL, enum value (1-4)

**Indexes**:
- IX_BankConnections_CompanyId_ProviderName

**Cascade Rules**:
- Delete Company -> Delete BankConnection
- Delete BankConnection -> Set NULL on related BankTransactions

**Status Values**:
```csharp
public enum BankConnectionStatus
{
    Active = 1,           // Connection working
    Inactive = 2,         // Temporarily disabled
    Disconnected = 3,     // Disconnected by user
    Error = 4             // Connection error
}
```

---

### BankTransaction

**Purpose**: Represents individual bank transactions from connected accounts.

**Properties**:
```csharp
public Guid CompanyId { get; set; }           // Foreign key to Company
public Guid? BankConnectionId { get; set; }   // Optional FK to BankConnection
public decimal Amount { get; set; }           // Transaction amount (precision 19,2)
public string Description { get; set; }       // Transaction description
public DateTime TransactionDate { get; set; } // Transaction date
```

**Relationships**:
- BelongsTo: `Company` (M BankTransaction -> 1 Company)
- BelongsTo: `BankConnection` (M BankTransaction -> 1 BankConnection, optional)

**Database Constraints**:
- CompanyId: NOT NULL, Foreign Key
- BankConnectionId: Optional Foreign Key
- Amount: NOT NULL, Decimal(19,2)
- Description: NOT NULL, Max 512 chars
- TransactionDate: NOT NULL, DateTime

**Indexes**:
- IX_BankTransactions_CompanyId_TransactionDate (for date range queries)
- IX_BankTransactions_BankConnectionId

**Cascade Rules**:
- Delete Company -> Delete BankTransaction
- Delete BankConnection -> Set NULL on BankTransactionId

**Notes**:
- Amount supports international currencies with 2 decimal places
- BankConnectionId is optional for manual transactions
- TransactionDate indexed for efficient date range queries

---

## Authorization Entities

### Role

**Purpose**: Represents system roles for role-based access control.

**Properties**:
```csharp
public string Name { get; set; }  // Role name (e.g., "Admin", "AccountManager")
```

**Relationships**:
- HasMany: `UserRoles` (1 Role -> M UserRole)
- HasMany: `RolePermissions` (1 Role -> M RolePermission)

**Database Constraints**:
- Name: NOT NULL

---

### Permission

**Purpose**: Represents granular system permissions.

**Properties**:
```csharp
public string Name { get; set; }  // Permission name (e.g., "CreateTransaction")
```

**Relationships**:
- HasMany: `RolePermissions` (1 Permission -> M RolePermission)

**Database Constraints**:
- Name: NOT NULL

---

### UserRole

**Purpose**: Junction entity linking Users to Roles (many-to-many relationship).

**Properties**:
```csharp
public Guid UserId { get; set; }    // Foreign key to User
public Guid RoleId { get; set; }    // Foreign key to Role
```

**Relationships**:
- BelongsTo: `User` (M UserRole -> 1 User)
- BelongsTo: `Role` (M UserRole -> 1 Role)

**Cascade Rules**:
- Delete User -> Delete all UserRoles
- Delete Role -> Delete all UserRoles

---

### RolePermission

**Purpose**: Junction entity linking Roles to Permissions (many-to-many relationship).

**Properties**:
```csharp
public Guid RoleId { get; set; }          // Foreign key to Role
public Guid PermissionId { get; set; }    // Foreign key to Permission
```

**Relationships**:
- BelongsTo: `Role` (M RolePermission -> 1 Role)
- BelongsTo: `Permission` (M RolePermission -> 1 Permission)

**Cascade Rules**:
- Delete Role -> Delete all RolePermissions
- Delete Permission -> Delete all RolePermissions

---

## Multi-Tenant Support Entity

### Organization

**Purpose**: Represents multi-tenant organizations (future expansion).

**Properties**:
```csharp
public string Name { get; set; }  // Organization name
```

**Relationships**:
- Available for future implementation

**Current Status**: Structure in place, not yet integrated with Company model.

**Future Enhancement**: Link Companies to Organizations for true multi-tenancy.

---

## Entity Relationships Diagram

```
┌─────────────┐
│    User     │
├─────────────┤
│ Id (PK)     │
│ Email (UQ)  │
│ FirstName   │
│ LastName    │
│ PhoneNumber │
│ IsActive    │
│ RefreshToken│
└──────┬──────┘
       │
       ├──────────────────────┬──────────────────────┐
       │ (1:M via UserRole)   │ (1:M via CompanyUser)│
       │                      │                      │
       ▼                      ▼                      ▼
  ┌─────────────┐         ┌──────────────┐    ┌─────────────┐
  │  UserRole   │         │ CompanyUser  │    │   Company   │
  ├─────────────┤         ├──────────────┤    ├─────────────┤
  │ UserId (FK) │         │ CompanyId(FK)│    │ Id (PK)     │
  │ RoleId (FK) │         │ UserId (FK)  │    │ Name        │
  └─────────────┘         │ Role (Enum)  │    │ ABN         │
       │                  └──────┬───────┘    │ Industry    │
       │                         │            │ Country     │
       │                         │            │ BankInt.En. │
       │                  (M:1 to Company)    └──────┬──────┘
       │                                             │
  ┌────▼──────────┐                                 ├────────────┬─────────────┐
  │    Role       │                                 │(1:M)       │(1:M)        │
  ├───────────────┤                         ┌───────────────────┐    ┌────────────────┐
  │ Id (PK)       │                         │ BankConnection    │    │ BankTransaction│
  │ Name          │                         ├───────────────────┤    ├────────────────┤
  └───────────────┘                         │ CompanyId (FK)    │    │ CompanyId (FK) │
       │                                    │ ProviderName      │    │ BankConnId(FK?)│
       │(1:M via RolePermission)            │ ConnectionId      │    │ Amount         │
       │                                    │ Status            │    │ Description    │
       ▼                                    │ LastSyncDate      │    │ TransDate      │
  ┌─────────────┐                          └────┬──────────────┘    └────────────────┘
  │ RolePermission
  ├─────────────┤                               │
  │ RoleId (FK) │                               │
  │ PermId (FK) │                               │(M:1 to BankConnection)
  └─────────────┘
       │
  ┌────▼──────────┐
  │ Permission    │
  ├───────────────┤
  │ Id (PK)       │
  │ Name          │
  └───────────────┘
```

---

## Data Flow Examples

### User Logs into Company

1. **User** authenticates with email/password
2. System queries **UserRole** to get system roles
3. System queries **CompanyUser** to get all Companies user is associated with
4. System queries **CompanyUser.Role** to get user's role within Company
5. System queries **RolePermission** to get permissions for that role

### Transaction Sync Flow

1. **BankConnection** triggers sync for Company
2. New **BankTransactions** created with CompanyId and BankConnectionId
3. **Company.BankIntegrationEnabled** checked to ensure sync is enabled
4. **BankConnection.LastSyncDate** updated on success
5. **BankConnection.Status** updated based on sync result

---

## Database Design Decisions

### Soft Delete

- All entities include `IsDeleted` flag
- Deleted records remain in database for audit purposes
- Queries should filter out soft-deleted records
- Implement custom DbContext logic to automatically apply filter

### Audit Fields

- `CreatedAt`, `CreatedBy`: Set on entity creation
- `UpdatedAt`, `UpdatedBy`: Updated on modification
- Automatically maintained by `ApplicationDbContext.SaveChangesAsync()`

### Cascading Deletes

- **Company deletion** cascades to CompanyUsers and BankConnections
- **User deletion** cascades to CompanyUsers (removes from all companies)
- **BankConnection deletion** sets NULL on BankTransactions (preserves history)
- **Role/Permission deletion** cascades through junction tables

### Indexes

- **Email** on Users (unique, for login)
- **CompanyId, UserId** on CompanyUsers (unique, prevents duplicates)
- **CompanyId, ProviderName** on BankConnections (efficient provider lookup)
- **CompanyId, TransactionDate** on BankTransactions (date range queries)

### Foreign Key Constraints

- All relationships use Guid foreign keys
- Referential integrity enforced at database level
- Cascade rules prevent orphaned records

---

## Future Extensions

### Additional Entities

1. **Chart of Accounts**: GL account structure
2. **JournalEntry**: GL transactions
3. **BankReconciliation**: Monthly reconciliation records
4. **DocumentAttachment**: Supporting documents
5. **AuditLog**: Detailed change tracking
6. **Notification**: User notifications
7. **Report**: Saved reports and templates
8. **Team**: Sub-groups within Company

### Enhancements

1. Partition **BankTransaction** by date for large datasets
2. Add **BankingProvider** lookup table
3. Implement **DataClassification** for regulatory compliance
4. Add **ComplianceCheckpoint** for audit trail

---

## Migration Status

### Current Migration: InitialCreate

**Created Tables**:
- Users
- Companies
- CompanyUsers
- BankConnections
- BankTransactions
- Roles
- Permissions
- UserRoles
- RolePermissions
- Organizations

**Status**: Ready for application

**Next Steps**:
1. Seed initial roles and permissions
2. Create indexes for performance
3. Add stored procedures if needed
4. Implement soft delete filter in DbContext

---

## Accessing Entities in Code

### From DbContext

```csharp
var users = await _context.Users.ToListAsync();
var companies = await _context.Companies.Where(c => !c.IsDeleted).ToListAsync();
var transactions = await _context.BankTransactions
    .Include(bt => bt.BankConnection)
    .Include(bt => bt.Company)
    .Where(bt => bt.TransactionDate >= startDate)
    .ToListAsync();
```

### Using Repository Pattern

```csharp
var user = await _userRepository.GetByIdAsync(userId);
var companyUsers = await _companyUserRepository.GetAllAsync();
var userCompanies = companyUsers
    .Where(cu => cu.UserId == userId)
    .Select(cu => cu.Company)
    .ToList();
```

---

## Best Practices

1. **Always filter soft-deleted records** in queries
2. **Use async/await** for all database operations
3. **Include related entities** only when needed
4. **Use indexes** for frequently filtered columns
5. **Validate foreign keys** before assignment
6. **Keep CreatedBy/UpdatedBy** populated for audit trail
7. **Use transactions** for multi-entity operations
8. **Test cascade behaviors** carefully

---

## Performance Considerations

### Query Optimization

- Use `Select()` to fetch only needed columns
- `AsNoTracking()` for read-only queries
- Index on CompanyId + TransactionDate for range queries
- Partition BankTransactions by company for large datasets

### Connection Management

- Use connection pooling
- Close connections immediately after use
- Implement query timeouts for long operations

### Caching Strategy

- Cache user permissions after login
- Cache company settings
- Invalidate cache on entity updates

