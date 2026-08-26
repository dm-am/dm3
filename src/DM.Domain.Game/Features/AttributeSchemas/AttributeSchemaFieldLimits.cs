namespace DM.Domain.Game.Features.AttributeSchemas;

/// <summary>
/// How long the text fields of an attribute schema may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class AttributeSchemaFieldLimits
{
    /// <summary>Longest title of a schema or of one specification in it.</summary>
    public const int TitleMaxLength = 100;
}
