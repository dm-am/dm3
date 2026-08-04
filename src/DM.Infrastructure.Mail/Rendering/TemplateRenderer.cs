using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Mail.Rendering;

/// <inheritdoc />
/// <remarks>
/// Deliberately not disposable. The HtmlRenderer it holds is a singleton the
/// container owns, and this type used to dispose it: registered per dependency by
/// the blanket scan, it took the shared renderer down with the first request scope
/// that ended, and every letter after that rendered to an empty string. Nothing
/// noticed because the renderer had no templates and returned JSON without
/// touching it.
/// </remarks>
internal class TemplateRenderer : ITemplateRenderer
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
            if (type.IsAbstract || type.IsInterface || !type.IsAssignableTo(typeof(IComponent)))
            {
                continue;
            }

            var parameters = type.GetProperties()
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
                .ToImmutableArray();

            // Not "> 1": a component with no parameters at all reached Single()
            // and threw, and this runs in the renderer's constructor — so one
            // parameterless component anywhere in the assembly took out every
            // letter the product sends.
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
    /// A missing template throws rather than falling back. The fallback used to
    /// serialize the model to JSON and hand it back as the letter body, and since
    /// the project shipped with no templates at all, that is what every account
    /// letter contained: a registering user received
    /// {"ConfirmationLinkUrl":"..."}. Nothing failed, nothing was logged, and no
    /// test looked, so it survived. Loud is the only safe direction here.
    /// </remarks>
    public Task<string> RenderAsync<TModel>(TModel model)
    {
        if (!_templateTypes.TryGetValue(typeof(TModel), out var templateType))
        {
            _logger.LogError("No email template for {Model}", typeof(TModel).FullName);
            throw new TemplateRenderException(
                $"No email template for {typeof(TModel).FullName}");
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
