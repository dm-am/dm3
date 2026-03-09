using System;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <inheritdoc />
internal class UpdateBuilderFactory : IUpdateBuilderFactory
{
    /// <inheritdoc />
    public IUpdateBuilder<TEntity> Create<TEntity>(Guid id) where TEntity : class, new()
    {
        return new UpdateBuilder<TEntity>(id);
    }
}
