namespace DM.Domain.Game.Authorization;

/// <summary>
/// List of attribute schema template actions that require authorization
/// </summary>
public enum AttributeSchemaTemplateIntention
{
    /// <summary>
    /// Keep the catalogue of templates: create, replace, delete, and name the
    /// one new games start from. Reading is not here, because a template is
    /// open to everyone the way the tag catalogue is.
    /// </summary>
    Manage = 0
}
