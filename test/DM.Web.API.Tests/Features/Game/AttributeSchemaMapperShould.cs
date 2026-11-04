using System;
using DM.Web.API.Features.Game.AttributeSchemas;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// An unsaved specification carries no identifier: the domain reads null as "a new one", and a zero Guid
/// from the wire must not arrive as an identifier.
/// </summary>
public class AttributeSchemaMapperShould
{
    private static readonly AttributeSchemaMapper Mapper = new();

    [Fact]
    public void TranslateAZeroSpecificationIdIntoNull() =>
        Mapper.ToUpdateSpecification(new AttributeSpecification { Id = Guid.Empty, Title = "Новый" })
            .Id.Should().BeNull();

    [Fact]
    public void KeepAStoredSpecificationId()
    {
        var stored = Guid.Parse("d10a2b3c-0000-4000-8000-000000000001");

        Mapper.ToUpdateSpecification(new AttributeSpecification { Id = stored, Title = "Старый" })
            .Id.Should().Be(stored);
    }
}
