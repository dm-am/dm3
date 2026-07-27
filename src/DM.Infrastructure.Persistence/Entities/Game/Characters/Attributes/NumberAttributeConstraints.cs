namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute constraints for a number in range
/// </summary>
public class NumberAttributeConstraints : AttributeConstraints
{
    /// <summary>
    /// Maximum number of digits (no cap if null)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <inheritdoc />
    public override string GetDefaultValue() => "0";
}
