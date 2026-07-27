namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute constraints for BBCode formatted text
/// </summary>
public class BbCodeAttributeConstraints : AttributeConstraints
{
    /// <summary>
    /// Maximum text length (no cap if null)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <inheritdoc />
    public override string GetDefaultValue() => string.Empty;
}
