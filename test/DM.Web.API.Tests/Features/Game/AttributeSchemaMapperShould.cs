using System;
using System.Linq;
using DM.Testing;
using DM.Web.API.Features.Game.AttributeSchemas;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// PATCH carries "not sent" as null, and the write model must keep saying it.
/// AutoMapper said it through AllowNullCollections; a mapper that turns the
/// null into an empty list re-introduces the bug where updating a title alone
/// wiped every specification, because the domain guard reads "empty" as
/// "replace all with nothing".
/// </summary>
public class AttributeSchemaMapperShould : UnitTestBase
{
    private static readonly AttributeSchemaMapper Mapper = new();

    [Fact]
    public void KeepAnUnsentSpecificationListNull()
    {
        var update = Mapper.ToUpdateSchema(new UpdateAttributeSchemaRequest
        {
            Title = "Только имя"
        });

        update.Specifications.Should().BeNull(
            "null means the field was not sent, not \"clear the list\"");
    }

    [Fact]
    public void CarryASentSpecificationListThrough()
    {
        var update = Mapper.ToUpdateSchema(new UpdateAttributeSchemaRequest
        {
            Specifications = new[] { new AttributeSpecification { Title = "Сила" } }
        });

        update.Specifications.Should().ContainSingle()
            .Which.Title.Should().Be("Сила");
    }

    /// <summary>
    /// A zero Guid denotes a brand new specification - no persisted id yet -
    /// and the domain reads null as "create", an id as "update in place".
    /// </summary>
    [Fact]
    public void TranslateAZeroSpecificationIdIntoNull()
    {
        var update = Mapper.ToUpdateSchema(new UpdateAttributeSchemaRequest
        {
            Specifications = new[]
            {
                new AttributeSpecification { Id = Guid.Empty, Title = "Новый" },
                new AttributeSpecification { Id = Guid.Parse("d10a2b3c-0000-4000-8000-000000000001"), Title = "Старый" }
            }
        });

        var specs = update.Specifications!.ToArray();
        specs[0].Id.Should().BeNull();
        specs[1].Id.Should().Be(Guid.Parse("d10a2b3c-0000-4000-8000-000000000001"));
    }
}
