using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using Xunit;
using DbSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DtoSpecification = DM.Domain.Game.Features.Games.AttributeSpecification;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

/// <summary>
/// An attribute the master left without a maximum length comes back without one.
/// </summary>
/// <remarks>
/// The form offers "Без ограничения" and the number and BBCode types honoured it,
/// but the text one had nowhere to put an absence: its stored constraint held a
/// plain int, the write path filled it with zero, and zero read back as a cap of
/// zero characters. After that the server refused every value for the attribute
/// and the browser rendered its field with maxlength="0", so the attribute could
/// not be filled at all. The seeded schema writes 30 by hand, which is the only
/// reason nothing on a developer machine ever showed it.
///
/// Asserted on the specification that comes back rather than on the stored
/// constraint. The number is nullable on both sides of the round trip, so a zero
/// re-entering it anywhere fails here as a value rather than as a build.
/// </remarks>
public class AttributeSchemaMaxLengthShould
{
    private static DtoSpecification RoundTrip(AttributeSpecificationType type, int? maxLength) =>
        new DbSchema
        {
            AttributeSchemaId = Guid.NewGuid(),
            Title = "Схема",
            Specifications =
            [
                new DbSpecification
                {
                    Id = Guid.NewGuid(),
                    Title = "Race",
                    Order = 0,
                    Constraints = AttributeSchemaRepository.BuildConstraints(
                        new CreateAttributeSpecification
                        {
                            Title = "Race",
                            Type = type,
                            MaxLength = maxLength
                        },
                        required: false)
                }
            ]
        }.ToAttributeSchema().Specifications.Single();

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
