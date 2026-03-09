namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute constraints for BBCode formatted text
/// </summary>
public class BbCodeAttributeConstraints : AttributeConstraints
{
    /// <inheritdoc />
    public override string GetDefaultValue() => string.Empty;
}
