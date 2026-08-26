using System;
using System.Linq;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using AwesomeAssertions;
using Xunit;
using DomainPost = DM.Domain.Game.Features.Games.Post;
using DomainCharacterShort = DM.Domain.Game.Features.Games.CharacterShort;
using DomainPostAttachment = DM.Domain.Game.Features.Games.PostAttachment;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// A post edit takes two texts and nothing else: the request DTO has no
/// character field, so the "PATCH detached the character" hazard is gone by
/// construction, and the mapper has no method from the response DTO to any
/// write model at all - what used to be a runtime assertion is now a missing
/// overload the compiler refuses.
/// </summary>
public class PostMapperShould : UnitTestBase
{
    private PostMapper CreateMapper()
    {
        var userMapper = new UserMapper(Mock<IImgproxyUrlBuilder>());
        return new PostMapper(
            new CharacterMapper(userMapper),
            new GameMapper(new AttributeSchemaMapper(), Mock<IIdentityProvider>()));
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
        GameText = "Он наклонился к ней. [private=Горим]Беги.[/private]",
        MetagameText = "ooc",
        PrivateAddresseeSnapshotJson = $"{{\"Горим\":[\"{AddresseeId}\"]}}",
        GameMasterUserId = MasterId,
        GameAssistantUserIds = [AssistantId],
        SharePrivateWithAll = true,
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
    /// The game text is the surface [private] lives on: its envelope must
    /// carry the full privacy context - the author-forever id, the game, the
    /// frozen addressee map, the leads and both per-post/per-room overrides.
    /// A post that crosses the API without it renders [private] closed for
    /// the very people it addresses.
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
        context.PostSharePrivateWithAll.Should().BeTrue();
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
    /// the game but no addressee context at all.
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
        spec.EdgesCount.Should().Be(20);   // Dice → EdgesCount
        spec.DiceCount.Should().Be(2);     // Count → DiceCount
        spec.Bonus.Should().Be(3);
        spec.ExplosionCount.Should().Be(1); // Explosion → ExplosionCount
        spec.IsHidden.Should().BeTrue();   // !Public → IsHidden
        spec.Comment.Should().Be("hit");
    }

    /// <summary>
    /// The measured box of an attachment reaches the payload, because the page
    /// cannot reserve room for a picture whose shape it is not told.
    /// </summary>
    /// <remarks>
    /// The pipeline measures every image it stores and the row keeps the pair,
    /// so the only question is whether the numbers survive the trip to the
    /// client - and they are the whole of what stops an attachment picture in
    /// the text from pushing the text under it down when it decodes.
    /// </remarks>
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
    /// <remarks>
    /// Nulls and not zeroes, and the difference is the point: the client
    /// reserves the box only while it can tell that nobody measured, and a zero
    /// would read as a measurement of nothing.
    /// </remarks>
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
