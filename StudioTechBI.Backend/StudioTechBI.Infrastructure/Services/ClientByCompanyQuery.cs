using Microsoft.EntityFrameworkCore;
using StudioTechBI.Application.DTOs.Auth;
using StudioTechBI.Application.Interfaces;
using StudioTechBI.Infrastructure.Data;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>Resolves client for a user by email via User → CompanyUsers → Company → Client (equivalent to the SQL join).</summary>
public class ClientByCompanyQuery : IClientByCompanyQuery
{
    private readonly ApplicationDbContext _context;

    public ClientByCompanyQuery(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClientInfoFromCompanyDto?> GetClientForUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Equivalent to: SELECT c.Id AS ClientId, c.ClientName, c.BlobFolderPath AS ClientCode FROM Users u JOIN CompanyUsers cu ... JOIN Companies co ... JOIN Clients c ON co.ClientId = c.Id WHERE u.Email = @Email
        var result = await (
            from u in _context.Users
            where u.Email == normalizedEmail && !u.IsDeleted
            join cu in _context.CompanyUsers on u.Id equals cu.UserId
            where !cu.IsDeleted
            join co in _context.Companies on cu.CompanyId equals co.Id
            where !co.IsDeleted && co.ClientId != null
            join c in _context.Clients on co.ClientId equals c.Id
            where !c.IsDeleted
            select new ClientInfoFromCompanyDto
            {
                ClientId = c.Id,
                ClientCode = c.ClientCode ?? c.BlobFolderPath ?? "",
                ClientName = c.ClientName
            }
        ).FirstOrDefaultAsync(cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<ClientInfoFromCompanyDto>> GetClientsForUserEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return Array.Empty<ClientInfoFromCompanyDto>();

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var list = await (
            from u in _context.Users
            where u.Email == normalizedEmail && !u.IsDeleted
            join cu in _context.CompanyUsers on u.Id equals cu.UserId
            where !cu.IsDeleted
            join co in _context.Companies on cu.CompanyId equals co.Id
            where !co.IsDeleted && co.ClientId != null
            join c in _context.Clients on co.ClientId equals c.Id
            where !c.IsDeleted
            select new { c.Id, c.ClientCode, c.BlobFolderPath, c.ClientName }
        ).Distinct().ToListAsync(cancellationToken);

        return list.Select(x => new ClientInfoFromCompanyDto
        {
            ClientId = x.Id,
            ClientCode = x.ClientCode ?? x.BlobFolderPath ?? "",
            ClientName = x.ClientName
        }).ToList();
    }
}
