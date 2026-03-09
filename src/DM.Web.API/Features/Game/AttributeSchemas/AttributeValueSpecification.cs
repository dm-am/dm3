namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// DTO model for possible attribute value
/// </summary>
public class AttributeValueSpecification
{
    /// <summary>
    /// Value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Value modifier
    /// </summary>
    public int? Modifier { get; set; }
}
