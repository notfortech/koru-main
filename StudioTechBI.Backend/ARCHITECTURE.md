# StudioTechBI Backend - Architecture Documentation

## Overview

StudioTechBI is a production-grade SaaS Accounting Intelligence Platform built using .NET 8 and Clean Architecture principles. This document provides a comprehensive overview of the system architecture, design patterns, and implementation details.

## Clean Architecture Layers

### 1. Domain Layer (StudioTechBI.Domain)

**Purpose**: Contains core business entities and domain logic. This layer has no dependencies on other layers.

**Components**:

- **Entities**: Core business objects that represent the domain model
  - `BaseEntity`: Abstract base class with common properties (Id, CreatedAt, UpdatedAt, IsDeleted, etc.)
  - `User`: User account entity with authentication properties
  - `Role`: Role definition for RBAC
  - `Permission`: Permission definition for granular access control
  - `UserRole`: Many-to-many relationship between Users and Roles
  - `RolePermission`: Many-to-many relationship between Roles and Permissions
  - `Organization`: Multi-tenant organization entity

- **Enums**: Domain enumerations
  - `UserStatus`: Active, Inactive, Suspended, PendingVerification
  - `SubscriptionTier`: Free, Basic, Professional, Enterprise

- **Interfaces**: Core abstractions
  - `IRepository<T>`: Generic repository pattern interface
  - `IUnitOfWork`: Unit of Work pattern for transaction management

**Dependencies**: None

### 2. Application Layer (StudioTechBI.Application)

**Purpose**: Contains business logic, use cases, and application services. Orchestrates the flow between UI and Domain.

**Components**:

- **DTOs** (Data Transfer Objects):
  - Common: `ApiResponse<T>`, `PaginatedResult<T>`
  - Auth: `LoginRequestDto`, `LoginResponseDto`, `RegisterRequestDto`, `UserDto`, `RefreshTokenRequestDto`

- **Interfaces**: Service contracts
  - `IAuthService`: Authentication operations
  - `IUserService`: User management operations
  - `IJwtTokenService`: JWT token generation and validation

- **Services**: Business logic implementation
  - `BaseService`: Abstract base service with common functionality

**Dependencies**: StudioTechBI.Domain

### 3. Infrastructure Layer (StudioTechBI.Infrastructure)

**Purpose**: Implements interfaces defined in Domain and Application layers. Handles data persistence, external services, and infrastructure concerns.

**Components**:

- **Data**: Database context
  - `ApplicationDbContext`: EF Core DbContext with automatic audit fields

- **Repositories**: Data access implementations
  - `Repository<T>`: Generic repository implementation with soft delete support
  - `UnitOfWork`: Transaction management implementation

- **Configurations**: Entity configurations
  - `UserConfiguration`: EF Core fluent API configuration for User entity
  - `RoleConfiguration`: Configuration for Role entity
  - `PermissionConfiguration`: Configuration for Permission entity
  - `OrganizationConfiguration`: Configuration for Organization entity

- **Services**: Infrastructure service implementations
  - `JwtTokenService`: JWT token generation and validation

**Dependencies**: StudioTechBI.Domain, StudioTechBI.Application

### 4. API Layer (StudioTechBI.API)

**Purpose**: Presents HTTP API endpoints, handles HTTP requests/responses, and coordinates application services.

**Components**:

- **Controllers**:
  - `BaseApiController`: Base controller with common functionality
  - `HealthController`: Health check endpoint

- **Middleware**:
  - `ExceptionHandlingMiddleware`: Global exception handling
  - `RequestLoggingMiddleware`: Request/response logging

- **Configuration**:
  - `Program.cs`: Application startup and configuration
  - `appsettings.json`: Application settings
  - JWT authentication setup
  - Swagger/OpenAPI configuration

**Dependencies**: StudioTechBI.Application, StudioTechBI.Infrastructure

## Design Patterns

### 1. Repository Pattern

Abstracts data access logic and provides a collection-like interface for accessing domain entities.

**Benefits**:
- Decouples business logic from data access
- Facilitates unit testing with mocking
- Centralizes data access logic

**Implementation**:
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    // ... other methods
}
```

### 2. Unit of Work Pattern

Maintains a list of objects affected by a business transaction and coordinates the writing out of changes.

**Benefits**:
- Ensures consistency across repositories
- Manages database transactions
- Reduces database round-trips

**Implementation**:
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
```

### 3. Dependency Injection

All dependencies are injected through constructors, promoting loose coupling and testability.

**Implementation**:
- Each layer has a `DependencyInjection` class with extension methods
- Services are registered in `Program.cs`

### 4. Service Layer Pattern

Business logic is encapsulated in service classes, keeping controllers thin.

**Benefits**:
- Separation of concerns
- Reusable business logic
- Easier to test

## Key Features

### 1. Authentication & Authorization

**JWT-based Authentication**:
- Access tokens with configurable expiration
- Refresh tokens for extended sessions
- Token validation middleware

**Role-based Authorization**:
- User-Role-Permission hierarchy
- Flexible permission system
- System roles vs custom roles

### 2. Soft Delete

All entities inherit from `BaseEntity` which includes `IsDeleted` flag. Repository implementations automatically filter deleted entities.

### 3. Audit Fields

Automatic tracking of:
- `CreatedAt`: When the entity was created
- `UpdatedAt`: When the entity was last modified
- `CreatedBy`: User who created the entity
- `UpdatedBy`: User who last modified the entity

### 4. Global Error Handling

`ExceptionHandlingMiddleware` catches all unhandled exceptions and returns consistent error responses.

### 5. Request Logging

`RequestLoggingMiddleware` logs all incoming requests and their execution time.

### 6. API Documentation

Swagger/OpenAPI integrated for interactive API documentation.

## Database Schema

### Core Tables

1. **Users**: User accounts
2. **Roles**: Role definitions
3. **Permissions**: Permission definitions
4. **UserRoles**: User-Role associations (many-to-many)
5. **RolePermissions**: Role-Permission associations (many-to-many)
6. **Organizations**: Multi-tenant organizations

### Relationships

- User ↔ Role: Many-to-Many through UserRoles
- Role ↔ Permission: Many-to-Many through RolePermissions

## Configuration

### Connection Strings

Configure in `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=...;Database=StudioTechBIDb;..."
}
```

### JWT Settings

Configure in `appsettings.json`:
```json
"JwtSettings": {
  "SecretKey": "...",
  "Issuer": "StudioTechBI",
  "Audience": "StudioTechBIUsers",
  "AccessTokenExpirationMinutes": 60,
  "RefreshTokenExpirationDays": 7
}
```

## Security Considerations

1. **Password Hashing**: Implement using BCrypt or similar (structure ready)
2. **JWT Tokens**: Secure token generation and validation
3. **HTTPS**: Enforced in production
4. **CORS**: Configured (needs production adjustment)
5. **Soft Delete**: Sensitive data never truly deleted
6. **Input Validation**: DTOs with data annotations

## Azure Deployment Ready

### Required Azure Resources

1. **Azure SQL Database**: For data persistence
2. **Azure App Service**: For hosting the API
3. **Azure Key Vault**: For secure secret management (recommended)
4. **Application Insights**: For monitoring and logging (recommended)

### Environment Variables

Set these in Azure App Service configuration:
- `ConnectionStrings__DefaultConnection`
- `JwtSettings__SecretKey`
- `ASPNETCORE_ENVIRONMENT`

## Extension Points

### Adding New Entities

1. Create entity in `Domain/Entities` inheriting from `BaseEntity`
2. Add DbSet to `ApplicationDbContext`
3. Create entity configuration in `Infrastructure/Configurations`
4. Create migration using EF Core tools

### Adding New Services

1. Define interface in `Application/Interfaces`
2. Implement service in `Application/Services` or `Infrastructure/Services`
3. Register in appropriate `DependencyInjection` class

### Adding New Endpoints

1. Create DTOs in `Application/DTOs`
2. Create controller in `API/Controllers` inheriting from `BaseApiController`
3. Implement service logic
4. Add authorization attributes as needed

## Testing Strategy (Future)

### Unit Tests
- Domain entities business logic
- Application services
- Repository implementations

### Integration Tests
- API endpoints
- Database operations
- Authentication flows

### Performance Tests
- Load testing endpoints
- Database query optimization

## Monitoring & Logging

### Serilog Configuration

Logs are written to:
- Console (for debugging)
- File (`logs/log-{Date}.txt`)

### Logging Levels

- **Information**: Request/response logs
- **Warning**: Non-critical issues
- **Error**: Caught exceptions
- **Fatal**: Application startup failures

## Best Practices Implemented

1. **SOLID Principles**: Each class has single responsibility
2. **DRY**: Code reuse through base classes and services
3. **Async/Await**: All I/O operations are asynchronous
4. **Dependency Injection**: All dependencies injected
5. **Configuration Over Code**: Settings in appsettings.json
6. **Separation of Concerns**: Clear layer boundaries
7. **API Versioning Ready**: Structure supports versioning
8. **Entity Framework Best Practices**: Configurations, migrations, change tracking

## Next Steps

1. Implement business-specific entities and logic
2. Implement authentication services
3. Add authorization policies
4. Create database migrations
5. Implement unit tests
6. Set up CI/CD pipeline
7. Configure production environment
8. Implement monitoring and alerting

## Conclusion

This architecture provides a solid, scalable foundation for building a SaaS Accounting Intelligence Platform. The modular structure allows for easy extension and maintenance while following industry best practices and clean architecture principles.
