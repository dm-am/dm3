using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DM.Testing;

/// <summary>A context over PostgreSQL that never opens its connection: the model and the statement text only.</summary>
public static class ProbeContext
{
    public static DmDbContext Open() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseNpgsql("Host=localhost;Database=probe;Username=probe;Password=probe")
        .Options);

    /// <summary>The design-time model: HasData lives there, the runtime one drops it.</summary>
    public static IModel DesignTimeModel()
    {
        using var context = Open();
        return context.GetService<IDesignTimeModel>().Model;
    }

    public static IReadOnlyCollection<IDictionary<string, object?>> SeedRows<TEntity>() =>
        DesignTimeModel().FindEntityType(typeof(TEntity))!.GetSeedData().ToList();
}
