using Microsoft.EntityFrameworkCore;

namespace WalletPlatform.Infrastructure.Data;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
}
