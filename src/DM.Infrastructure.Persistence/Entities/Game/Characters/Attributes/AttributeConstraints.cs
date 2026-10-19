using System.Text.Json.Serialization;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// Base DAL model for attribute constraints
/// </summary>
/// <remarks>
/// The hierarchy is stored inside the schema's jsonb column, so the serialized
/// form says which subclass a node is; a test pins the round trip of every subclass.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_t")]
[JsonDerivedType(typeof(NumberAttributeConstraints), "NumberAttributeConstraints")]
[JsonDerivedType(typeof(StringAttributeConstraints), "StringAttributeConstraints")]
[JsonDerivedType(typeof(ListAttributeConstraints), "ListAttributeConstraints")]
[JsonDerivedType(typeof(BbCodeAttributeConstraints), "BbCodeAttributeConstraints")]
public abstract class AttributeConstraints
{
    /// <summary>
    /// Attribute value requirement flag
    /// </summary>
    public bool Required { get; set; }
}
