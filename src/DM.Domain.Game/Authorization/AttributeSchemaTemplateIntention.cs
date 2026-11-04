namespace DM.Domain.Game.Authorization;

public enum AttributeSchemaTemplateIntention
{
    /// <summary>
    /// Create, replace, delete and name the default template. Reading is not
    /// here: a template is open to everyone, like the tag catalogue.
    /// </summary>
    Manage = 0
}
