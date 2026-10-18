using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Subscriptions;

/// <summary>
/// The generic subscription endpoint takes a target type and an identifier and
/// belongs to no module.
/// </summary>
/// <remarks>
/// It used to ask every module for a rule of its own on being subscribed to,
/// and the one rule there was is gone: the blacklist
/// of a game or a blog closes writing, and following a game or a blog is
/// reading. The two doors to the same row - this endpoint and the module's own
/// handle - now agree by having nothing to ask, and the endpoint must not grow a
/// second answer again. The behaviour with a real list behind both doors is in
/// BlacklistRuleShould.
/// </remarks>
public class SubscriptionServiceShould : UnitTestBase
{
    private readonly ISubscriptionRepository _repository;
    private readonly SubscriptionService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    public SubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        var identityProvider = Mock<IIdentityProvider>();
        var guidFactory = Mock<IGuidFactory>();
        var dateTimeProvider = Mock<IDateTimeProvider>();

        identityProvider.Current.Returns(Identities.User(_currentUserId, "subscriber"));
        guidFactory.Create().Returns(Guid.NewGuid());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new SubscriptionService(
            _repository,
            Mock<IUserLookupService>(),
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    [Theory]
    [InlineData(SubscriptionTargetType.Game)]
    [InlineData(SubscriptionTargetType.Blog)]
    public async Task WriteTheSubscriptionWithoutAskingTheTargetsBlacklist(SubscriptionTargetType targetType)
    {
        await _service.SubscribeAsync(targetType, _targetId);

        await _repository.Received(1).CreateAsync(
            Arg.Is<CreateSubscription>(s =>
                s.SubscriberId == _currentUserId && s.TargetType == targetType && s.TargetId == _targetId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StillRefuseASubscriptionToOneself()
    {
        var act = async () => await _service.SubscribeAsync(SubscriptionTargetType.User, _currentUserId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().CreateAsync(
            Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }
}
