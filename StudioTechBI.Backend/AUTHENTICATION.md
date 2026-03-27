# StudioTechBI Authentication & Authorization Guide

## Overview

StudioTechBI implements a complete JWT (JSON Web Token) based authentication system with role-based access control (RBAC). This guide provides comprehensive information about authentication setup, usage, and best practices.

## Architecture

### Authentication Flow

```
1. User submits login credentials (email, password)
                    ↓
2. AuthService validates credentials
   - Verifies email exists
   - Verifies password against BCrypt hash
   - Checks user is active
                    ↓
3. Generate JWT tokens
   - AccessToken (60 minutes expiry)
   - RefreshToken (7 days expiry)
                    ↓
4. Return tokens + user info to client
                    ↓
5. Client includes AccessToken in Authorization header for subsequent requests
                    ↓
6. JWT middleware validates token on each request
```

## Components

### 1. AuthService (Application Layer)

**Location**: `StudioTechBI.Application/Services/AuthService.cs`

**Responsibilities**:
- User login validation
- User registration
- Password hashing and verification
- JWT token generation
- Token refresh handling
- Token revocation

**Key Methods**:
```csharp
Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
Task<LoginResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken)
Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken)
Task<bool> RevokeTokenAsync(string userId, CancellationToken cancellationToken)
```

### 2. JwtTokenService (Infrastructure Layer)

**Location**: `StudioTechBI.Infrastructure/Services/JwtTokenService.cs`

**Responsibilities**:
- Access token generation
- Refresh token generation
- Token validation
- Claims extraction

**Configuration**:
Configured in `appsettings.json`:
```json
"JwtSettings": {
  "SecretKey": "YourVeryLongSecureKeyAtLeast32CharactersLong!@#",
  "Issuer": "StudioTechBI",
  "Audience": "StudioTechBIUsers",
  "AccessTokenExpirationMinutes": 60,
  "RefreshTokenExpirationDays": 7
}
```

### 3. AuthController (API Layer)

**Location**: `StudioTechBI.API/Controllers/AuthController.cs`

**Endpoints**:

#### POST /api/auth/login
Authenticate user with email and password.

**Request**:
```json
{
  "email": "user@example.com",
  "password": "securePassword123"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "very-long-base64-encoded-refresh-token",
    "expiresAt": "2024-12-20T10:30:00Z",
    "user": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "phoneNumber": "+1234567890",
      "isActive": true,
      "roles": ["Admin"],
      "hasAIInsights": true
    }
  },
  "message": "Login successful"
}
```

#### POST /api/auth/register
Register a new user account.

**Request**:
```json
{
  "email": "newuser@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "password": "securePassword123",
  "confirmPassword": "securePassword123"
}
```

**Response**: Same as login endpoint

#### POST /api/auth/refresh
Refresh expired access token using refresh token.

**Request**:
```json
{
  "accessToken": "expired-access-token",
  "refreshToken": "valid-refresh-token"
}
```

**Response**: New access token and refresh token

#### POST /api/auth/logout
Revoke refresh token (requires authentication).

**Request**: No body required (uses current user from token)

**Response**:
```json
{
  "success": true,
  "data": null,
  "message": "Logout successful"
}
```

### 4. Authorization Policies

**Location**: `StudioTechBI.API/Authorization/AuthorizationPolicies.cs`

**Available Policies**:

| Policy | Description | Required Roles |
|--------|-------------|-----------------|
| `AdminOnly` | Admin access only | Admin |
| `AccountantOnly` | Accountant access only | Accountant |
| `ClientOnly` | Client access only | Client |
| `AdminOrAccountant` | Admin or Accountant access | Admin, Accountant |
| `AnyAuthenticated` | Any authenticated user | Any role |

**Usage in Controllers**:
```csharp
[Authorize(Policy = AuthorizationPolicies.AdminPolicy)]
public IActionResult AdminOnlyEndpoint()
{
    // Only Admin users can access
}

[Authorize(Policy = AuthorizationPolicies.AdminOrAccountantPolicy)]
public IActionResult ProcessTransactions()
{
    // Admin and Accountant can access
}

[Authorize]
public IActionResult AuthenticatedUsersOnly()
{
    // Any authenticated user can access
}
```

## Password Hashing

### BCrypt Implementation

**Library**: `BCrypt.Net-Next` v4.0.3

**Hashing Function**:
```csharp
public static string HashPassword(string password)
{
    return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
}
```

**Verification Function**:
```csharp
public static bool VerifyPassword(string password, string hash)
{
    try
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
    catch
    {
        return false;
    }
}
```

**Features**:
- Work factor of 11 provides secure hashing (computationally expensive)
- Automatic salt generation
- Protection against rainbow table attacks
- Timing-attack resistant verification

## JWT Token Structure

### Access Token Claims

```csharp
{
  "nameid": "550e8400-e29b-41d4-a716-446655440000",  // User ID
  "email": "user@example.com",
  "name": "John Doe",
  "given_name": "John",
  "family_name": "Doe",
  "role": ["Admin", "Accountant"],
  "iat": 1703068200,
  "exp": 1703071800,
  "iss": "StudioTechBI",
  "aud": "StudioTechBIUsers"
}
```

### Token Validation

JWT Bearer middleware validates:
- Token signature using secret key
- Issuer matches configured issuer
- Audience matches configured audience
- Token is not expired
- Algorithm is HMAC SHA256

## Role-Based Access Control (RBAC)

### Role Hierarchy

1. **Admin**: Full system access
   - Manage all users
   - Access all data
   - System configuration
   - Create other admins

2. **Accountant**: Financial operations
   - Manage transactions
   - Generate reports
   - Access company financials
   - Limited user management

3. **Client**: Read-only operations
   - View own data
   - View transaction history
   - Download statements
   - No write permissions

### Assigning Roles

Roles are assigned via `UserRole` entity:

```csharp
// Add Admin role to user
var adminRole = await _roleRepository.GetByPredicateAsync(
    r => r.Name == "Admin", cancellationToken);

var userRole = new UserRole
{
    UserId = userId,
    RoleId = adminRole.Id
};

await _userRoleRepository.AddAsync(userRole, cancellationToken);
await _unitOfWork.SaveChangesAsync(cancellationToken);
```

## Implementation Examples

### 1. Login Flow

```bash
# 1. Send login request
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "password123"
  }'

# 2. Receive tokens
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "very-long-refresh-token",
  "expiresAt": "2024-12-20T10:30:00Z"
}

# 3. Use access token for protected endpoints
curl -X GET https://localhost:5001/api/users/me \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..."
```

### 2. Protected Endpoint

```csharp
[HttpGet("admin-data")]
[Authorize(Policy = AuthorizationPolicies.AdminPolicy)]
public IActionResult GetAdminData()
{
    // Only accessible by users with Admin role
    // Middleware automatically returns 403 Forbidden for unauthorized users
    var data = new { message = "Admin data" };
    return Ok(ApiResponse<object>.SuccessResponse(data));
}
```

### 3. Accessing User Claims

```csharp
[Authorize]
public IActionResult GetUserInfo()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = User.FindFirst(ClaimTypes.Email)?.Value;
    var roles = User.FindAll(ClaimTypes.Role);

    return Ok(new
    {
        UserId = userId,
        Email = email,
        Roles = roles.Select(r => r.Value)
    });
}
```

### 4. Token Refresh

```bash
# When access token expires
curl -X POST https://localhost:5001/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "accessToken": "expired-token",
    "refreshToken": "valid-refresh-token"
  }'

# Get new access token
{
  "accessToken": "new-access-token",
  "refreshToken": "new-refresh-token",
  "expiresAt": "2024-12-20T11:30:00Z"
}
```

## Security Best Practices

### 1. Secret Key Management

**DO**:
- Use Azure Key Vault in production
- Use strong, random 32+ character keys
- Rotate keys periodically
- Never commit secrets to version control

**DON'T**:
- Store secrets in appsettings.json in production
- Use weak or hardcoded secrets
- Share secrets via email or chat
- Log sensitive information

### 2. HTTPS Enforcement

**Configuration**: `Program.cs`
```csharp
app.UseHttpsRedirection();
```

All authentication endpoints MUST use HTTPS in production.

### 3. Token Storage (Frontend)

**Best Practice**: Store in Memory or SessionStorage
```javascript
// ✓ Good: Store in memory (lost on page refresh)
let accessToken = response.accessToken;

// ✗ Avoid: LocalStorage (vulnerable to XSS)
localStorage.setItem('token', response.accessToken);

// ✗ Avoid: Cookies without HttpOnly flag (vulnerable to XSS)
document.cookie = `token=${response.accessToken}`;
```

### 4. Request Logging

Sensitive information is excluded from logs:
- Passwords
- Password hashes
- Tokens
- Secret keys

### 5. CORS Configuration

**Development** (Allow All):
```csharp
options.AddPolicy("AllowAll", policy =>
{
    policy.WithOrigins("https://yourdomain.com")
          .AllowAnyMethod()
          .AllowAnyHeader();
});
```

**Production** (Specific Origins):
```csharp
options.AddPolicy("Production", policy =>
{
    policy.WithOrigins("https://yourdomain.com")
          .AllowAnyMethod()
          .AllowCredentials()
          .AllowAnyHeader();
});
```

### 6. Rate Limiting (Future Enhancement)

Implement rate limiting to prevent brute force attacks:
- Maximum login attempts per IP
- Account lockout after failed attempts
- Progressive delays between attempts

## Database User Setup

### Create Initial Admin User

```sql
-- Hash: BCrypt("AdminPassword123")
INSERT INTO Users (Id, Email, PasswordHash, FirstName, LastName, IsActive, CreatedAt)
VALUES (
  NEWID(),
  'admin@studiotechbi.com',
  '$2a$11$...',  -- BCrypt hash
  'Admin',
  'User',
  1,
  GETUTCDATE()
);

-- Get the Admin role
DECLARE @AdminRoleId UNIQUEIDENTIFIER;
SELECT @AdminRoleId = Id FROM Roles WHERE Name = 'Admin' AND IsDeleted = 0;

-- Assign Admin role
INSERT INTO UserRoles (Id, UserId, RoleId, CreatedAt)
VALUES (
  NEWID(),
  (SELECT Id FROM Users WHERE Email = 'admin@studiotechbi.com'),
  @AdminRoleId,
  GETUTCDATE()
);
```

## Testing Authentication

### Using Swagger UI

1. Navigate to https://localhost:5001/swagger
2. Click "Authorize" button
3. Click "jwt" security
4. Enter: `Bearer <your-access-token>`
5. Click "Authorize"
6. Test protected endpoints

### Using cURL

```bash
# Login
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "password123"
  }' \
  -k  # Ignore SSL in development

# Use token
curl -X GET https://localhost:5001/api/users/me \
  -H "Authorization: Bearer <access-token>" \
  -k
```

## Troubleshooting

### 401 Unauthorized

**Cause**: Missing or invalid token

**Solutions**:
- Include Authorization header: `Authorization: Bearer <token>`
- Ensure token is not expired
- Verify token format is correct

### 403 Forbidden

**Cause**: User doesn't have required role

**Solutions**:
- Check user roles in database
- Verify authorization policy configuration
- Ensure role names match exactly (case-sensitive)

### Invalid Token Exception

**Cause**: Token signature or claims invalid

**Solutions**:
- Verify JWT secret key is same in all environments
- Check token hasn't been modified
- Ensure issuer and audience match configuration

### Password Verification Failed

**Cause**: Password hash mismatch

**Solutions**:
- Verify password is correct
- Check BCrypt hash is valid
- Ensure hash wasn't corrupted in database

## Configuration Reference

### appsettings.json

```json
{
  "JwtSettings": {
    "SecretKey": "YourVeryLongSecureKeyAtLeast32CharactersLong!@#",
    "Issuer": "StudioTechBI",
    "Audience": "StudioTechBIUsers",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

### Environment Variables (Production)

```bash
export JwtSettings__SecretKey="your-secret-key"
export JwtSettings__Issuer="StudioTechBI"
export JwtSettings__Audience="StudioTechBIUsers"
export JwtSettings__AccessTokenExpirationMinutes="60"
export JwtSettings__RefreshTokenExpirationDays="7"
```

## API Response Codes

| Code | Meaning | Example |
|------|---------|---------|
| 200 | Success | Login successful |
| 400 | Bad Request | Invalid email format |
| 401 | Unauthorized | Invalid password, expired token |
| 403 | Forbidden | Insufficient permissions |
| 500 | Server Error | Database connection error |

## Future Enhancements

1. **Multi-Factor Authentication (MFA)**
   - TOTP (Time-based One-Time Password)
   - Email verification
   - SMS verification

2. **OAuth/OpenID Connect**
   - Google login
   - Microsoft login
   - GitHub login

3. **Advanced Security**
   - Rate limiting
   - Account lockout
   - Login audit trail
   - IP whitelist/blacklist

4. **Session Management**
   - Multiple device sessions
   - Session revocation
   - Device management

5. **Audit Logging**
   - Login history
   - Failed attempt tracking
   - Permission change log

## Support

For authentication issues:
1. Check logs for error details
2. Review AUTHENTICATION.md (this file)
3. Verify configuration in appsettings.json
4. Test with Swagger UI
5. Check database user records

