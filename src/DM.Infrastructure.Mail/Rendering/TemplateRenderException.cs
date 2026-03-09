using System;

namespace DM.Infrastructure.Mail.Rendering;

/// <summary>
/// Template rendering exception
/// </summary>
public class TemplateRenderException : Exception
{
    /// <summary>
    /// Template rendering exception
    /// </summary>
    public TemplateRenderException(string message) : base(message)
    {
    }

    /// <summary>
    /// Template rendering exception
    /// </summary>
    public TemplateRenderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
