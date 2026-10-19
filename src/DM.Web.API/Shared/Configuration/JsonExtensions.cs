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
public static class JsonExtensions
{
    /// <summary>
    /// Setup JSON API options
    /// </summary>
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
    /// Apart from <see cref="Setup"/> because the SignalR hub has serializer options
    /// of its own, and at their defaults one notification reaches a tab as
    /// "NewMessage" over REST and as 11 over the socket. The MVC-only converters
    /// need an HttpContext, and nothing the hub sends carries a BbText or an Optional.
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
