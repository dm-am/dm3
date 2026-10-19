using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The filters on what a post says, read through the rated feed: what is
/// matched is the text the reader is shown, never the hidden block or the markup.
/// </summary>
public class PostFiltersShould : PostRepositoryTestBase
{
    public PostFiltersShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Which posts come back says nothing about which of them hides something.
    /// </summary>
    /// <remarks>The two bodies differ by the block alone and project to the same visible text.</remarks>
    [Fact]
    public async Task TellNothingAboutAHiddenBlockFromWhichPostsComeBack()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var withBlock = await AddRatedPostAsync(dbContext, context, positiveReviews: 0,
            gameText: "Отряд идет дальше [private=\"Гончая\"]в третьей комнате ловушка[/private] к реке");
        var withoutBlock = await AddRatedPostAsync(dbContext, context, positiveReviews: 0,
            gameText: "Отряд идет дальше к реке");

        // The phrase spans exactly the place where the block stands in one of them.
        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "дальше к реке",
        }, Guid.Empty);

        total.Should().Be(2);
        posts.Select(p => p.Id).Should().BeEquivalentTo(new[] { withBlock, withoutBlock },
            "a post with a hidden block and the same post without one answer a search alike");
    }

    /// <summary>
    /// And the hidden words are not what is matched either.
    /// </summary>
    [Fact]
    public async Task KeepThePrivateTextOutOfWhatTheFilterMatches()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        await AddRatedPostAsync(dbContext, context, positiveReviews: 0,
            gameText: "Отряд идет дальше [private=\"Гончая\"]в третьей комнате ловушка[/private] к реке");

        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "ловушка",
        }, Guid.Empty);

        total.Should().Be(0);
        posts.Should().BeEmpty("the block is not on the page, so it is not in the search either");
    }

    /// <summary>
    /// The ordinary case: a post is found by the words of its text.
    /// </summary>
    [Fact]
    public async Task FindAPostByTheWordsItsReaderSees()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var postId = await AddRatedPostAsync(dbContext, context, positiveReviews: 0,
            gameText: "Гонец принес письмо из столицы");
        await AddRatedPostAsync(dbContext, context, positiveReviews: 0,
            gameText: "Караван ушел на восход");

        var (found, foundTotal) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "принес письмо",
        }, Guid.Empty);

        foundTotal.Should().Be(1);
        found.Should().ContainSingle().Which.Id.Should().Be(postId);

        // The filter is case-insensitive, as the parameter says it is.
        var (upperCase, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "ПРИНЕС ПИСЬМО",
        }, Guid.Empty);

        upperCase.Should().ContainSingle().Which.Id.Should().Be(postId);

        var (missing, missingTotal) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "дракон",
        }, Guid.Empty);

        missingTotal.Should().Be(0);
        missing.Should().BeEmpty();
    }

    /// <summary>
    /// The named, visible price of the move: the markup is no longer searchable and
    /// the words it splits are.
    /// </summary>
    /// <remarks>What is matched is the text the reader is shown.</remarks>
    [Fact]
    public async Task StopMatchingTheMarkupAndStartMatchingTheWordItSplits()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var postId = await AddRatedPostAsync(dbContext, context, positiveReviews: 0,
            gameText: "[b]Стран[/b]ники вышли к реке");

        var (byWord, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "Странники",
        }, Guid.Empty);

        byWord.Should().ContainSingle("the reader sees one word, and one word is what he searches for")
            .Which.Id.Should().Be(postId);

        var (byMarkup, byMarkupTotal) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
            Search = "[b]Стран",
        }, Guid.Empty);

        byMarkupTotal.Should().Be(0);
        byMarkup.Should().BeEmpty("the markup is not part of what anybody reads");
    }
}
