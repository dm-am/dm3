using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.MongoIntegration;

/// <summary>
/// One index the database disagrees about must cost that index and nothing else.
/// </summary>
/// <remarks>
/// The whole set of a collection used to go out as a single createIndexes, which
/// the server runs whole or not at all: an index created by hand, or a stored TTL
/// that no longer matches the constant, took every other index of that collection
/// down with it. The failure was swallowed into a log line, so the site simply ran
/// its queries as collection scans and said nothing.
/// </remarks>
public class MongoIndexAssertionShould : UnitTestBase
{
    private static CreateIndexModel<BsonDocument> Index(string name) =>
        new(Builders<BsonDocument>.IndexKeys.Ascending(name),
            new CreateIndexOptions { Name = name });

    [Fact]
    public async Task AssertTheRestOfTheSetWhenOneIndexIsRefused()
    {
        var indexManager = Mock<IMongoIndexManager<BsonDocument>>();
        indexManager
            .SetupSequence(m => m.CreateOneAsync(
                It.IsAny<CreateIndexModel<BsonDocument>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("IndexOptionsConflict"))
            .ReturnsAsync("IX_Second")
            .ReturnsAsync("IX_Third");

        await MongoIndexInitializer.Assert(
            indexManager.Object,
            "Probes",
            new[] { Index("IX_First"), Index("IX_Second"), Index("IX_Third") },
            NullLogger.Instance,
            CancellationToken.None);

        indexManager.Verify(m => m.CreateOneAsync(
                It.IsAny<CreateIndexModel<BsonDocument>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3),
            "the two indexes after the refused one serve queries of their own");
    }

    /// <summary>
    /// An index is a performance decision, so a database that refuses one must not
    /// keep the site from starting.
    /// </summary>
    [Fact]
    public async Task NotFailWhenTheStoreRefusesEveryIndex()
    {
        var indexManager = Mock<IMongoIndexManager<BsonDocument>>();
        indexManager
            .Setup(m => m.CreateOneAsync(
                It.IsAny<CreateIndexModel<BsonDocument>>(),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MongoException("no"));

        var act = () => MongoIndexInitializer.Assert(
            indexManager.Object,
            "Probes",
            new[] { Index("IX_First"), Index("IX_Second") },
            NullLogger.Instance,
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
