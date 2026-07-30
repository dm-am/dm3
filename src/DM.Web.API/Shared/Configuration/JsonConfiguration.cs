using System.Text.Json;
using System.Text.Json.Serialization;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Binding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Configuration of API JSON serializer
/// </summary>
public static class JsonConfiguration
{
    /// <summary>
    /// Setup JSON API options
    /// </summary>
    /// <param name="config"></param>
    /// <param name="httpContextAccessor"></param>
    /// <param name="bbParserProvider"></param>
    public static void Setup(this JsonOptions config,
        IHttpContextAccessor httpContextAccessor,
        IBbParserProvider bbParserProvider)
    {
        config.JsonSerializerOptions.ApplyApiConventions();

        config.JsonSerializerOptions.Converters.Insert(0, new OptionalConverterFactory());
        config.JsonSerializerOptions.Converters.Insert(0, new BbConverterFactory(httpContextAccessor, bbParserProvider));
        config.JsonSerializerOptions.Converters.Insert(0, new ReadableGuidConverter());
        config.JsonSerializerOptions.Converters.Insert(0, new ReadableNullableGuidConverter());
    }

    /// <summary>
    /// The conventions every JSON this API writes obeys, whatever carries it.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="Setup"/> because MVC is not the only writer. The
    /// SignalR hub sends the very same Notification the notification list returns,
    /// and the hub protocol has serializer options of its own. Left at their
    /// defaults they camel-case the members and write the event as its number, so
    /// one notification reached one open tab as "NewMessage" over REST and as 11
    /// over the socket, and a screen matched on whichever of the two it happened to
    /// be written against.
    ///
    /// The MVC-only converters stay in <see cref="Setup"/>: they need an
    /// HttpContext to resolve the reader, and nothing the hub sends carries a
    /// BbText or an Optional.
    /// </remarks>
    /// <param name="options">Serializer options to configure.</param>
    public static JsonSerializerOptions ApplyApiConventions(this JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;

        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

        options.Converters.Insert(0, new JsonStringEnumConverter());

        return options;
    }
}