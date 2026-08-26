using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DM.Infrastructure.Core.Tests.Extensions.DefaultTypesFixtures;

public interface IPlainContract;

public class PlainService : IPlainContract;

public interface ISharedContract;

public class FirstShared : ISharedContract;

public class SecondShared : ISharedContract;

/// <summary>Second implementation of a contract the infrastructure assembly introduces.</summary>
public class FixtureCursorService : ICursorService
{
    public bool TryDecode(string cursor, out CursorData data)
    {
        data = null!;
        return false;
    }

    public string CreateAfterCursor(Guid entityId, DateTimeOffset timestampUtc) => string.Empty;

    public string CreateBeforeCursor(Guid entityId, DateTimeOffset timestampUtc) => string.Empty;
}

public class FixtureException : Exception;

public class FixtureAttribute : Attribute;

public class ArrayConstructed(int[] values)
{
    public int[] Values { get; } = values;
}

public class FixtureHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class FixtureDbContext : DbContext;

public class InternallyConstructed
{
    internal InternallyConstructed()
    {
    }
}

public class StringlyConstructed(string value)
{
    public string Value { get; } = value;
}

public record PositionalRecord(Guid Id, int Count);

public class DefaultedConstruction(string name = "default")
{
    public string Name { get; } = name;
}
