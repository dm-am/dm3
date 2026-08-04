namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute constraints for a string
/// </summary>
public class StringAttributeConstraints : AttributeConstraints
{
    /// <summary>
    /// Maximum string length (no cap if null)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <inheritdoc />
    public override string GetDefaultValue() => string.Empty;
}
