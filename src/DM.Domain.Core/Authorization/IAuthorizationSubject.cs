using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// Minimal interface for authorization checks.
/// Used by IntentionResolvers to check user permissions without exposing sensitive data.
/// </summary>
public interface IAuthorizationSubject
{
    /// <summary>
    /// User identifier
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// User role (hierarchical)
    /// </summary>
    UserRole Role { get; }

    /// <summary>
    /// Whether user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Personal access restriction policy
    /// </summary>
    AccessPolicy AccessPolicy { get; }
}
