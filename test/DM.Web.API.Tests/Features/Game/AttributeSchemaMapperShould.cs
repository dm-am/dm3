using System;
using DM.Testing;
using DM.Web.API.Features.Game.AttributeSchemas;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// A specification the caller has never saved carries no identifier, and the
/// write path has to keep saying so. On the wire an unsent identifier is a zero
/// Guid; the domain reads null as "hand me a new one" and an identifier as "keep
/// this one if the stored set still holds it", so a zero arriving as an
/// identifier would ask the server to preserve a specification nobody stored.
/// </summary>
public class AttributeSchemaMapperShould : UnitTestBase
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
