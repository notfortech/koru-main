# StudioTechBI Backend

A production-grade .NET 8 SaaS backend for an accounting intelligence platform using Clean Architecture, Entity Framework Core, and SQL Server.

## Quick Start

```bash
# Clone and navigate
cd StudioTechBI.Backend

# Restore packages
dotnet restore

# Update appsettings.json with your database connection
# Then apply migrations
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Run the API
cd StudioTechBI.API
dotnet run
```

**API will be available at**: https://localhost:5001 (Swagger at /swagger)

## Project Structure

```
StudioTechBI.Backend/
├── StudioTechBI.Domain/          # Core business entities and interfaces
├── StudioTechBI.Application/     # Business logic, DTOs, service interfaces
├── StudioTechBI.Infrastructure/  # Data access, EF Core, repositories
└── StudioTechBI.API/             # HTTP endpoints, middleware, configuration
```

## Core Entities

### User
- Represents system users with authentication credentials
- Properties: Email, PasswordHash, FirstName, LastName, PhoneNumber, IsActive
- Relationships: Many UserRoles, Many CompanyUsers

### Company
- Represents client organizations
- Properties: Name, ABN, Industry, Country, BankIntegrationEnabled
- Relationships: Many CompanyUsers, Many BankConnections, Many BankTransactions

### CompanyUser
- Junction table linking Users to Companies
- Properties: CompanyId, UserId, Role (Admin/Accountant/Client)
- Ensures each user-company combination is unique

### BankConnection
- Represents integrated bank accounts
- Properties: CompanyId, ProviderName, ConnectionId, Status, LastSyncDate
- Statuses: Active, Inactive, Disconnected, Error
- Relationships: Belongs to Company, Has many BankTransactions

### BankTransaction
- Represents individual bank transactions
- Properties: CompanyId, Amount, Description, TransactionDate, BankConnectionId
- Indexed on (CompanyId, TransactionDate) for efficient queries
- Optional relationship to BankConnection

## Architecture Features

### Clean Architecture Layers
1. **Domain**: Pure business entities, no external dependencies
2. **Application**: Business logic, DTOs, service interfaces
3. **Infrastructure**: EF Core, repositories, database configurations
4. **API**: HTTP endpoints, middleware, configuration

### Built-in Features
- JWT Authentication ready
- Role-Based Access Control (RBAC)
- Soft delete support for all entities
- Automatic audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
- Global exception handling middleware
- Request logging middleware
- Swagger/OpenAPI documentation
- Azure SQL compatible

## Database Schema

### Tables
- **Users**: User accounts and authentication
- **Companies**: Client organizations
- **CompanyUsers**: User-Company relationships (M2M)
- **BankConnections**: Bank integrations
- **BankTransactions**: Bank transaction records
- **Roles**: System roles (RBAC)
- **Permissions**: System permissions
- **UserRoles**: User-Role relationships (M2M)
- **RolePermissions**: Role-Permission relationships (M2M)
- **Organizations**: Multi-tenant organizations

### Entity Relationships
```
User (1) ──> (M) UserRole
User (1) ──> (M) CompanyUser
Company (1) ──> (M) CompanyUser
Company (1) ──> (M) BankConnection
Company (1) ──> (M) BankTransaction
BankConnection (1) ──> (M) BankTransaction
```

## Technology Stack

- **.NET 8**: Latest .NET runtime
- **Entity Framework Core 8**: ORM for data access
- **SQL Server**: Azure SQL compatible database
- **JWT**: Secure authentication
- **Serilog**: Structured logging
- **Swagger/OpenAPI**: API documentation

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) or [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [JetBrains Rider](https://www.jetbrains.com/rider/) (Optional)

### Configuration

1. Update the connection string in `StudioTechBI.API/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StudioTechBIDb;Trusted_Connection=true;MultipleActiveResultSets=true"
}
```

**For Azure SQL Database**:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=StudioTechBIDb;Persist Security Info=False;User ID=YOUR_USERNAME;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}
```

2. Configure JWT settings in `appsettings.json`:

```json
"JwtSettings": {
  "SecretKey": "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS_LONG",
  "Issuer": "StudioTechBI",
  "Audience": "StudioTechBIUsers",
  "AccessTokenExpirationMinutes": 60,
  "RefreshTokenExpirationDays": 7
}
```

### Database Setup

```bash
# Build the solution
dotnet build

# Apply migrations to create database
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### Running the Application

```bash
cd StudioTechBI.API
dotnet run
```

The API will be available at:
- **HTTPS**: https://localhost:5001
- **HTTP**: http://localhost:5000
- **Swagger UI**: https://localhost:5001/swagger

## API Endpoints

### Health Check
- `GET /api/health` - Check API health status

### Future Endpoints
- `POST /api/auth/login` - User login
- `POST /api/auth/register` - User registration
- `GET /api/users/{id}` - Get user profile
- `GET /api/companies/{id}` - Get company details
- `POST /api/companies/{companyId}/bank-connections` - Create bank connection
- `GET /api/companies/{companyId}/transactions` - List transactions

## Development Workflow

### Adding a New Entity

1. Create entity in `Domain/Entities` extending `BaseEntity`
2. Create DTOs in `Application/DTOs` if needed
3. Add DbSet to `ApplicationDbContext`
4. Create EF Core configuration in `Infrastructure/Configurations`
5. Create migration: `dotnet ef migrations add EntityName --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API`
6. Update database: `dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API`

### Adding a New Service

1. Define interface in `Application/Interfaces`
2. Implement in `Application/Services` or `Infrastructure/Services`
3. Register in appropriate `DependencyInjection.cs`
4. Inject into controller or other services

### Creating an Endpoint

1. Create or update controller in `API/Controllers` inheriting from `BaseApiController`
2. Implement action methods
3. Use dependency injection for services
4. Document with XML comments for Swagger

## Migration Guide

### Create and Apply Migrations

```bash
# Create migration
dotnet ef migrations add MigrationName --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Apply to database
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# View migration SQL
dotnet ef migrations script --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### Reset Database

```bash
dotnet ef database drop -f --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

## Logging

Logging is configured using Serilog:
- **Console**: Development environment
- **File**: Rolling daily logs in `logs/` directory
- **Levels**: Debug, Information, Warning, Error, Fatal

Configure in `appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft": "Warning"
  }
}
```

## Security Best Practices

1. **Password Hashing**: Use BCrypt (structure ready)
2. **JWT Tokens**: Secure token validation implemented
3. **HTTPS**: Enforced in production
4. **CORS**: Configure for your frontend domain
5. **SQL Injection**: Protected via EF Core parameterized queries
6. **Soft Delete**: Sensitive data never permanently deleted
7. **Audit Trail**: All changes tracked with CreatedBy/UpdatedBy

## Performance Optimization

### Database Indexes
- Email on Users (unique constraint)
- CompanyId + UserId on CompanyUsers (unique constraint)
- CompanyId + ProviderName on BankConnections
- CompanyId + TransactionDate on BankTransactions
- BankConnectionId on BankTransactions

### Query Best Practices
- Use `AsNoTracking()` for read-only queries
- Implement pagination for large result sets
- Use `Select()` to fetch only needed columns
- Implement caching for frequently accessed data

## Azure Deployment

### Prerequisites
- Azure account with active subscription
- Azure SQL Database
- Azure App Service

### Configuration
```bash
# Build release
dotnet publish --configuration Release --output ./publish

# Deploy to Azure App Service
az webapp deployment source config-zip --resource-group YOUR_RG --name YOUR_APP --src ./publish.zip
```

### Environment Variables
- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey`
- `ASPNETCORE_ENVIRONMENT` (Production)
- `UploadLimits__MaxUploadBytes` (optional, default 50 MB) — sync-path/routing threshold for
  file uploads (Report Generator, Report Designer, Dashboard Template Generator, Report
  Validation, admin template upload). Below this, uploads go through the original fast
  synchronous flow unchanged.
- `UploadLimits__MaxAsyncUploadBytes` (optional, default 500 MB) — hard ceiling for the
  Report Generator's direct-to-blob async upload path (see "Large-file upload" below). Must
  stay `<=` `ReportAgent:MaxInputFileBytes` on the ReportAgent.Api side (stbi_transformers) or
  a large job will fail late, inside the worker, instead of at upload time.

### Large-file upload (direct-to-blob + async processing) — deploy-time prerequisites

The Report Generator's large-file path (`POST /api/report-generator/uploads/init` +
`.../{jobId}/complete`) uploads straight from the browser to Azure Blob Storage using a
short-lived SAS URL — the app tier never buffers the file. Two things must be configured on
the **Storage Account** itself for this to work; neither is something the application code
can set up on its own:

1. **CORS rules on the Storage Account**, so a browser on the frontend's origin(s) is allowed
   to `PUT` directly to a blob URL. Without this, every direct upload fails with a CORS error
   in the browser console even though the SAS URL itself is valid — this is the most common
   silent failure point for this pattern in practice. Minimum required rule (Storage Account →
   Settings → Resource sharing (CORS) → Blob service):
   - Allowed origins: the frontend's deployed origin(s) (e.g. `https://app.studiotechbi.com`)
   - Allowed methods: `PUT`, `OPTIONS`
   - Allowed headers: `x-ms-*`, `Content-Type`, `Content-Length`
   - Exposed headers: `x-ms-*`
   - Max age: a few hours is fine (e.g. `3600`)
2. **A blob lifecycle management policy** on the `clients` container's
   `*/report-generator/uploads/*` prefix, to clean up an orphaned upload — the case where a
   browser's direct PUT succeeds but the "confirm complete" call never arrives (closed tab,
   dropped connection, network failure). A simple "delete blobs under this prefix N days after
   last modified" rule (Storage Account → Data management → Lifecycle management) is enough;
   no application code sweeps these on its own. `ReportGenerationJob` rows for an orphaned
   upload just stay `Pending` forever — harmless, but worth knowing they won't self-clean
   either (a low-priority admin cleanup query, not built as part of this feature).

Also requires the **Azure Storage Queue** service to be reachable on the same storage account
already configured via `AzureBlob:ConnectionString` — `ReportGenerationJobQueue` creates its
own queue (`report-generation-jobs`) on first use, no manual provisioning needed there.

## Common Commands

```bash
# Restore NuGet packages
dotnet restore

# Build solution
dotnet build

# Run in watch mode (auto-reload)
cd StudioTechBI.API
dotnet watch run

# Run tests
dotnet test

# Add NuGet package
dotnet add StudioTechBI.API package PackageName

# View all migrations
dotnet ef migrations list --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Remove last migration
dotnet ef migrations remove --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

## Troubleshooting

### Connection String Issues
```bash
# Test LocalDB
sqllocaldb info
sqllocaldb start MSSQLLocalDB
```

### Migration Errors
```bash
# Remove last migration
dotnet ef migrations remove --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Or reset database
dotnet ef database drop -f --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## Documentation

- **ARCHITECTURE.md** - Detailed architecture documentation
- **QUICKSTART.md** - Step-by-step developer setup guide
- **API Documentation** - Available via Swagger at https://localhost:5001/swagger

## Next Steps

1. Implement authentication services
2. Create authorization policies
3. Implement account management endpoints
4. Create company management endpoints
5. Implement bank integration service
6. Add unit and integration tests
7. Set up CI/CD pipeline
8. Configure Azure deployment
9. Implement caching strategy
10. Add API rate limiting

## License

Proprietary - StudioTechBI Inc.

## Support

For issues or questions, refer to the documentation files in this project.
