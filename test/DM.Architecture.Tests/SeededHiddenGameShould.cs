using System;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Parsing;
using DM.Tools.Seeder.Seeding;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The finished game of the seed holds each kind of hidden content an outsider must not read: a
/// hidden attribute, a hidden roll, and a hidden block that mentions one of two attachments.
/// </summary>
/// <remarks>
/// Held to the rules that do the hiding, so a fixture that stopped exercising one would say so.
/// </remarks>
public class SeededHiddenGameShould
{
    private static readonly Guid MasterId = Guid.Parse("10000000-0000-0000-0000-0000000000a1");

    private static readonly Guid AddresseeId = Guid.Parse("10000000-0000-0000-0000-0000000000a2");

    private static readonly Guid AddresseeCharacterId = Guid.Parse("10000000-0000-0000-0000-0000000000a3");

    private static readonly Guid MapId = Guid.Parse("10000000-0000-0000-0000-0000000000a4");

    private static readonly Guid LetterId = Guid.Parse("10000000-0000-0000-0000-0000000000a5");

    /// <summary>
    /// The class is hidden and the descriptor open: the descriptor stands in the list every reader of
    /// the game sees, so the domain refuses a hidden one, and the master saves the sheet he was given.
    /// </summary>
    [Fact]
    public void HideTheClassAndKeepTheDescriptorOpen()
    {
        var sheet = DataSeeder.HiddenClassSchema();

        var hiddenClass = sheet.Should().ContainSingle(s => s.Id == DataSeeder.HiddenClassAttributeId).Subject;
        hiddenClass.IsHidden.Should().BeTrue("an outsider reading a sheet is the case the game is there for");
        hiddenClass.IsDescriptor.Should().BeFalse("the list of characters shows its descriptor to everybody");
        sheet.Where(s => s.IsDescriptor).Should().ContainSingle(
                "CharacterAttributeValueFiller takes the first descriptor, and a second one would be read instead")
            .Subject.IsHidden.Should().BeFalse("a hidden descriptor is a sheet the domain refuses to save");
        sheet.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        sheet.Select(s => s.Order).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ShowOneRollOfTheScoutAndHideTheOther()
    {
        var rolls = DataSeeder.ScoutRolls(Guid.NewGuid(), DateTimeOffset.UnixEpoch, Guid.NewGuid(), Guid.NewGuid());

        rolls.Select(r => r.IsHidden).Should().Equal(new[] { false, true },
            "the post shows the reader what everybody reads beside what the leads and the author do");
    }

    [Fact]
    public void KeepTheMentionedAttachmentFromAnOutsiderAlone()
    {
        var text = DataSeeder.StrangerNoteText(MapId);
        var note = new PrivateBlockText(
            text,
            MasterId,
            [MasterId],
            DataSeeder.StrangerNoteSnapshot(text, AddresseeCharacterId, AddresseeId),
            GameShowPrivateMessages: false,
            RoomViewPrivateText: false);
        var rule = new PrivateBlockReading(new BbParserProvider());

        rule.HiddenFrom(null, note, [MapId, LetterId]).Should().BeEquivalentTo(new[] { MapId },
            "a guest reads neither the block nor the map it mentions, and the letter it does not mention " +
            "is kept from nobody");
        rule.HiddenFrom(Reader(AddresseeId), note, [MapId, LetterId]).Should().BeEmpty(
            "the block is frozen to the addressee's owner, who reads it and the map");
        rule.HiddenFrom(Reader(MasterId), note, [MapId, LetterId]).Should().BeEmpty(
            "the master reads every block");
    }

    /// <summary>
    /// The map the text puts only inside the block is marked for the block's addressee, as the save
    /// path requires, so its plate and the text say the same thing.
    /// </summary>
    [Fact]
    public void MarkTheMapForTheAddresseeOfItsBlock()
    {
        var text = DataSeeder.StrangerNoteText(MapId);
        new PrivateBlockReading(new BbParserProvider()).OnlyInsideBlocks(text, [MapId, LetterId])
            .Should().BeEquivalentTo(new[] { MapId }, "the map stands in the block alone");

        var mark = PrivateAddresseeSnapshot.ReadList(
            DataSeeder.StrangerNoteMapAddressees(AddresseeCharacterId, AddresseeId));

        mark.Should().NotBeNull("a file the text keeps in a block is not a file for everybody");
        mark!.Characters.Select(c => c.CharacterId).Should().Equal(AddresseeCharacterId);
        mark.OwnerUserIds.Should().BeEquivalentTo(new[] { AddresseeId });
    }

    private static AuthenticatedUser Reader(Guid userId) =>
        new() { UserId = userId, Username = "reader", Role = UserRole.RegularUser };
}
