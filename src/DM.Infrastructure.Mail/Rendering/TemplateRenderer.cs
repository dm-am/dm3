using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DM.Infrastructure.Mail.Rendering;

/// <inheritdoc />
/// <remarks>
/// Not disposable: the HtmlRenderer it holds is a singleton the container owns.
/// </remarks>
internal class TemplateRenderer : ITemplateRenderer
{
    private readonly HtmlRenderer _htmlRenderer;
    private readonly ImmutableDictionary<Type, Type> _templateTypes;

    public TemplateRenderer(HtmlRenderer htmlRenderer)
    {
        _htmlRenderer = htmlRenderer;
        _templateTypes = GetAvailableComponents();
    }

    public static ImmutableDictionary<Type, Type> GetAvailableComponents()
    {
        return GetTemplatesByModel(Assembly.GetExecutingAssembly().GetTypes())
            .ToImmutableDictionary();
    }

    private static IEnumerable<KeyValuePair<Type, Type>> GetTemplatesByModel(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || !type.IsAssignableTo(typeof(IComponent)))
            {
                continue;
            }

            var parameters = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
                .ToImmutableArray();

            // Not "> 1": a parameterless component must be skipped, not fail the constructor.
            if (parameters.Length != 1)
            {
                continue;
            }

            var parameter = parameters[0];

            if (parameter.Name != "Model")
            {
                continue;
            }

            yield return KeyValuePair.Create(parameter.PropertyType, type);
        }
    }

    /// <inheritdoc cref="ITemplateRenderer.RenderAsync{TModel}"/>
    /// <exception cref="TemplateRenderException">A model with no template</exception>
    /// <remarks>
    /// A missing template throws rather than falls back: a fallback letter reaches the reader broken.
    /// </remarks>
    public Task<string> RenderAsync<TModel>(TModel model)
    {
        if (!_templateTypes.TryGetValue(typeof(TModel), out var templateType))
        {
            throw new TemplateRenderException(
                $"No email template for {typeof(TModel).FullName}");
        }

        return _htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await _htmlRenderer.RenderComponentAsync(
                templateType,
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    { "Model", model }
                }));

            return output.ToHtmlString();
        });
    }
}
