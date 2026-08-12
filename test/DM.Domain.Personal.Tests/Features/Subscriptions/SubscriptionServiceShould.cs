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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Subscriptions;

/// <summary>
/// The generic subscription endpoint takes a target type and an identifier and
/// belongs to no module, so the rules the modules put on their own targets are
/// exactly what it cannot know by itself.
/// </summary>
/// <remarks>
/// It wrote the row without asking. A game's blacklist refuses a join, and a
/// request naming that game by type and identifier walked past the refusal: the
/// same table, the same TargetType.Game, the roster and GameRole.Reader, the
/// private comment thread open. The module's own handle checked; this one was a
/// second door to the same write with no lock on it.
/// </remarks>
public class SubscriptionServiceShould : UnitTestBase
{
    /// <summary>
    /// Hand-written rather than mocked: what is under test is which guard the
    /// service picks and whether it obeys the answer, so the guard has to be a
    /// thing with behaviour and a call count, not a configured expectation.
    /// </summary>
    private sealed class Guard : ISubscriptionTargetGuard
    {
        private readonly string? _refusal;

        public Guard(SubscriptionTargetType targetType, string? refusal)
        {
            TargetType = targetType;
            _refusal = refusal;
        }

        public SubscriptionTargetType TargetType { get; }

        public int Asked { get; private set; }

        public Task<string?> Refusal(Guid targetId, Guid subscriberId, CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult(_refusal);
        }
    }

    private readonly Mock<ISubscriptionRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    public SubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _identityProvider.Setup(p => p.Current)
            .Returns(Identities.User(_currentUserId, "subscriber"));
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
    }

    private SubscriptionService ServiceGuardedBy(params ISubscriptionTargetGuard[] guards) =>
        new(
            _repository.Object,
            _userLookupService.Object,
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            guards);

    [Fact]
    public async Task RefuseWhatTheTargetRefuses()
    {
        var guard = new Guard(SubscriptionTargetType.Game, RefusalMessage.BlacklistedFromGame);
        var service = ServiceGuardedBy(guard);

        var act = async () => await service.SubscribeAsync(SubscriptionTargetType.Game, _targetId);

        (await act.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        guard.Asked.Should().Be(1, "the target is asked before the row is written, not after");
        _repository.Verify(
            r => r.CreateAsync(It.IsAny<CreateSubscription>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the row is the thing the rule exists to prevent");
    }

    [Fact]
    public async Task WriteTheSubscriptionTheTargetAllows()
    {
        var guard = new Guard(SubscriptionTargetType.Game, refusal: null);
        var service = ServiceGuardedBy(guard);

        await service.SubscribeAsync(SubscriptionTargetType.Game, _targetId);

        guard.Asked.Should().Be(1);
        _repository.Verify(
            r => r.CreateAsync(
                It.Is<CreateSubscription>(s =>
                    s.TargetType == SubscriptionTargetType.Game && s.TargetId == _targetId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskTheGuardThatSpeaksForTheTargetTypeAndNoOther()
    {
        // A guard answers for one type. Asking the wrong one would either refuse
        // subscriptions that are fine or, worse, let through the ones it was
        // written to stop.
        var gameGuard = new Guard(SubscriptionTargetType.Game, RefusalMessage.BlacklistedFromGame);
        var service = ServiceGuardedBy(gameGuard);

        await service.SubscribeAsync(SubscriptionTargetType.Blog, _targetId);

        gameGuard.Asked.Should().Be(0, "a blog is not a game");
        _repository.Verify(
            r => r.CreateAsync(It.IsAny<CreateSubscription>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "a type with no rule of its own is not refused by another type's rule");
    }
}
