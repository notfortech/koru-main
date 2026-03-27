# StudioTechBI Backend - Quick Start Guide

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) or [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [JetBrains Rider](https://www.jetbrains.com/rider/) (Optional)
- [Azure Data Studio](https://azure.microsoft.com/products/data-studio/) or [SQL Server Management Studio](https://docs.microsoft.com/sql/ssms/) (Optional)

## Initial Setup

### 1. Clone and Navigate

```bash
cd StudioTechBI.Backend
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Configure Database Connection

Edit `StudioTechBI.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StudioTechBIDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

**For Azure SQL Database**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:YOUR_SERVER.database.windows.net,1433;Initial Catalog=StudioTechBIDb;Persist Security Info=False;User ID=YOUR_USERNAME;Password=YOUR_PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

### 4. Configure JWT Settings

Edit `StudioTechBI.API/appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS_LONG",
    "Issuer": "StudioTechBI",
    "Audience": "StudioTechBIUsers",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

## Database Setup

### Create Initial Migration

```bash
dotnet ef migrations add InitialCreate --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### Apply Migration to Database

```bash
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### View Migration SQL (Optional)

```bash
dotnet ef migrations script --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

## Build and Run

### Build the Solution

```bash
dotnet build
```

### Run the API

```bash
cd StudioTechBI.API
dotnet run
```

The API will start at:
- **HTTPS**: https://localhost:5001
- **HTTP**: http://localhost:5000
- **Swagger UI**: https://localhost:5001

### Run with Watch (Auto-reload)

```bash
cd StudioTechBI.API
dotnet watch run
```

## Common Commands

### Entity Framework Core

```bash
# Add new migration
dotnet ef migrations add MigrationName --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Update database to latest migration
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Rollback to specific migration
dotnet ef database update MigrationName --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Remove last migration (if not applied)
dotnet ef migrations remove --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# List all migrations
dotnet ef migrations list --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Drop database
dotnet ef database drop --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### Build & Clean

```bash
# Clean build artifacts
dotnet clean

# Build in Release mode
dotnet build --configuration Release

# Publish for deployment
dotnet publish --configuration Release --output ./publish
```

### Testing (Future)

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure Commands

### Navigate to Projects

```bash
# API Layer
cd StudioTechBI.API

# Application Layer
cd StudioTechBI.Application

# Domain Layer
cd StudioTechBI.Domain

# Infrastructure Layer
cd StudioTechBI.Infrastructure
```

### Add NuGet Package

```bash
# To API project
dotnet add StudioTechBI.API package PackageName

# To Application project
dotnet add StudioTechBI.Application package PackageName

# To Infrastructure project
dotnet add StudioTechBI.Infrastructure package PackageName
```

### Add Project Reference

```bash
dotnet add StudioTechBI.API reference StudioTechBI.Application
dotnet add StudioTechBI.Application reference StudioTechBI.Domain
```

## Development Workflow

### 1. Create New Feature

1. Define entities in `Domain/Entities`
2. Create DTOs in `Application/DTOs`
3. Define service interfaces in `Application/Interfaces`
4. Implement services in `Application/Services` or `Infrastructure/Services`
5. Create entity configurations in `Infrastructure/Configurations`
6. Add DbSet to `ApplicationDbContext`
7. Create migration
8. Create controllers in `API/Controllers`
9. Test with Swagger UI

### 2. Create Migration

```bash
dotnet ef migrations add FeatureName --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### 3. Test API

Navigate to: https://localhost:5001

## Troubleshooting

### Connection Issues

**Problem**: Cannot connect to database

**Solutions**:
```bash
# Verify connection string
# Check if SQL Server is running
# Test connection using SQL client

# For LocalDB
sqllocaldb info
sqllocaldb start MSSQLLocalDB
```

### Migration Issues

**Problem**: Migration fails

**Solutions**:
```bash
# Drop database and recreate
dotnet ef database drop --force --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
dotnet ef database update --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API

# Or remove last migration
dotnet ef migrations remove --project StudioTechBI.Infrastructure --startup-project StudioTechBI.API
```

### Build Issues

**Problem**: Build fails

**Solutions**:
```bash
# Clean and restore
dotnet clean
dotnet restore
dotnet build
```

## API Testing with cURL

### Health Check

```bash
curl https://localhost:5001/api/health
```

### Example POST Request

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"password123"}'
```

### Example with Authorization

```bash
curl https://localhost:5001/api/users \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

## IDE Setup

### Visual Studio 2022

1. Open `StudioTechBI.sln`
2. Set `StudioTechBI.API` as startup project
3. Press F5 to run with debugging
4. Press Ctrl+F5 to run without debugging

### Visual Studio Code

1. Install C# extension
2. Open folder: `StudioTechBI.Backend`
3. Press F5 to run with debugging

### JetBrains Rider

1. Open `StudioTechBI.sln`
2. Right-click `StudioTechBI.API` → Set as Startup Project
3. Press Shift+F10 to run

## Environment Variables

For production, set these as environment variables instead of appsettings.json:

```bash
# Windows PowerShell
$env:ConnectionStrings__DefaultConnection="Server=..."
$env:JwtSettings__SecretKey="..."

# Linux/macOS
export ConnectionStrings__DefaultConnection="Server=..."
export JwtSettings__SecretKey="..."
```

## Next Steps

1. ✅ Setup completed
2. 🔨 Implement authentication services
3. 🔨 Create business-specific entities
4. 🔨 Add authorization policies
5. 🔨 Write unit tests
6. 🔨 Configure CI/CD
7. 🔨 Deploy to Azure

## Useful Links

- [.NET 8 Documentation](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [ASP.NET Core](https://learn.microsoft.com/aspnet/core/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Azure SQL Database](https://azure.microsoft.com/products/azure-sql/database/)

## Support

For issues or questions, refer to:
- README.md for architecture overview
- ARCHITECTURE.md for detailed design documentation
- Swagger UI at https://localhost:5001 for API documentation
