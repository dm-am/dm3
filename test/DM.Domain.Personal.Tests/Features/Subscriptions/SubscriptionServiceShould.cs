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

    private readonly ISubscriptionRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUserLookupService _userLookupService;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _targetId = Guid.NewGuid();

    public SubscriptionServiceShould()
    {
        _repository = Mock<ISubscriptionRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _identityProvider.Current.Returns(Identities.User(_currentUserId, "subscriber"));
        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
    }

    private SubscriptionService ServiceGuardedBy(params ISubscriptionTargetGuard[] guards) =>
        new(
            _repository,
            _userLookupService,
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
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
        // The row is the thing the rule exists to prevent.
        await _repository.DidNotReceive().CreateAsync(
            Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteTheSubscriptionTheTargetAllows()
    {
        var guard = new Guard(SubscriptionTargetType.Game, refusal: null);
        var service = ServiceGuardedBy(guard);

        await service.SubscribeAsync(SubscriptionTargetType.Game, _targetId);

        guard.Asked.Should().Be(1);
        await _repository.Received(1).CreateAsync(
                Arg.Is<CreateSubscription>(s =>
                    s.TargetType == SubscriptionTargetType.Game && s.TargetId == _targetId),
                Arg.Any<CancellationToken>());
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
        // A type with no rule of its own is not refused by another type's rule.
        await _repository.Received(1).CreateAsync(
            Arg.Any<CreateSubscription>(), Arg.Any<CancellationToken>());
    }
}
