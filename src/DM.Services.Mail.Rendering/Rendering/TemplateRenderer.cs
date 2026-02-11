using Autofac;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DM.Services.Mail.Rendering.Rendering;

/// <inheritdoc />
internal class TemplateRenderer : ITemplateRenderer, IRenderer, IAsyncDisposable
{
    private readonly ILogger<TemplateRenderer> _logger;
    private readonly HtmlRenderer _htmlRenderer;
    private readonly ImmutableDictionary<Type, Type> _templateTypes;

    public TemplateRenderer(
        ILogger<TemplateRenderer> logger,
        HtmlRenderer htmlRenderer)
    {
        _logger = logger;
        _htmlRenderer = htmlRenderer;
        _templateTypes = GetAvailableComponents();
    }

    public static ImmutableDictionary<Type, Type> GetAvailableComponents()
    {
        return GetPropertyTypes(Assembly.GetExecutingAssembly().GetTypes())
            .ToImmutableDictionary();
    }

    public static IEnumerable<KeyValuePair<Type, Type>> GetPropertyTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || !type.IsAssignableTo<IComponent>())
            {
                continue;
            }

            var parameters = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
                .ToImmutableArray();

            if (parameters.Length > 1)
            {
                continue;
            }

            var parameter = parameters.Single();

            if (parameter.Name != "Model")
            {
                continue;
            }

            yield return KeyValuePair.Create(parameter.PropertyType, type);
        }
    }


    public ValueTask DisposeAsync()
    {
        return _htmlRenderer.DisposeAsync();
    }

    /// <inheritdoc />
    public Task<string> Render<TModel>(TModel model)
    {
        var haveTemplateType = _templateTypes.TryGetValue(typeof(TModel), out var templateType);

        if (!haveTemplateType)
        {
            return Task.FromResult(JsonSerializer.Serialize(model));
        }

        return _htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await _htmlRenderer.RenderComponentAsync(
                templateType!,
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    { "Model", model }
                }));

            return output.ToHtmlString();
        });
    }
}