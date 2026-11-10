using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using AwesomeAssertions;
using Xunit;
using DtoCharacter = DM.Domain.Game.Features.Characters.Character;
using DtoCharacterAttribute = DM.Domain.Game.Features.Characters.CharacterAttribute;

namespace DM.Web.API.Tests.Features.Game;

public class CharacterMapperShould : UnitTestBase
{
    private static CharacterMapper CreateMapper() =>
        new(new UserMapper());

    // PATCH carries "not sent" as null and the write model keeps saying it: the destination flags
    // are nullable.
    [Fact]
    public void KeepAnUnsentPrivacyBlockNull()
    {
        var update = CharacterMapper.ToUpdateCharacter(new UpdateCharacterRequest
        {
            Name = "Гримли"
        });

        update.Name.Should().Be("Гримли");
        update.IsNpc.Should().BeNull("an absent privacy block must not demote an NPC");
        update.AccessPolicy.Should().BeNull();
    }

    [Fact]
    public void FoldASentPrivacyBlockIntoTheAccessPolicy()
    {
        var update = CharacterMapper.ToUpdateCharacter(new UpdateCharacterRequest
        {
            Privacy = new CharacterPrivacySettings
            {
                IsNpc = true,
                EditByMaster = true,
                EditPostByMaster = false
            }
        });

        update.IsNpc.Should().BeTrue();
        update.AccessPolicy.Should().Be(CharacterAccessPolicy.EditAllowed);
    }

    // The raw stored BBCode is never placed into the plain Value (an HTML sink); it travels as a
    // server-rendered InfoBbText instead.
    [Fact]
    public void CarryABbCodeAttributeAsRenderedTextOnly()
    {
        var details = CreateMapper().ToCharacterDetails(new DtoCharacter
        {
            Id = Guid.NewGuid(),
            Name = "Гримли",
            Attributes =
            [
                new DtoCharacterAttribute
                {
                    Id = Guid.NewGuid(),
                    Title = "История",
                    Value = "[b]жирный[/b]",
                    Type = AttributeSpecificationType.BbCode
                }
            ]
        });

        var attribute = details.Attributes.Single();
        attribute.Value.Should().BeNull();
        attribute.ValueBbText!.Value.Should().Be("[b]жирный[/b]");
        attribute.ValueBbText.Context.Should().NotBeNull(
            "the envelope is what lets the owner round-trip the raw BBCode");
    }

    /// <summary>
    /// A lead who may edit an NPC or a sheet that allows it gets its BBCode values
    /// as written, as the player does; a reader who may not, none.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LetAViewerWhoMayEditTheCharacterHaveItsValuesAsWritten(bool viewerMayEdit)
    {
        var details = CreateMapper().ToCharacterDetails(new DtoCharacter
        {
            Id = Guid.NewGuid(),
            Name = "Гримли",
            IsNpc = true,
            Attributes =
            [
                new DtoCharacterAttribute
                {
                    Id = Guid.NewGuid(),
                    Title = "История",
                    Value = "[b]жирный[/b]",
                    Type = AttributeSpecificationType.BbCode
                }
            ]
        }, viewerMayEdit);

        var editableBy = details.Attributes.Single().ValueBbText!.Context!.EditableBy;
        (editableBy?.Invoke(NSubstitute.Substitute.For<DM.Domain.Core.Authorization.IIntentionManager>()) ?? false)
            .Should().Be(viewerMayEdit);
    }

    [Fact]
    public void UnfoldTheAccessPolicyIntoThePrivacyBlock()
    {
        var details = CreateMapper().ToCharacterDetails(new DtoCharacter
        {
            Id = Guid.NewGuid(),
            Name = "Гримли",
            IsNpc = true,
            AccessPolicy = CharacterAccessPolicy.EditAllowed | CharacterAccessPolicy.PostEditAllowed
        });

        details.Privacy.IsNpc.Should().BeTrue();
        details.Privacy.EditByMaster.Should().BeTrue();
        details.Privacy.EditPostByMaster.Should().BeTrue();
    }
}
