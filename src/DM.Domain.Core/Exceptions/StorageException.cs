using System;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// The store refused a write for a reason the caller cannot classify further.
/// </summary>
/// <remarks>
/// Callers that have to undo external side effects — an object already put into
/// the bucket, a message already handed to a queue — need to tell "the write did
/// not land" apart from "the code above it is broken". Reading that off the ORM's
/// own exception type would put a persistence dependency in the layer that owns
/// the compensation, which is exactly what the repository abstraction removes;
/// translating the engine's failure is the repository's job.
/// </remarks>
public class StorageException : Exception
{
    /// <inheritdoc />
    public StorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
