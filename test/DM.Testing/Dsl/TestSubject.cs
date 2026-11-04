using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Testing.Dsl;

/// <summary>
/// A reader, for the rules that ask who is looking.
/// </summary>
/// <remarks>One declaration, so a property added to the contract is answered once.</remarks>
public sealed class TestSubject : IAuthorizationSubject
{
    /// <inheritdoc />
    public Guid UserId { get; init; }

    /// <inheritdoc />
    public UserRole Role { get; init; }

    /// <inheritdoc />
    public bool IsAuthenticated { get; init; }

    /// <inheritdoc />
    public AccessPolicy AccessPolicy { get; init; }
}
