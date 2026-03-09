using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DM.Infrastructure.Persistence.Design;

/// <inheritdoc />
internal class DmDbContextFactory : IDesignTimeDbContextFactory<DmDbContext>
{
    /// <inheritdoc />
    public DmDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DM_ConnectionStrings__Rdb") ??
                               "Host=localhost;Database=dm3_dev;Username=postgres;Password=postgres";
        return new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql(connectionString)
            .Options);
    }
}
