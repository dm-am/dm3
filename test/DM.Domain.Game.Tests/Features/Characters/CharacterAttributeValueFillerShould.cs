using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.AttributeSchemas;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

public class CharacterAttributeValueFillerShould : UnitTestBase
{
    private readonly IGameAttributeSchemaRepository gameSchemas;
    private readonly IAttributeValueValidator validator;
    private readonly CharacterAttributeValueFiller filler;

    private readonly Guid gameId = Guid.NewGuid();
    private readonly Guid masterId = Guid.NewGuid();
    private readonly Guid ownerId = Guid.NewGuid();
    private readonly Guid visibleSpecId = Guid.NewGuid();
    private readonly Guid hiddenSpecId = Guid.NewGuid();

    public CharacterAttributeValueFillerShould()
    {
        gameSchemas = Mock<IGameAttributeSchemaRepository>();
        validator = Mock<IAttributeValueValidator>();
        validator.Validate(Arg.Any<string>(), Arg.Any<AttributeSpecification>()).Returns((true, (string?)null));

        filler = new CharacterAttributeValueFiller(gameSchemas, validator);
    }

    private GameDto Game() => new()
    {
        Id = gameId,
        Master = new GeneralUser { UserId = masterId, Username = "Master" },
        Assistants = [],
        Players = []
    };

    private void SetupSchema(params AttributeSpecification[] specifications) =>
        gameSchemas.Get(gameId)
            .Returns(new GameAttributeSchema { Specifications = specifications });

    private Character OwnedCharacter(params CharacterAttribute[] attributes) => new()
    {
        Id = Guid.NewGuid(),
        Author = new GeneralUser { UserId = ownerId, Username = "Owner" },
        Attributes = attributes
    };

    /// <summary>
    /// A schema whose descriptor is hidden, written around the validator that refuses one,
    /// and a character that fills it.
    /// </summary>
    private Character WithHiddenDescriptor()
    {
        SetupSchema(new AttributeSpecification
        {
            Id = hiddenSpecId,
            Title = "Class",
            Type = AttributeSpecificationType.Text,
            IsDescriptor = true,
            IsHidden = true
        });
        return OwnedCharacter(new CharacterAttribute { Id = hiddenSpecId, Value = "Assassin" });
    }

    [Fact]
    public async Task RedactHiddenValuesFromStranger()
    {
        SetupSchema(
            new AttributeSpecification { Id = visibleSpecId, Title = "Name", Type = AttributeSpecificationType.Text },
            new AttributeSpecification { Id = hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(
            new CharacterAttribute { Id = visibleSpecId, Value = "Bob" },
            new CharacterAttribute { Id = hiddenSpecId, Value = "classified" });

        await filler.Fill(new[] { character }, Game(), Guid.NewGuid());

        character.Attributes.Should().ContainSingle(a => a.Id == visibleSpecId);
        character.Attributes.Should().NotContain(a => a.Id == hiddenSpecId);
    }

    [Fact]
    public async Task ShowHiddenValuesToOwner()
    {
        SetupSchema(
            new AttributeSpecification { Id = hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(new CharacterAttribute { Id = hiddenSpecId, Value = "classified" });

        await filler.Fill(new[] { character }, Game(), ownerId);

        character.Attributes.Should().ContainSingle(a => a.Id == hiddenSpecId && a.Value == "classified");
    }

    [Fact]
    public async Task ShowHiddenValuesToMaster()
    {
        SetupSchema(
            new AttributeSpecification { Id = hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(new CharacterAttribute { Id = hiddenSpecId, Value = "classified" });

        await filler.Fill(new[] { character }, Game(), masterId);

        character.Attributes.Should().ContainSingle(a => a.Id == hiddenSpecId && a.Value == "classified");
    }

    [Fact]
    public async Task RedactHiddenValuesFromMentor()
    {
        var mentorId = Guid.NewGuid();
        var game = Game();
        game.Mentor = new GeneralUser { UserId = mentorId, Username = "Mentor" };
        SetupSchema(
            new AttributeSpecification { Id = hiddenSpecId, Title = "Secret", Type = AttributeSpecificationType.Text, IsHidden = true });
        var character = OwnedCharacter(new CharacterAttribute { Id = hiddenSpecId, Value = "classified" });

        await filler.Fill(new[] { character }, game, mentorId);

        character.Attributes.Should().NotContain(a => a.Id == hiddenSpecId);
    }

    [Fact]
    public async Task ShowTheDescriptorToStranger()
    {
        SetupSchema(new AttributeSpecification
        {
            Id = visibleSpecId,
            Title = "Class",
            Type = AttributeSpecificationType.Text,
            IsDescriptor = true
        });
        var character = OwnedCharacter(new CharacterAttribute { Id = visibleSpecId, Value = "Warrior" });

        await filler.Fill(new[] { character }, Game(), Guid.NewGuid());

        character.Descriptor.Should().Be("Warrior");
    }

    [Fact]
    public async Task RedactHiddenDescriptorFromStranger()
    {
        var character = WithHiddenDescriptor();

        await filler.Fill(new[] { character }, Game(), Guid.NewGuid());

        character.Descriptor.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowHiddenDescriptorToOwnerAndMaster(bool viewerIsOwner)
    {
        var character = WithHiddenDescriptor();

        await filler.Fill(new[] { character }, Game(), viewerIsOwner ? ownerId : masterId);

        character.Descriptor.Should().Be("Assassin");
    }

    [Fact]
    public async Task DeriveModifierForListSubtypes()
    {
        SetupSchema(new AttributeSpecification
        {
            Id = visibleSpecId,
            Title = "Strength",
            Type = AttributeSpecificationType.TextNumberList,
            Values = new[]
            {
                new ListValue { Value = "Weak", Modifier = -2 },
                new ListValue { Value = "Strong", Modifier = 3 }
            }
        });
        var character = OwnedCharacter(new CharacterAttribute { Id = visibleSpecId, Value = "Strong" });

        await filler.Fill(new[] { character }, Game(), masterId);

        character.Attributes.Single().Modifier.Should().Be(3);
    }

    [Fact]
    public async Task CarryBbCodeTypeThroughToFilledAttribute()
    {
        SetupSchema(new AttributeSpecification
        {
            Id = visibleSpecId,
            Title = "Biography",
            Type = AttributeSpecificationType.BbCode
        });
        var character = OwnedCharacter(new CharacterAttribute { Id = visibleSpecId, Value = "[b]hi[/b]" });

        await filler.Fill(new[] { character }, Game(), masterId);

        var filled = character.Attributes.Single();
        filled.Type.Should().Be(AttributeSpecificationType.BbCode);
        filled.Value.Should().Be("[b]hi[/b]");
    }
}
