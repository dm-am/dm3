using System;
using DM.Domain.Game.Features.Games;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

/// <summary>
/// Who the post reports as a lead of its game.
/// </summary>
/// <remarks>
/// The renderer's widest permission hangs off this list: a lead sees every
/// [private] block in the game, addressed to them or not. The list is composed
/// from two fields a projection has to fill, and a projection that filled
/// neither used to produce a one-element list holding Guid.Empty — the id an
/// anonymous reader carries. That is the leak this covers, from the side that
/// produced the list rather than the side that read it.
/// </remarks>
public class PostGameLeadsShould
{
    private static readonly Guid MasterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AssistantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void NameNobody_WhenTheProjectionFilledNeitherField()
    {
        new Post().GameLeadUserIds.Should().BeEmpty();
    }

    [Fact]
    public void NameTheMasterAndTheAssistants()
    {
        var post = new Post
        {
            GameMasterUserId = MasterId,
            GameAssistantUserIds = [AssistantId]
        };

        post.GameLeadUserIds.Should().Equal(MasterId, AssistantId);
    }

    [Fact]
    public void DropAnEmptyMaster_AndKeepTheAssistants()
    {
        var post = new Post { GameAssistantUserIds = [AssistantId] };

        post.GameLeadUserIds.Should().Equal(AssistantId);
    }

    [Fact]
    public void DropAnEmptyAssistant()
    {
        var post = new Post
        {
            GameMasterUserId = MasterId,
            GameAssistantUserIds = [Guid.Empty]
        };

        post.GameLeadUserIds.Should().Equal(MasterId);
    }
}
