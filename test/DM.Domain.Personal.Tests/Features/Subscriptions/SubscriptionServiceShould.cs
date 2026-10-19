using System;
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
/// It has no rules of its own: a blacklist closes writing, and following is reading.
/// </remarks>
public class SubscriptionServiceShould : UnitTestBase
{
    private readonly ISubscriptionRepository repository;
    private readonly SubscriptionService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly Guid targetId = Guid.NewGuid();

    public SubscriptionServiceShould()
    {
        repository = Mock<ISubscriptionRepository>();
        var identityProvider = Mock<IIdentityProvider>();
        var guidFactory = Mock<IGuidFactory>();
        var dateTimeProvider = Mock<IDateTimeProvider>();

        identityProvider.Current.Returns(Identities.User(currentUserId, "subscriber"));
        guidFactory.Create().Returns(Guid.NewGuid());
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new SubscriptionService(
            repository,
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
        await service.SubscribeAsync(targetType, targetId);

        await repository.Received(1).CreateAsync(
            Arg.Is<CreateSubscription>(s =>
                s.SubscriberId == currentUserId && s.TargetType == targetType && s.TargetId == targetId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StillRefuseASubscriptionToOneself()
    {
        var act = async () => await service.SubscribeAsync(SubscriptionTargetType.User, currentUserId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().CreateAsync(
            Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }
}
