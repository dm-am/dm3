namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// One declaration, because creation and editing must agree.
/// </summary>
internal static class AttributeSchemaFieldLimits
{
    /// <summary>Longest title of a schema or of one specification in it.</summary>
    public const int TitleMaxLength = 100;
}
