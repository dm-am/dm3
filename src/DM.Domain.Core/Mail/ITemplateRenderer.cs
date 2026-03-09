using System.Threading.Tasks;

namespace DM.Domain.Core.Mail;

/// <summary>
/// Template renderer for email content
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Render template against given model
    /// </summary>
    /// <param name="model">Model</param>
    /// <typeparam name="TModel">Model type</typeparam>
    /// <returns>Rendered template</returns>
    Task<string> RenderAsync<TModel>(TModel model);
}
