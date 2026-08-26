using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Mentorships;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Mentorships;

public class MentorshipServiceShould : UnitTestBase
{
    private readonly IMentorshipRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly MentorshipService _service;
    private readonly Guid _mentorUserId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _blogId = Guid.NewGuid();

    public MentorshipServiceShould()
    {
        _repository = Mock<IMentorshipRepository>();
        _identityProvider = Mock<IIdentityProvider>();

        var mentorIdentity = Identity.Success(
            new AuthenticatedUser { UserId = _mentorUserId, Role = UserRole.Mentor, Username = "Mentor" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(mentorIdentity);

        _service = new MentorshipService(_repository, _identityProvider);
    }

    [Fact]
    public async Task ThrowWhenNonMentorTriesToAssignGameMentor()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Current.Returns(userIdentity);

        var act = () => _service.AssignGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("роль наставника"));
    }

    [Fact]
    public async Task ThrowWhenAssigningMentorToNonexistentGame()
    {
        _repository.GameExists(_gameId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _service.AssignGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не найдена"));
    }

    [Fact]
    public async Task ThrowWhenGameAlreadyHasMentor()
    {
        _repository.GameExists(_gameId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetGameMentorId(_gameId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var act = () => _service.AssignGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("уже есть наставник"));
    }

    [Fact]
    public async Task AssignGameMentorSuccessfully()
    {
        _repository.GameExists(_gameId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetGameMentorId(_gameId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await _service.AssignGameMentor(_gameId);

        await _repository.Received(1).SetGameMentor(_gameId, _mentorUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenRemovingMentorFromGameUserIsNotMentorOf()
    {
        _repository.GameExists(_gameId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetGameMentorId(_gameId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        var act = () => _service.RemoveGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("не наставник"));
    }

    [Fact]
    public async Task RemoveGameMentorSuccessfully()
    {
        _repository.GameExists(_gameId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetGameMentorId(_gameId, Arg.Any<CancellationToken>()).Returns(_mentorUserId);

        await _service.RemoveGameMentor(_gameId);

        await _repository.Received(1).SetGameMentor(_gameId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignBlogMentorSuccessfully()
    {
        _repository.BlogExists(_blogId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetBlogMentorId(_blogId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        await _service.AssignBlogMentor(_blogId);

        await _repository.Received(1).SetBlogMentor(_blogId, _mentorUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveBlogMentorSuccessfully()
    {
        _repository.BlogExists(_blogId, Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetBlogMentorId(_blogId, Arg.Any<CancellationToken>()).Returns(_mentorUserId);

        await _service.RemoveBlogMentor(_blogId);

        await _repository.Received(1).SetBlogMentor(_blogId, null, Arg.Any<CancellationToken>());
    }
}
