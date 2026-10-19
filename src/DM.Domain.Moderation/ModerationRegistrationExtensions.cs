using DM.Domain.Core.Authorization;
using DM.Domain.Moderation.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DM.Domain.Moderation;

/// <summary>
/// Lifetimes this assembly's types need beyond the assembly scan's default: the resolver
/// is internal, so its own assembly names its lifetime.
/// </summary>
public static class ModerationRegistrationExtensions
{
    public static IServiceCollection AddDmModeration(this IServiceCollection services)
    {
        services.TryAddScoped<IIntentionResolver<ModerationIntention>, ModerationIntentionResolver>();

        return services;
    }
}
