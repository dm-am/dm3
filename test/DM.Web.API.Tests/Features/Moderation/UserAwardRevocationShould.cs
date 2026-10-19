using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Awards;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Features.Moderation.Awards;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// The revoke route names the holder, and only that user's award goes.
/// </summary>
public class UserAwardRevocationShould : UnitTestBase
{
    private readonly IAwardService _awardService = Mock<IAwardService>();
    private readonly UserAwardApiService _service;
    private readonly Guid _heldAwardId = Guid.NewGuid();

    public UserAwardRevocationShould()
    {
        _awardService.GetUserAwardsAsync("alice", Arg.Any<CancellationToken>())
            .Returns(new List<DomainUserAward> { new() { Id = _heldAwardId } });
        _service = new UserAwardApiService(_awardService, new AwardMapper());
    }

    [Fact]
    public async Task RevokeAnAwardTheUserHolds()
    {
        await _service.Revoke("alice", _heldAwardId);

        await _awardService.Received(1).RevokeAsync(_heldAwardId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseAnAwardOfAnotherUserAsIfThereWereNone()
    {
        var othersAwardId = Guid.NewGuid();

        var act = async () => await _service.Revoke("alice", othersAwardId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _awardService.DidNotReceive().RevokeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
