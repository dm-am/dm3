using System.Threading.Tasks;
using DM.Domain.Core.Mail;

namespace DM.Infrastructure.Mail.Rendering;

/// <summary>
/// Razor template renderer (internal implementation contract)
/// </summary>
/// <remarks>
/// DEPRECATED: Use <see cref="ITemplateRenderer"/> from Domain.Core.Mail instead.
/// This interface will be removed after migration is complete.
/// </remarks>
internal interface IRenderer
{
    /// <summary>
    /// Render template against given model
    /// </summary>
    /// <param name="model">Model</param>
    /// <typeparam name="TModel">Model type</typeparam>
    /// <returns>Rendered template</returns>
    Task<string> Render<TModel>(TModel model);
}
