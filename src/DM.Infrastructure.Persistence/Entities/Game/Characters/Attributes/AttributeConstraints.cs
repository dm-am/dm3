using System.Text.Json.Serialization;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

/// <summary>
/// Base DAL model for attribute constraints
/// </summary>
/// <remarks>
/// The hierarchy is stored inside the schema's jsonb column, so the serialized
/// form has to say which subclass a node is. The discriminator names mirror the
/// class names the Bson serializer used to store, and the round trip of every
/// subclass is pinned by a test — the compiler checks none of this.
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

    /// <summary>
    /// Get default attribute value
    /// </summary>
    /// <returns>Default value as string</returns>
    public abstract string GetDefaultValue();
}
