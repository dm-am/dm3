using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Features.Games;

/// <summary>
/// One signed-in viewer's listings, one cache entry each.
/// </summary>
/// <remarks>
/// A signed-in viewer's page of games is cached for a few seconds under a key
/// spelled out field by field. A field that changes what the repository returns
/// and is left out of that spelling hands the viewer the page of the previous
/// listing for as long as the entry lives, which is exactly the window in which
/// somebody clicking through the filters asks the next one. The anonymous key is walked the same way in
/// <see cref="AnonymousGameCacheKeyShould"/>.
///
/// Asked through the listing itself rather than through the method that spells
/// the key: what is held is the key the cache is actually handed, whatever
/// builds it.
/// </remarks>
public class UserGameCacheKeyShould : UnitTestBase
{
    private static readonly Guid ViewerId = Guid.Parse("5a1e0000-0000-4000-8000-0000000000a1");

    private readonly ICache _cache;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly GameService _service;
    private readonly List<string> _keys = [];

    public UserGameCacheKeyShould()
    {
        var gamesQueryValidator = Mock<IValidator<GamesQuery>>();
        gamesQueryValidator.ValidateAsync(Arg.Any<ValidationContext<GamesQuery>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(ViewerId, UserRole.RegularUser));

        _blacklistChecker = Mock<IUserBlacklistChecker>();
        _blacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(ViewerId, UserBlacklistSettings.HideGames, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        // The page itself is empty: what is asked is the key it is filed under.
        _cache = Mock<ICache>();
        _cache
            .GetOrCreateAsync(Arg.Any<object>(), Arg.Any<Func<Task<(GameDto[], PagingData)>>>(), Arg.Any<TimeSpan>())
            .Returns(call =>
            {
                _keys.Add((string)call.ArgAt<object>(0));
                return (Array.Empty<GameDto>(), new PagingData(new PagingQuery(), 10, 0));
            });

        _service = new GameService(
            gamesQueryValidator,
            Mock<IValidator<UpdateGame>>(),
            Mock<IGameCreationValidator>(),
            Mock<IGameTagResolver>(),
            Mock<IIntentionManager>(),
            Mock<IGameAttributeSchemaRepository>(),
            Mock<IGameRepository>(),
            Mock<IGameUserRepository>(),
            Mock<IGameInvitationService>(),
            identityProvider,
            Mock<IGameBlacklistRepository>(),
            _blacklistChecker,
            Mock<IUnreadCountersRepository>(),
            Mock<IGameSubscriptionService>(),
            Mock<IDateTimeProvider>(),
            Mock<IGuidFactory>(),
            Mock<IEventProducer>(),
            _cache,
            Mock<ILogger<GameService>>(),
            new TagNamesAsTyped());
    }

    /// <summary>
    /// Every field the viewer can set moves the key.
    /// </summary>
    /// <remarks>
    /// Walked from a listing the viewer asked about himself, because that is the
    /// one listing the personal blacklist leaves alone: on any other the service
    /// fills <see cref="GamesQuery.ExcludedMasterIds"/> itself and whatever the
    /// caller put there is gone before the key is built. The rule below holds
    /// that field on the path where the service fills it.
    /// </remarks>
    [Fact]
    public async Task TellApartEveryQueryTheViewerCanAsk()
    {
        var properties = QueryProbes.WritableProperties(typeof(GamesQuery));
        properties.Should().NotBeEmpty(
            "the query declares the fields this is about, and finding none passes everything");

        var plainKey = await KeyOf(Own());

        foreach (var property in properties)
        {
            var keys = new List<string>();
            foreach (var value in QueryProbes.Candidates(property.PropertyType))
            {
                keys.Add(await KeyOf(Own(property, value)));
            }

            keys.Should().NotBeEmpty($"{property.Name} has to be probed with some value to be checked at all");
            keys.Should().Contain(key => key != plainKey,
                $"no value of {property.Name} changes the key the viewer's page is cached under, so " +
                "two listings that differ in it share one entry and the second is handed the first one's page");
        }
    }

    /// <summary>
    /// The masters the viewer hides are part of the key on the listings they are
    /// hidden from.
    /// </summary>
    /// <remarks>
    /// Switching "Скрывать игры" on, or adding a master to the list, has to take
    /// effect on the next page and not after the entry runs out.
    /// </remarks>
    [Fact]
    public async Task TellApartTheListingsOfAViewerWhoHidesAMaster()
    {
        var hidesNobody = await KeyOf(new GamesQuery());

        _blacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(ViewerId, UserBlacklistSettings.HideGames, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { Guid.Parse("5a1e0000-0000-4000-8000-0000000000b2") });
        var hidesOne = await KeyOf(new GamesQuery());

        hidesOne.Should().NotBe(hidesNobody,
            "the master is taken off this listing, so the page without him and the page with " +
            "him are two pages");
    }

    /// <summary>A listing of the games the viewer takes part in, with one field set.</summary>
    private static GamesQuery Own(PropertyInfo? property = null, object? value = null)
    {
        var query = new GamesQuery { Participating = true };
        property?.SetValue(query, value);

        return query;
    }

    private async Task<string> KeyOf(GamesQuery query)
    {
        _keys.Clear();
        await _service.GetGamesAsync(query);

        _keys.Should().ContainSingle("a signed-in viewer's listing is read through the cache once");
        return _keys.Single();
    }
}
