using System;

namespace DM.Infrastructure.Mail.Rendering;

public class TemplateRenderException : Exception
{
    public TemplateRenderException(string message) : base(message)
    {
    }
}
