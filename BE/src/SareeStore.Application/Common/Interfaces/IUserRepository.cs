using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for User entity operations.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetEmployeesAsync(CancellationToken cancellationToken = default);
}
