using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Mentorships;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Mentorships;

public class MentorshipServiceShould : UnitTestBase
{
    private readonly IMentorshipRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly MentorshipService service;
    private readonly Guid mentorUserId = Guid.NewGuid();
    private readonly Guid gameId = Guid.NewGuid();
    private readonly Guid blogId = Guid.NewGuid();

    public MentorshipServiceShould()
    {
        repository = Mock<IMentorshipRepository>();
        identityProvider = Mock<IIdentityProvider>();

        identityProvider.Current.Returns(Identities.User(mentorUserId, "Mentor", UserRole.Mentor));

        service = new MentorshipService(repository, identityProvider);
    }

    [Fact]
    public async Task ThrowWhenNonMentorTriesToAssignGameMentor()
    {
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "User"));

        var act = () => service.AssignGameMentor(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("роль наставника"));
    }

    [Fact]
    public async Task ThrowWhenAssigningMentorToNonexistentGame()
    {
        repository.GameExists(gameId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => service.AssignGameMentor(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдена"));
    }

    [Fact]
    public async Task ThrowWhenGameAlreadyHasMentor()
    {
        repository.GameExists(gameId, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetGameMentorId(gameId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var act = () => service.AssignGameMentor(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("уже есть наставник"));
    }

    [Fact]
    public async Task AssignGameMentorSuccessfully()
    {
        repository.GameExists(gameId, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetGameMentorId(gameId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await service.AssignGameMentor(gameId);

        await repository.Received(1).SetGameMentor(gameId, mentorUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenRemovingMentorFromGameUserIsNotMentorOf()
    {
        repository.GameExists(gameId, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetGameMentorId(gameId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var act = () => service.RemoveGameMentor(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не наставник"));
    }

    [Fact]
    public async Task RemoveGameMentorSuccessfully()
    {
        repository.GameExists(gameId, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetGameMentorId(gameId, Arg.Any<CancellationToken>()).Returns(mentorUserId);

        await service.RemoveGameMentor(gameId);

        await repository.Received(1).SetGameMentor(gameId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignBlogMentorSuccessfully()
    {
        repository.BlogExists(blogId, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetBlogMentorId(blogId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await service.AssignBlogMentor(blogId);

        await repository.Received(1).SetBlogMentor(blogId, mentorUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveBlogMentorSuccessfully()
    {
        repository.BlogExists(blogId, Arg.Any<CancellationToken>()).Returns(true);
        repository.GetBlogMentorId(blogId, Arg.Any<CancellationToken>()).Returns(mentorUserId);

        await service.RemoveBlogMentor(blogId);

        await repository.Received(1).SetBlogMentor(blogId, null, Arg.Any<CancellationToken>());
    }
}
