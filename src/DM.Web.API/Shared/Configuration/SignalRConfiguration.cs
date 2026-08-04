using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Registration of the realtime hub.
/// </summary>
public static class SignalRConfiguration
{
    /// <summary>
    /// Registers SignalR on the JSON contract the rest of the API answers in.
    /// </summary>
    /// <remarks>
    /// A bare AddSignalR leaves the hub on the protocol defaults, which write an
    /// enum as its number. The hub and the notification list carry the same DTO to
    /// the same open tab, so the tab was handed two spellings of one event and
    /// matched on whichever one it happened to be written against. There is no
    /// second contract to choose between: the API answers every other enum by name
    /// already, and it is the hub that has to come to it.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddDmSignalR(this IServiceCollection services)
    {
        services
            .AddSignalR()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.ApplyApiConventions());

        return services;
    }
}
