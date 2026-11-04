using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.BbRendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DM.Web.API.Swagger;

/// <summary>
/// Extensions for swagger setup
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    /// Every OpenAPI group the API publishes, derived from the controllers
    /// themselves. Public because the contract artifact is produced by iterating
    /// it: a hard-coded list in the generator would drift from the code the
    /// moment a group is added.
    /// </summary>
    public static readonly IReadOnlyList<string> ApiGroups = Assembly.GetExecutingAssembly().GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ControllerBase)))
        .Select(t => t.GetCustomAttribute<ApiExplorerSettingsAttribute>() is { IgnoreApi: false } settings
            ? settings.GroupName
            : null)
        .OfType<string>()
        .Distinct()
        .ToArray();

    public static void ConfigureGen(this SwaggerGenOptions options)
    {
        foreach (var apiGroup in ApiGroups)
        {
            options.SwaggerDoc(apiGroup, new OpenApiInfo { Title = $"DM.API {apiGroup}", Version = "v1" });
        }

        // BFF Pattern: Cookie-based authentication
        options.AddSecurityDefinition(AuthenticationSwaggerFilter.CookieScheme, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = ApiCredentialsStorage.AuthCookieName,
            Description = "Authentication via HttpOnly session cookie. " +
                          "Login using POST /v1/account/login to set the cookie automatically."
        });

        options.OperationFilter<AuthenticationSwaggerFilter>();
        options.OperationFilter<BbAudienceSwaggerFilter>();
        options.OperationFilter<ResponseMediaTypeSwaggerFilter>();
        options.OperationFilter<CreatedLocationSwaggerFilter>();
        options.ParameterFilter<SortVocabularySwaggerFilter>();
        options.SchemaFilter<OptionalSchemaFilter>();

        var apiAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{apiAssemblyName}.xml"));

        options.DescribeAllParametersInCamelCase();

        // Use fully qualified type names to avoid schema conflicts between types with same name
        options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    }

    public static void Configure(this SwaggerOptions options)
    {
        options.RouteTemplate = "swagger/{documentName}/swagger.json";
    }

    public static void ConfigureUi(this SwaggerUIOptions options)
    {
        foreach (var apiGroup in ApiGroups)
        {
            options.SwaggerEndpoint($"swagger/{apiGroup}/swagger.json", apiGroup);
        }

        options.RoutePrefix = string.Empty;
        options.DocumentTitle = "DM.API";
    }
}
