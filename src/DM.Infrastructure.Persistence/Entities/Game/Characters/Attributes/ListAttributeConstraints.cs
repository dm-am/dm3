using System.Collections.Generic;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// DAL model for attribute constraints for a list of limited values
/// </summary>
public class ListAttributeConstraints : AttributeConstraints
{
    /// <summary>
    /// Kind of values held by the list
    /// </summary>
    public ListValueKind Kind { get; set; }

    /// <summary>
    /// Possible values
    /// </summary>
    public IEnumerable<ListAttributeValue> Values { get; set; } = [];
}

/// <summary>
/// DAL model for a possible list value
/// </summary>
public class ListAttributeValue
{
    /// <summary>
    /// Value
    /// </summary>
    public string Value { get; set; } = null!;

    public int? Modifier { get; set; }
}
