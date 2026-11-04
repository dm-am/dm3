using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Exceptions;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Comments;

/// <summary>
/// A discussion that is not there to the reader answers as the comment not being there.
/// </summary>
public class DiscussionRefusalShould
{
    private readonly Guid commentId = Guid.NewGuid();

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task AnswerAnAbsentDiscussionAsAnAbsentComment(HttpStatusCode discussionRefusal)
    {
        var read = () => DiscussionRefusal.AsCommentNotFound<int>(
            commentId, () => throw new HttpException(discussionRefusal, RefusalMessage.GameNotFound));

        var refusal = (await read.Should().ThrowExactlyAsync<HttpException>()).Which;
        refusal.StatusCode.Should().Be(HttpStatusCode.NotFound);
        refusal.Message.Should().Be(RefusalMessage.CommentNotFound(commentId));
    }

    [Fact]
    public async Task PassARefusalOfAnotherKindUnchanged()
    {
        var forbidden = new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);

        var read = () => DiscussionRefusal.AsCommentNotFound<int>(commentId, () => throw forbidden);

        (await read.Should().ThrowExactlyAsync<HttpException>()).Which.Should().BeSameAs(forbidden);
    }
}
