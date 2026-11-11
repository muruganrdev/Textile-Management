using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

/// <summary>
/// EF Core repository implementation for User entity persistence and queries.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _context.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.Email = NormalizeEmail(user.Email);

        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Users.Update(user);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetEmployeesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Employee)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
