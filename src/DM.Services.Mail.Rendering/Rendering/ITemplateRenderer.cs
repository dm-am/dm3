using System.Threading.Tasks;

namespace DM.Services.Mail.Rendering.Rendering;

/// <summary>
/// Razor template renderer
/// </summary>
internal interface ITemplateRenderer
{
    /// <summary>
    /// Render template against model
    /// </summary>
    /// <param name="model">Model</param>
    /// <typeparam name="TModel">Model type</typeparam>
    /// <returns>Rendered template</returns>
    Task<string> Render<TModel>(TModel model);
}