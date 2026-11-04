using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using Xunit;
using DbSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DtoSpecification = DM.Domain.Game.Features.AttributeSchemas.AttributeSpecification;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

/// <summary>
/// An attribute the master left without a maximum length comes back without one.
/// </summary>
/// <remarks>
/// A stored zero reads back as a cap of zero characters. Over the document conversion every store of
/// specifications goes through, asserted on the specification that comes back.
/// </remarks>
public class AttributeSchemaMaxLengthShould
{
    private static DtoSpecification RoundTrip(AttributeSpecificationType type, int? maxLength) =>
        AttributeSpecificationDocument.ToSpecifications(
        [
            new DbSpecification
            {
                Id = Guid.NewGuid(),
                Title = "Race",
                Order = 0,
                Constraints = AttributeSpecificationDocument.BuildConstraints(
                    new CreateAttributeSpecification
                    {
                        Title = "Race",
                        Type = type,
                        MaxLength = maxLength
                    },
                    required: false)
            }
        ]).Single();

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.Number)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void StayAbsentWhenTheMasterSetNone(AttributeSpecificationType type) =>
        RoundTrip(type, null).MaxLength.Should().BeNull(
            "an empty limit means no limit; a zero means an attribute nobody can fill");

    [Theory]
    [InlineData(AttributeSpecificationType.Text)]
    [InlineData(AttributeSpecificationType.Number)]
    [InlineData(AttributeSpecificationType.BbCode)]
    public void KeepTheNumberTheMasterSet(AttributeSpecificationType type) =>
        RoundTrip(type, 30).MaxLength.Should().Be(30,
            "the round trip has to carry a limit that exists as faithfully as one that does not");
}
