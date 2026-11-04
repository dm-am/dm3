using System;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainPost = DM.Domain.Game.Features.Posts.Post;
using DomainCharacterShort = DM.Domain.Game.Features.Characters.CharacterShort;
using DomainPostAttachment = DM.Domain.Game.Features.Posts.PostAttachment;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// A post carries the render context of its texts, its dice and its attachments to the client.
/// </summary>
public class PostMapperShould : UnitTestBase
{
    /// <remarks>The rule of hidden blocks is the real one, read for a guest.</remarks>
    private static PostMapper CreateMapper()
    {
        var readers = Mock<IAuthorizationContextProvider>();
        readers.CurrentSubject.Returns(AuthenticatedUser.Guest);

        var userMapper = new UserMapper();
        return new PostMapper(
            new CharacterMapper(userMapper),
            new GameMapper(new AttributeSchemaMapper(), Mock<IIdentityProvider>()),
            new PrivateBlockReading(new BbParserProvider()),
            readers);
    }

    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid GameId = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid MasterId = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid AssistantId = Guid.Parse("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid AddresseeId = Guid.Parse("00000000-0000-0000-0000-00000000000e");

    /// <summary>
    /// A domain post carrying everything the render-context envelope reads:
    /// the frozen addressee snapshot, the lead ids and both [private]
    /// overrides.
    /// </summary>
    private static DomainPost CreateDomainPost() => new()
    {
        Id = Guid.NewGuid(),
        RoomId = Guid.NewGuid(),
        AuthorUserId = AuthorId,
        GameId = GameId,
        CreatedUtc = DateTimeOffset.UtcNow,
        GameText = "Он наклонился к ней. [private=\"Горим\"]Беги.[/private]",
        MetagameText = "ooc",
        PrivateAddresseeSnapshotJson = $"{{\"Горим\":[{{\"owner\":\"{AddresseeId}\"}}]}}",
        GameMasterUserId = MasterId,
        GameAssistantUserIds = [AssistantId],
        GameShowPrivateMessages = true,
        RoomViewPrivateText = true,
        Author = new GeneralUser { UserId = AuthorId, Username = "author" },
        Character = new DomainCharacterShort
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = AuthorId, Username = "author" },
            Name = "Арагорн"
        }
    };

    /// <summary>
    /// The game text is the surface [private] lives on: its envelope carries the full privacy context.
    /// </summary>
    [Fact]
    public void WrapGameTextInAGamePostEnvelope()
    {
        var post = CreateDomainPost();

        var result = CreateMapper().ToPost(post);

        var context = result.GameText.Context;
        context.Should().NotBeNull();
        context!.Surface.Should().Be(BbSurface.GamePost);
        context.PostAuthorUserId.Should().Be(AuthorId);
        context.GameId.Should().Be(GameId);
        context.GameLeadUserIds.Should().BeEquivalentTo(
            [MasterId, AssistantId], "leads are the master plus the assistants");
        context.GameShowPrivateMessages.Should().BeTrue();
        context.RoomViewPrivateText.Should().BeTrue();
    }

    /// <summary>
    /// The frozen snapshot is what the render path resolves [private] blocks
    /// against - the envelope must carry it parsed, keyed by the raw tag
    /// attribute.
    /// </summary>
    [Fact]
    public void CarryTheFrozenAddresseeMapIntoTheEnvelope()
    {
        var post = CreateDomainPost();

        var result = CreateMapper().ToPost(post);

        var map = result.GameText.Context!.PrivateAddresseeOwnerUserIdsByAttribute;
        map.Should().HaveCount(1);
        map.Should().ContainKey("Горим")
            .WhoseValue.Should().BeEquivalentTo([AddresseeId]);
    }

    /// <summary>
    /// The metagame text is OOC commentary: Comment surface ([mod] for
    /// moderators, never [private]), so its envelope carries the author and
    /// the game but no block addressees and no leads: the files the post keeps
    /// from the viewer come to it decided.
    /// </summary>
    [Fact]
    public void WrapMetagameTextInACommentEnvelope()
    {
        var post = CreateDomainPost();

        var result = CreateMapper().ToPost(post);

        var context = result.MetagameText.Context;
        context.Should().NotBeNull();
        context!.Surface.Should().Be(BbSurface.Comment);
        context.PostAuthorUserId.Should().Be(AuthorId);
        context.GameId.Should().Be(GameId);
        context.PrivateAddresseeOwnerUserIdsByAttribute.Should().BeEmpty();
        context.GameLeadUserIds.Should().BeEmpty();
    }

    /// <summary>
    /// A file the post keeps from the viewer is answered once, by PostAttachmentVisibility: the strip
    /// under the post goes without it, and both texts carry it to be cut from their pictures and links.
    /// </summary>
    [Fact]
    public void WithholdAFileFromTheStripAndTheTextsByOneAnswer()
    {
        var post = CreateDomainPost();
        // Neither override, which would open the file to every reader of the room.
        post.GameShowPrivateMessages = false;
        post.RoomViewPrivateText = false;
        var marked = Attachment(width: null, height: null);
        marked.AddresseesJson = $"[{{\"character\":\"{Guid.NewGuid()}\",\"name\":\"Горим\",\"owner\":\"{AddresseeId}\"}}]";
        post.Attachments = [marked];

        var result = CreateMapper().ToPost(post);

        result.Attachments.Should().BeEmpty("a guest is not among those the file is marked for");
        result.GameText.Context!.WithheldUploadIds.Should().Equal(marked.Id);
        result.MetagameText.Context!.WithheldUploadIds.Should().Equal(marked.Id);
    }

    /// <summary>A file every reader of the post reads is cut from nothing.</summary>
    [Fact]
    public void WithholdNothingOfAFileForEverybody()
    {
        var post = CreateDomainPost();
        post.Attachments = [Attachment(width: null, height: null)];

        var result = CreateMapper().ToPost(post);

        result.Attachments.Should().ContainSingle();
        result.GameText.Context!.WithheldUploadIds.Should().BeEmpty();
    }

    [Fact]
    public void MapAPostEditWithoutTouchingTheCharacter()
    {
        var request = new UpdatePostRequest { GameText = "text", MetagameText = "ooc" };

        var update = CreateMapper().ToUpdatePost(request);

        update.GameText.Should().Be("text");
        update.MetagameText.Should().Be("ooc");
        update.CharacterId.Should().BeNull(
            "an absent character is what leaves the stored one alone");
    }

    [Fact]
    public void MapCreateDiceRollRequestToDomainSpec()
    {
        var request = new CreatePostRequest
        {
            GameText = "text",
            DiceRolls = new[]
            {
                new CreatePostDiceRoll
                {
                    Dice = 20, Count = 2, Bonus = 3, Explosion = 1, Public = false, Comment = "hit"
                }
            }
        };

        var domain = CreateMapper().ToCreatePost(request);

        var spec = domain.DiceRolls.Single();
        spec.EdgesCount.Should().Be(20);
        spec.DiceCount.Should().Be(2);
        spec.Bonus.Should().Be(3);
        spec.ExplosionCount.Should().Be(1);
        spec.IsHidden.Should().BeTrue();
        spec.Comment.Should().Be("hit");
    }

    /// <summary>
    /// The measured box of an attachment reaches the payload, because the page
    /// cannot reserve room for a picture whose shape it is not told.
    /// </summary>
    [Fact]
    public void CarryTheMeasuredBoxOfAnAttachment()
    {
        var post = CreateDomainPost();
        post.Attachments = [Attachment(width: 1600, height: 1200)];

        var attachment = CreateMapper().ToPost(post).Attachments.Single();

        attachment.Width.Should().Be(1600);
        attachment.Height.Should().Be(1200);
        attachment.FileName.Should().Be("karta.png");
    }

    /// <summary>
    /// An attachment stored before the pipeline measured anything comes back
    /// without a pair rather than with an invented one.
    /// </summary>
    /// <remarks>Nulls, not zeroes: a zero would read as a measurement of nothing.</remarks>
    [Fact]
    public void SayNothingAboutAnAttachmentNobodyMeasured()
    {
        var post = CreateDomainPost();
        post.Attachments = [Attachment(width: null, height: null)];

        var attachment = CreateMapper().ToPost(post).Attachments.Single();

        attachment.Width.Should().BeNull();
        attachment.Height.Should().BeNull();
        attachment.Url.Should().NotBeNullOrEmpty("the file itself is still served");
    }

    private static DomainPostAttachment Attachment(int? width, int? height) => new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-00000000000f"),
        FileName = "karta.png",
        ContentType = "image/png",
        SizeBytes = 6707,
        Width = width,
        Height = height,
        CreatedUtc = DateTimeOffset.UtcNow,
    };
}
