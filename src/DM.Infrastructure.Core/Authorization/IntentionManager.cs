using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Core.Authorization;

/// <inheritdoc />
internal class IntentionManager : IIntentionManager
{
    private readonly IAuthorizationContextProvider _authorizationContextProvider;
    private readonly IEnumerable<IIntentionResolver> _resolvers;
    private readonly ILogger<IntentionManager> _logger;

    /// <inheritdoc />
    public IntentionManager(
        IAuthorizationContextProvider authorizationContextProvider,
        IEnumerable<IIntentionResolver> resolvers,
        ILogger<IntentionManager> logger)
    {
        _authorizationContextProvider = authorizationContextProvider;
        _resolvers = resolvers;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsAllowed<TIntention>(TIntention intention) where TIntention : struct
    {
        var matchingResolver = _resolvers
            .OfType<IIntentionResolver<TIntention>>()
            .FirstOrDefault();
        if (matchingResolver != null)
        {
            return matchingResolver.IsAllowed(_authorizationContextProvider.CurrentSubject, intention);
        }

        _logger.LogError("No matching resolver found for intention type {intentionType}", typeof(TIntention));
        return false;
    }

    /// <inheritdoc />
    public bool IsAllowed<TIntention, TTarget>(TIntention intention, TTarget target)
        where TIntention : struct
    {
        var matchingResolver = _resolvers
            .OfType<IIntentionResolver<TIntention, TTarget>>()
            .FirstOrDefault();
        if (matchingResolver != null)
        {
            return matchingResolver.IsAllowed(_authorizationContextProvider.CurrentSubject, intention, target);
        }

        _logger.LogError(
            "No matching resolver found for intention type {intentionType} and target type {targetType}",
            typeof(TIntention), typeof(TTarget));
        return false;
    }

    /// <inheritdoc />
    public void ThrowIfForbidden<TIntention>(TIntention intention) where TIntention : struct
    {
        if (!IsAllowed(intention))
        {
            throw new IntentionManagerException(_authorizationContextProvider.CurrentSubject, GetIntentionEnum(intention));
        }
    }

    /// <inheritdoc />
    public void ThrowIfForbidden<TIntention, TTarget>(TIntention intention, TTarget target)
        where TIntention : struct
    {
        if (!IsAllowed(intention, target))
        {
            throw new IntentionManagerException(_authorizationContextProvider.CurrentSubject, GetIntentionEnum(intention), target);
        }
    }

    private static Enum GetIntentionEnum<TIntention>(TIntention intention)
    {
        return (Enum)Enum.Parse(typeof(TIntention), intention?.ToString() ?? string.Empty);
    }
}
