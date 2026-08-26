using DM.Domain.Core.Authorization;
using DM.Domain.Moderation.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DM.Domain.Moderation;

/// <summary>
/// Lifetimes this assembly's types need beyond the assembly scan's default.
/// </summary>
/// <remarks>
/// Same reason as the account registrations: the resolver is internal, so the
/// host that wanted it per scope had to name it by namespace string. The
/// requirement lives in the assembly that owns the type.
/// </remarks>
public static class ModerationRegistrationExtensions
{
    /// <summary>
    /// Registers the moderation types whose lifetimes the scan default cannot serve.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmModeration(this IServiceCollection services)
    {
        services.TryAddScoped<IIntentionResolver<ModerationIntention>, ModerationIntentionResolver>();

        return services;
    }
}
