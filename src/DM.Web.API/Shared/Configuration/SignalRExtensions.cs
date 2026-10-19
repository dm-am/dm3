using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Registration of the realtime hub.
/// </summary>
public static class SignalRExtensions
{
    /// <summary>
    /// Registers SignalR on the JSON contract the rest of the API answers in;
    /// <see cref="JsonExtensions.ApplyApiConventions"/> says why.
    /// </summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddDmSignalR(this IServiceCollection services)
    {
        services
            .AddSignalR()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.ApplyApiConventions());

        return services;
    }
}
