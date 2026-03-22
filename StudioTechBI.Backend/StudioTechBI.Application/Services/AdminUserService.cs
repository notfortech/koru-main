using StudioTechBI.Application.DTOs.Admin;
using StudioTechBI.Application.DTOs.Common;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Domain.Entities;
using StudioTechBI.Domain.Interfaces;

namespace StudioTechBI.Application.Services;

public class AdminUserService : BaseService, IAdminUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserRole> _userRoleRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IAdminLoggingService _adminLogging;

    public AdminUserService(
        IUnitOfWork unitOfWork,
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<Role> roleRepository,
        IAuditLogService auditLogService,
        IAdminLoggingService adminLogging)
        : base(unitOfWork)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _auditLogService = auditLogService;
        _adminLogging = adminLogging;
    }

    public async Task<AdminUserDto> CreateAsync(AdminUserCreateDto dto, CancellationToken cancellationToken = default)
    {
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            throw new InvalidOperationException("Email is required.");
        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new InvalidOperationException("Password is required.");

        var existing = await _userRepository.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("A user with this email already exists.");

        if (dto.UserType != 0 && dto.UserType != 1)
            throw new InvalidOperationException("UserType must be 0 (general client) or 1 (accountant).");
        if (dto.UserType == 1 && !dto.ClientId.HasValue)
            throw new InvalidOperationException("Client is required for accountant users (UserType = 1).");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = (dto.FirstName ?? "").Trim(),
            LastName = (dto.LastName ?? "").Trim(),
            PasswordHash = AuthService.HashPassword(dto.Password),
            IsActive = true,
            UserType = dto.UserType,
            TenantId = dto.TenantId,
            ClientId = dto.UserType == 1 ? dto.ClientId : null
        };
        await _userRepository.AddAsync(user, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        if (dto.RoleIds != null && dto.RoleIds.Count > 0)
        {
            var rolesExist = await _roleRepository.FindAsync(r => dto.RoleIds.Contains(r.Id), cancellationToken);
            foreach (var role in rolesExist)
            {
                await _userRoleRepository.AddAsync(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = role.Id
                }, cancellationToken);
            }
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = "UserCreated",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            NewValue = System.Text.Json.JsonSerializer.Serialize(new { user.Email, user.FirstName, user.LastName })
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("AdminUserService", $"User created: {user.Email}", cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<PaginatedResult<AdminUserDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _userRepository.GetPagedAsync(page, pageSize, null, cancellationToken);
        var users = items.ToList();
        var userIds = users.Select(u => u.Id).ToList();
        var userRoles = userIds.Count > 0
            ? (await _userRoleRepository.FindAsync(ur => userIds.Contains(ur.UserId), cancellationToken)).ToList()
            : new List<UserRole>();
        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roles = roleIds.Count > 0
            ? (await _roleRepository.FindAsync(r => roleIds.Contains(r.Id), cancellationToken)).ToDictionary(r => r.Id, r => r.Name)
            : new Dictionary<Guid, string>();
        var rolesByUser = userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => roles.GetValueOrDefault(ur.RoleId)).Where(n => n != null).Cast<string>().ToList());

        var dtos = users.Select(u => MapToDto(u, rolesByUser.GetValueOrDefault(u.Id, new List<string>()))).ToList();
        return new PaginatedResult<AdminUserDto>
        {
            Items = dtos,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<AdminUserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null) return null;
        var userRoles = await _userRoleRepository.FindAsync(ur => ur.UserId == id, cancellationToken);
        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roleNames = roleIds.Count > 0
            ? (await _roleRepository.FindAsync(r => roleIds.Contains(r.Id), cancellationToken)).Select(r => r.Name).ToList()
            : new List<string>();
        return MapToDto(user, roleNames);
    }

    public async Task<AdminUserDto?> UpdateAsync(Guid id, AdminUserUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null) return null;

        if (dto.UserType != 0 && dto.UserType != 1)
            throw new InvalidOperationException("UserType must be 0 (general client) or 1 (accountant).");
        if (dto.UserType == 1 && !dto.ClientId.HasValue)
            throw new InvalidOperationException("Client is required for accountant users (UserType = 1).");

        var oldValue = System.Text.Json.JsonSerializer.Serialize(new { user.FirstName, user.LastName, user.PhoneNumber, user.TenantId, user.UserType, user.ClientId });
        user.FirstName = dto.FirstName.Trim();
        user.LastName = (dto.LastName ?? "").Trim();
        user.PhoneNumber = dto.PhoneNumber?.Trim();
        user.TenantId = dto.TenantId;
        user.UserType = dto.UserType;
        user.ClientId = dto.UserType == 0 ? null : dto.ClientId;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        var newValue = System.Text.Json.JsonSerializer.Serialize(new { user.FirstName, user.LastName, user.PhoneNumber, user.TenantId, user.UserType, user.ClientId });
        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            UserId = id,
            TenantId = user.TenantId,
            Action = "UserUpdated",
            EntityType = "User",
            EntityId = id.ToString(),
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("AdminUserService", $"User updated: {user.Email}", cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto?> DisableAsync(Guid id, AdminUserDisableDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null) return null;

        var previous = user.IsActive;
        user.IsActive = dto.IsActive;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            UserId = id,
            TenantId = user.TenantId,
            Action = "UserDisabled",
            EntityType = "User",
            EntityId = id.ToString(),
            OldValue = previous.ToString(),
            NewValue = dto.IsActive.ToString()
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("AdminUserService", $"User {(dto.IsActive ? "enabled" : "disabled")}: {user.Email}", cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto?> AssignRolesAsync(Guid id, AssignRolesDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null) return null;

        var existing = (await _userRoleRepository.FindAsync(ur => ur.UserId == id, cancellationToken)).ToList();
        foreach (var ur in existing)
            await _userRoleRepository.DeleteAsync(ur, cancellationToken);
        await UnitOfWork.SaveChangesAsync(cancellationToken);

        if (dto.RoleIds != null && dto.RoleIds.Count > 0)
        {
            var rolesExist = await _roleRepository.FindAsync(r => dto.RoleIds.Contains(r.Id), cancellationToken);
            foreach (var role in rolesExist)
            {
                await _userRoleRepository.AddAsync(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = id,
                    RoleId = role.Id
                }, cancellationToken);
            }
            await UnitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _auditLogService.WriteAsync(new AuditLogEntryDto
        {
            UserId = id,
            TenantId = user.TenantId,
            Action = "UserRolesAssigned",
            EntityType = "User",
            EntityId = id.ToString(),
            NewValue = System.Text.Json.JsonSerializer.Serialize(dto.RoleIds ?? new List<Guid>())
        }, cancellationToken);
        await _adminLogging.LogAdminActionAsync("AdminUserService", $"Roles assigned for user: {user.Email}", cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    private static AdminUserDto MapToDto(User u, List<string> roles)
    {
        return new AdminUserDto
        {
            Id = u.Id,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            UserType = u.UserType,
            TenantId = u.TenantId,
            ClientId = u.ClientId,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt,
            Roles = roles
        };
    }
}
