using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Popularity;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Popularity;

public class BlogPopularityProcessorShould : UnitTestBase
{
    [Fact]
    public async Task ScoreABlogWithoutActiveReadersAsZero()
    {
        var repository = Mock<IBlogPopularityRepository>();
        var read = Guid.NewGuid();
        var unread = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        repository.GetScorableBlogIds(default).Returns(new[] { read, unread });
        repository.CountActiveReaders(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>(), default)
            .Returns(new Dictionary<Guid, int> { [read] = 3 });
        repository.ApplyScores(Arg.Any<IReadOnlyDictionary<Guid, int>>(), now, default).Returns(1);

        var result = await new BlogPopularityProcessor(repository).UpdateScoresAsync(now);

        result.Should().Be((1, 2));
        await repository.Received(1).ApplyScores(
            Arg.Is<IReadOnlyDictionary<Guid, int>>(s => s.Count == 2 && s[read] == 3 && s[unread] == 0),
            now, default);
    }
}
