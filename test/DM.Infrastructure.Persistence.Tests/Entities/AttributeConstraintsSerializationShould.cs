using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.RelationalStorage;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// Every constraint type survives the jsonb round trip whole.
/// </summary>
/// <remarks>
/// The hierarchy is stored inside the schema's Specifications column, and the
/// serializer is what decides which subclass a node comes back as: the
/// discriminator lives in the written JSON, not in the compiler's view of the
/// code. A subclass dropped from the [JsonDerivedType] list, or a renamed
/// discriminator, loses fields silently — including "no limit", the absence
/// the number types have to carry — which is why each of the four is pinned
/// end to end, through the same options the column reads and writes with.
/// </remarks>
public class AttributeConstraintsSerializationShould
{
    private static AttributeSpecification RoundTrip(AttributeConstraints constraints)
    {
        var specification = new AttributeSpecification
        {
            Id = Guid.NewGuid(),
            Title = "Раса",
            Order = 3,
            IsDescriptor = true,
            IsHidden = false,
            Constraints = constraints
        };

        var stored = JsonSerializer.Serialize(specification, JsonColumn.Options);
        return JsonSerializer.Deserialize<AttributeSpecification>(stored, JsonColumn.Options)!;
    }

    [Fact]
    public void KeepANumberConstraintAndItsAbsentLimit()
    {
        var read = RoundTrip(new NumberAttributeConstraints { Required = true, MaxLength = null });

        var constraints = read.Constraints.Should().BeOfType<NumberAttributeConstraints>().Subject;
        constraints.Required.Should().BeTrue();
        constraints.MaxLength.Should().BeNull("an absent limit is a value, not a zero");
    }

    [Fact]
    public void KeepAStringConstraint()
    {
        var read = RoundTrip(new StringAttributeConstraints { Required = false, MaxLength = 30 });

        var constraints = read.Constraints.Should().BeOfType<StringAttributeConstraints>().Subject;
        constraints.Required.Should().BeFalse();
        constraints.MaxLength.Should().Be(30);
    }

    [Fact]
    public void KeepABbCodeConstraint()
    {
        var read = RoundTrip(new BbCodeAttributeConstraints { Required = false, MaxLength = 20000 });

        read.Constraints.Should().BeOfType<BbCodeAttributeConstraints>()
            .Which.MaxLength.Should().Be(20000);
    }

    [Fact]
    public void KeepAListConstraintWithItsNestedValues()
    {
        var read = RoundTrip(new ListAttributeConstraints
        {
            Required = true,
            Kind = ListValueKind.TextNumber,
            Values = new List<ListAttributeValue>
            {
                new() { Value = "Хаотичный добрый", Modifier = 2 },
                new() { Value = "Нейтральный", Modifier = null },
            }
        });

        var constraints = read.Constraints.Should().BeOfType<ListAttributeConstraints>().Subject;
        constraints.Kind.Should().Be(ListValueKind.TextNumber);
        constraints.Values.Select(v => (v.Value, v.Modifier)).Should().Equal(
            ("Хаотичный добрый", 2),
            ("Нейтральный", null));
    }

    [Fact]
    public void CarryTheSpecificationFieldsBesideTheConstraint()
    {
        var read = RoundTrip(new StringAttributeConstraints { MaxLength = 10 });

        read.Title.Should().Be("Раса");
        read.Order.Should().Be(3);
        read.IsDescriptor.Should().BeTrue();
        read.IsHidden.Should().BeFalse();
    }
}
