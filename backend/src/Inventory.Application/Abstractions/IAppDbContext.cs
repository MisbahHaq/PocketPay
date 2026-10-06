using Inventory.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Inventory.Application.Abstractions;

/// <summary>
/// The set of tables this feature set can read and write.
/// </summary>
/// <remarks>
/// Handlers depend on this interface, never on <c>InventoryDbContext</c>, so the
/// dependency arrow points inward and a unit test can substitute a fake. Entity
/// configuration lives in Infrastructure; nothing here knows how a table is mapped.
/// </remarks>
public interface IAppDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>
    /// Escape hatch for Infrastructure-owned infrastructure concerns (migrations,
    /// transaction control, provider inspection) without leaking the concrete context type.
    /// </summary>
    DatabaseFacade Database { get; }
}