using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Mentorships;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Mentorships;

public class MentorshipServiceShould : UnitTestBase
{
    private readonly Mock<IMentorshipRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
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
        _identityProvider.Setup(p => p.Current).Returns(mentorIdentity);

        _service = new MentorshipService(_repository.Object, _identityProvider.Object);
    }

    [Fact]
    public async Task ThrowWhenNonMentorTriesToAssignGameMentor()
    {
        var userIdentity = Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = UserRole.RegularUser, Username = "User" },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        _identityProvider.Setup(p => p.Current).Returns(userIdentity);

        var act = () => _service.AssignGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("Mentor role"));
    }

    [Fact]
    public async Task ThrowWhenAssigningMentorToNonexistentGame()
    {
        _repository.Setup(r => r.GameExists(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _service.AssignGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("not found"));
    }

    [Fact]
    public async Task ThrowWhenGameAlreadyHasMentor()
    {
        _repository.Setup(r => r.GameExists(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetGameMentorId(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var act = () => _service.AssignGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("already has a mentor"));
    }

    [Fact]
    public async Task AssignGameMentorSuccessfully()
    {
        _repository.Setup(r => r.GameExists(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetGameMentorId(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        await _service.AssignGameMentor(_gameId);

        _repository.Verify(r => r.SetGameMentor(_gameId, _mentorUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenRemovingMentorFromGameUserIsNotMentorOf()
    {
        _repository.Setup(r => r.GameExists(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetGameMentorId(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var act = () => _service.RemoveGameMentor(_gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.Message.Contains("not the mentor"));
    }

    [Fact]
    public async Task RemoveGameMentorSuccessfully()
    {
        _repository.Setup(r => r.GameExists(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetGameMentorId(_gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mentorUserId);

        await _service.RemoveGameMentor(_gameId);

        _repository.Verify(r => r.SetGameMentor(_gameId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignBlogMentorSuccessfully()
    {
        _repository.Setup(r => r.BlogExists(_blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetBlogMentorId(_blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        await _service.AssignBlogMentor(_blogId);

        _repository.Verify(r => r.SetBlogMentor(_blogId, _mentorUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveBlogMentorSuccessfully()
    {
        _repository.Setup(r => r.BlogExists(_blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _repository.Setup(r => r.GetBlogMentorId(_blogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mentorUserId);

        await _service.RemoveBlogMentor(_blogId);

        _repository.Verify(r => r.SetBlogMentor(_blogId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
