using System;
using DM.Domain.Game.Features.Posts;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

// A lead sees every [private] block in the game, so an unfilled field must not become Guid.Empty,
// the guest's id.
public class PostGameLeadsShould
{
    private static readonly Guid MasterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AssistantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void NameNobodyWhenTheProjectionFilledNeitherField()
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
    public void DropAnEmptyMasterAndKeepTheAssistants()
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
