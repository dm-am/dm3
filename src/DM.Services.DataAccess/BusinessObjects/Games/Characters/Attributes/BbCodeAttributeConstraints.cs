namespace DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes;

/// <summary>
/// DAL model for attribute constraints for BBCode formatted text
/// </summary>
public class BbCodeAttributeConstraints : AttributeConstraints
{
    /// <inheritdoc />
    public override string GetDefaultValue() => string.Empty;
}
