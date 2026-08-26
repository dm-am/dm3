using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using AwesomeAssertions;
using Xunit;
using DtoCharacter = DM.Domain.Game.Features.Games.Character;
using DtoCharacterAttribute = DM.Domain.Game.Features.Games.CharacterAttribute;

namespace DM.Web.API.Tests.Features.Game;

public class CharacterMapperShould : UnitTestBase
{
    private CharacterMapper CreateMapper() =>
        new(new UserMapper(Mock<IImgproxyUrlBuilder>()));

    /// <summary>
    /// PATCH carries "not sent" as null and the write model must keep saying
    /// it. The destination flags are nullable on purpose: a plain false here
    /// is the bug that demoted an NPC to a player character on every PATCH
    /// without a privacy block.
    /// </summary>
    [Fact]
    public void KeepAnUnsentPrivacyBlockNull()
    {
        var update = CreateMapper().ToUpdateCharacter(new UpdateCharacterRequest
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
        var update = CreateMapper().ToUpdateCharacter(new UpdateCharacterRequest
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

    /// <summary>
    /// The raw stored BBCode is never placed into the plain Value (an HTML
    /// sink); it travels as a server-rendered InfoBbText instead.
    /// </summary>
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
    /// The privacy block of a details response folds back out of the flags.
    /// </summary>
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
