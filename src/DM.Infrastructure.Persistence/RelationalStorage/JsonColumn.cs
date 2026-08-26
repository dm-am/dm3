using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DM.Infrastructure.Persistence.RelationalStorage;

/// <summary>
/// One spelling of "this property is a document": the serializer options, the
/// conversion and the column type in a single place, so the columns that came
/// out of the document store cannot drift apart in how they read their own
/// bytes.
/// </summary>
internal static class JsonColumn
{
    /// <summary>
    /// The options every jsonb column reads and writes with. Deliberately the
    /// serializer defaults: property names as declared, enums as numbers —
    /// the shape the documents always had. Polymorphism is declared on the
    /// types themselves ([JsonPolymorphic] on AttributeConstraints), because
    /// the discriminator is part of the stored shape, not of the transport.
    /// </summary>
    public static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;

    /// <summary>
    /// Maps the property to a jsonb column through the shared options.
    /// </summary>
    /// <remarks>
    /// The column type is set only on PostgreSQL: the in-memory provider of the
    /// unit tier carries the same conversion and no column at all. The comparer
    /// goes through the serialized form, because these are mutable object
    /// graphs and reference equality would miss an in-place edit.
    /// </remarks>
    public static PropertyBuilder<TProperty> IsJson<TProperty>(
        this PropertyBuilder<TProperty> property, bool isPostgres)
    {
        property.HasConversion(
            value => JsonSerializer.Serialize(value, Options),
            stored => JsonSerializer.Deserialize<TProperty>(stored, Options)!,
            new ValueComparer<TProperty>(
                (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
                value => JsonSerializer.Serialize(value, Options).GetHashCode(),
                value => JsonSerializer.Deserialize<TProperty>(JsonSerializer.Serialize(value, Options), Options)!));

        if (isPostgres)
        {
            property.HasColumnType("jsonb");
        }

        return property;
    }
}
