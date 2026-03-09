using System;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <inheritdoc />
internal class UpdateBuilderException : Exception
{
    /// <inheritdoc />
    public UpdateBuilderException(string message) : base(message)
    {
    }
}
