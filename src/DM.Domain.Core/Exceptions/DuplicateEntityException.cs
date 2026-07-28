using System;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// A write lost the race with an identical one: the storage layer rejected it
/// as a duplicate of a row that already exists.
/// </summary>
/// <remarks>
/// Domain services pre-check for duplicates and answer 409 on the normal path;
/// this exists for the narrow window where two concurrent requests both pass
/// that check. Translating the storage engine's error into a domain exception
/// is the repository's job — a domain service that catches PostgresException
/// by SQLSTATE has to know which engine it runs on and which unique index
/// exists, which is exactly what the repository abstraction is there to hide.
/// </remarks>
public class DuplicateEntityException : Exception
{
    /// <inheritdoc />
    public DuplicateEntityException(string message) : base(message)
    {
    }

    /// <inheritdoc />
    public DuplicateEntityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
