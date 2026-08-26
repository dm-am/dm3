using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Testing.Dsl;

/// <summary>
/// A reader, for the rules that ask who is looking.
/// </summary>
/// <remarks>
/// The interface is four values and no behaviour, so every suite that needed one
/// declared the same four-property class of its own. One declaration, because the
/// next property added to the contract has to be answered once rather than in
/// every copy.
/// </remarks>
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
