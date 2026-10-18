using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

/// <summary>
/// A cached listing of a board answers only the query it was filled for.
/// </summary>
/// <remarks>
/// The listing of a board the home page reads is cached for a minute and shared
/// by every viewer with one access policy. Two hand-kept lists decide it: which
/// queries may be served from the cache, and which fields the key is built from.
/// A field of the query that changes what the repository returns and is in
/// neither list hands one reader the page another reader asked for, and nothing
/// on either page says so.
///
/// Walked by reflection rather than listed, as the game keys are
/// (AnonymousGameCacheKeyShould): every field either keeps the query off the
/// cache or moves the key, and a field added tomorrow is walked without anybody
/// remembering this test.
/// </remarks>
public class TopicListCacheKeyShould : UnitTestBase
{
    /// <summary>The board whose listing the service caches.</summary>
    private const string CachedBoard = "news";

    private static readonly Guid BoardId = Guid.Parse("70b1c000-0000-4000-8000-0000000000b0");

    /// <summary>
    /// Fields the cache is known to answer for wrongly, each with what is wrong.
    /// </summary>
    /// <remarks>
    /// A register rather than a skipped rule: the walk still runs over these, and
    /// an entry whose field has been put right fails as stale, so the register
    /// cannot outlive the defect it records. A new entry is a new defect, and it
    /// is fixed rather than written down here.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> KnownShared = new Dictionary<string, string>
    {
        [nameof(TopicsQuery.IsAttached)] =
            "false is served from the cache while only true is kept off it, and the key does not " +
            "carry the field: the repository filters by false and returns everything for no value, " +
            "so the board page (isAttached=false) and a plain read of the same size share one entry",
    };

    private readonly List<string> _keys = [];

    [Fact]
    public async Task KeepEveryQueryItCannotTellApartOffTheCache()
    {
        var service = ServiceFor(BoardAccessPolicy.Guest);
        var properties = QueryProbes.WritableProperties(typeof(TopicsQuery));
        properties.Should().NotBeEmpty(
            "the query declares the fields this is about, and finding none passes everything");

        var plainKey = await KeyOf(service, new TopicsQuery());
        plainKey.Should().NotBeNull(
            "the plainest listing of the board - the one the home page reads - is the one this " +
            "cache exists for, and a walk that never reaches the cache checks nothing");

        var shared = new List<string>();
        foreach (var property in properties)
        {
            // Several values rather than one, as the game keys are walked: a field
            // may be normalised before it reaches the store, and what has to hold
            // is that the field is capable of moving the key at all.
            var cachedKeys = new List<string>();
            foreach (var value in QueryProbes.Candidates(property.PropertyType))
            {
                var key = await KeyOf(service, Query(property, value));
                if (key is not null)
                {
                    cachedKeys.Add(key);
                }
            }

            if (cachedKeys.Count > 0 && cachedKeys.All(key => key == plainKey))
            {
                shared.Add(property.Name);
            }
        }

        shared.Should().BeEquivalentTo(KnownShared.Keys,
            "a field the cache agrees to serve and the key does not carry gives one reader another " +
            "reader's page for the life of the entry; a field missing from the register is a new " +
            "defect, and a register entry the walk no longer finds has been put right and goes");
    }

    /// <summary>
    /// A guest and a moderator never share a listing: the moderator's carries
    /// topics the guest may not see.
    /// </summary>
    [Fact]
    public async Task KeepViewersOfDifferentAccessApart()
    {
        var asGuest = await KeyOf(ServiceFor(BoardAccessPolicy.Guest), new TopicsQuery());
        var asModerator = await KeyOf(ServiceFor(BoardAccessPolicy.Moderator), new TopicsQuery());

        asModerator.Should().NotBe(asGuest,
            "one entry for both shows the guest what only a moderator may read");
    }

    private static TopicsQuery Query(PropertyInfo property, object value)
    {
        var query = new TopicsQuery();
        property.SetValue(query, value);

        return query;
    }

    /// <summary>The key the listing was filed under, or null when it bypassed the cache.</summary>
    private async Task<string?> KeyOf(TopicService service, TopicsQuery query)
    {
        _keys.Clear();
        await service.GetListAsync(CachedBoard, query);

        _keys.Should().HaveCountLessThanOrEqualTo(1, "one listing is read through the cache at most once");
        return _keys.SingleOrDefault();
    }

    /// <summary>
    /// The service for a viewer whose role converts to the given policy, over a
    /// cache that records the keys it is handed.
    /// </summary>
    private TopicService ServiceFor(BoardAccessPolicy policy)
    {
        var boardService = Mock<IBoardService>();
        boardService.GetBoard(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(new Board { Id = BoardId, Title = CachedBoard, Alias = CachedBoard });

        var accessPolicyConverter = Mock<IAccessPolicyConverter>();
        accessPolicyConverter.Convert(Arg.Any<UserRole>()).Returns(policy);

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.Guest());

        var repository = Mock<ITopicRepository>();
        repository
            .Get(Arg.Any<Guid?>(), Arg.Any<BoardAccessPolicy>(), Arg.Any<PagingData?>(), Arg.Any<TopicsQuery>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Topic>());

        // The page itself is empty: what is asked is whether it was filed, and
        // under which key.
        var cache = Mock<ICache>();
        cache
            .GetOrCreateAsync(Arg.Any<object>(), Arg.Any<Func<Task<Topic[]>>>(), Arg.Any<TimeSpan>())
            .Returns(call =>
            {
                _keys.Add((string)call.ArgAt<object>(0));
                return Array.Empty<Topic>();
            });

        return new TopicService(
            Mock<IValidator<CreateTopic>>(),
            Mock<IValidator<UpdateTopic>>(),
            boardService,
            accessPolicyConverter,
            Mock<IIntentionManager>(),
            identityProvider,
            repository,
            Mock<IUnreadCountersRepository>(),
            Mock<IUserLookupService>(),
            Mock<IEventProducer>(),
            Mock<IGuidFactory>(),
            cache,
            new TagNamesAsTyped());
    }
}
