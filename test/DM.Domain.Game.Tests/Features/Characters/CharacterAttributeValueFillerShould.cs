using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CharacterAttributeValueFillerShould : UnitTestBase
{
    private readonly IAttributeSchemaService _schemaService;
    private readonly IAttributeValueValidator _validator;
    private readonly CharacterAttributeValueFiller _filler;

    private readonly Guid _schemaId = Guid.NewGuid();
    private readonly Guid _masterId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _visibleSpecId = Guid.NewGuid();
    private readonly Guid _hiddenSpecId = Guid.NewGuid();

    public CharacterAttributeValueFillerShould()
    {
        _schemaService = Mock<IAttributeSchemaService>();
        _validator = Mock<IAttributeValueValidator>();
        _validator.Validate(Arg.Any<string>(), Arg.Any<AttributeSpecification>()).Returns((true, (string?)null));

        _filler = new CharacterAttributeValueFiller(_schemaService, _validator);
    }

    private GameDto Game() => new()
    {
        Id = Guid.NewGuid(),
        AttributeSchemaId = _schemaId,
        Master = new GeneralUser { UserId = _masterId, Username = "Master" },
        Assistants = [],
        Players = []
    };

    private void SetupSchema(params AttributeSpecification[] specifications) =>
        _schemaService.GetAsync(_schemaId)
            .Returns(new AttributeSchema { Id = _schemaId, Specifications = specifications });

    private Character OwnedCharacter(params CharacterAttribute[] attributes) => new()
    {
        Id = Guid.NewGuid(),
        Author = new GeneralUser { UserId = _ownerId, Username = "Owner" },
        Attributes = attributes
    };

    [Fact]
    public async Task RedactHiddenValuesFromStranger()
    {
        SetupSchema(
            new AttributeSpecification { Id = _visibleSpecId, Title = "Name", Type = AttributeSpecificationType.Text },
            new AttributeSpecification { Id = _hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(
            new CharacterAttribute { Id = _visibleSpecId, Value = "Bob" },
            new CharacterAttribute { Id = _hiddenSpecId, Value = "classified" });

        await _filler.Fill(new[] { character }, Game(), Guid.NewGuid());

        character.Attributes.Should().ContainSingle(a => a.Id == _visibleSpecId);
        character.Attributes.Should().NotContain(a => a.Id == _hiddenSpecId);
    }

    [Fact]
    public async Task ShowHiddenValuesToOwner()
    {
        SetupSchema(
            new AttributeSpecification { Id = _hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(new CharacterAttribute { Id = _hiddenSpecId, Value = "classified" });

        await _filler.Fill(new[] { character }, Game(), _ownerId);

        character.Attributes.Should().ContainSingle(a => a.Id == _hiddenSpecId && a.Value == "classified");
    }

    [Fact]
    public async Task ShowHiddenValuesToMaster()
    {
        SetupSchema(
            new AttributeSpecification { Id = _hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(new CharacterAttribute { Id = _hiddenSpecId, Value = "classified" });

        await _filler.Fill(new[] { character }, Game(), _masterId);

        character.Attributes.Should().ContainSingle(a => a.Id == _hiddenSpecId && a.Value == "classified");
    }

    [Fact]
    public async Task RedactHiddenValuesFromMentor()
    {
        var mentorId = Guid.NewGuid();
        var game = Game();
        game.Mentor = new GeneralUser { UserId = mentorId, Username = "Mentor" };
        SetupSchema(
            new AttributeSpecification { Id = _hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(new CharacterAttribute { Id = _hiddenSpecId, Value = "classified" });

        await _filler.Fill(new[] { character }, game, mentorId);

        character.Attributes.Should().NotContain(a => a.Id == _hiddenSpecId);
    }

    [Fact]
    public async Task DeriveModifierForListSubtypes()
    {
        SetupSchema(new AttributeSpecification
        {
            Id = _visibleSpecId,
            Title = "Strength",
            Type = AttributeSpecificationType.TextNumberList,
            Values = new[]
            {
                new ListValue { Value = "Weak", Modifier = -2 },
                new ListValue { Value = "Strong", Modifier = 3 }
            }
        });
        var character = OwnedCharacter(new CharacterAttribute { Id = _visibleSpecId, Value = "Strong" });

        await _filler.Fill(new[] { character }, Game(), _masterId);

        character.Attributes.Single().Modifier.Should().Be(3);
    }

    [Fact]
    public async Task CarryBbCodeTypeThroughToFilledAttribute()
    {
        SetupSchema(new AttributeSpecification
        {
            Id = _visibleSpecId,
            Title = "Biography",
            Type = AttributeSpecificationType.BbCode
        });
        var character = OwnedCharacter(new CharacterAttribute { Id = _visibleSpecId, Value = "[b]hi[/b]" });

        await _filler.Fill(new[] { character }, Game(), _masterId);

        var filled = character.Attributes.Single();
        filled.Type.Should().Be(AttributeSpecificationType.BbCode);
        filled.Value.Should().Be("[b]hi[/b]");
    }
}
