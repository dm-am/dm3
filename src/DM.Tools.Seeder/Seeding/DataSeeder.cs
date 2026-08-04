using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Blog.Features.Popularity;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Game.Features.Popularity;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

/// <summary>
/// Development data seeder. Writes straight to the stores it seeds, on purpose:
/// the seed sets fields (timestamps, counters, ratings) that the domain services
/// derive and would refuse to accept. It is a tool, never part of a running site.
/// </summary>
internal sealed partial class DataSeeder
{
    private readonly DmDbContext _dbContext;
    private readonly DmMongoClient _mongoClient;
    private readonly ISecurityManager _securityManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IPollRepository _pollRepository;
    private readonly IPublicIdService _publicIdService;
    private readonly IImageProcessingService _imageProcessing;
    private readonly Amazon.S3.IAmazonS3 _s3Client;
    private readonly IGamePopularityProcessor _gamePopularity;
    private readonly IBlogPopularityProcessor _blogPopularity;
    private readonly CdnConfiguration _cdnConfig;

    /// <summary>
    /// The only source of randomness in the seed, and a seeded one.
    /// See <see cref="SeedDeterminism.Random"/>.
    /// </summary>
    private readonly Random _random;

    /// <summary>
    /// The instant every seeded timestamp is offset from.
    /// See <see cref="SeedDeterminism.Epoch"/>.
    /// </summary>
    private readonly DateTimeOffset _now;

    /// <summary>
    /// Creates a new instance of <see cref="DataSeeder"/>
    /// </summary>
    public DataSeeder(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        ISecurityManager securityManager,
        IGuidFactory guidFactory,
        SeedDeterminism determinism,
        IPollRepository pollRepository,
        IPublicIdService publicIdService,
        IImageProcessingService imageProcessing,
        Amazon.S3.IAmazonS3 s3Client,
        IGamePopularityProcessor gamePopularity,
        IBlogPopularityProcessor blogPopularity,
        IOptions<CdnConfiguration> cdnOptions)
    {
        _dbContext = dbContext;
        _mongoClient = mongoClient;
        _securityManager = securityManager;
        _guidFactory = guidFactory;
        _pollRepository = pollRepository;
        _publicIdService = publicIdService;
        _imageProcessing = imageProcessing;
        _s3Client = s3Client;
        _gamePopularity = gamePopularity;
        _blogPopularity = blogPopularity;
        _cdnConfig = cdnOptions.Value;
        _random = determinism.Random;
        _now = determinism.Epoch;
    }

    /// <summary>
    /// Start of the window the site means by "за неделю": seven days back.
    /// </summary>
    /// <remarks>
    /// The homepage "лучший пост недели" block filters posts by exactly this
    /// boundary (getWeekStartUtc on the client), and two parts of the seed have
    /// to agree with it: the showcase post must land inside the window, and
    /// leaderboard coverage must stay out of it. One implementation, so the two
    /// cannot drift apart.
    ///
    /// Rolling, not the calendar Monday. A calendar boundary empties the block
    /// for the first hours of every Monday, and it empties it for good once the
    /// seed is a week old, which is what a fixture on a developer machine
    /// always is.
    /// </remarks>
    private static DateTimeOffset WeekStartUtc(DateTimeOffset moment) => moment.AddDays(-7);

    /// <summary>
    /// Seed the content set: forum topics and comments, games, characters, posts,
    /// blogs, publications, chat messages, reviews, polls and ratings.
    /// Requires the base users, so run <see cref="SeedTestUsers"/> first.
    /// </summary>
    /// <returns>Per-aggregate creation counts</returns>
    public async Task<ComprehensiveSeedResult> SeedComprehensiveData()
    {
        var result = new ComprehensiveSeedResult();
        var now = _now;

        // This list decides who masters which game, who writes which comment and
        // who plays where, so its order has to be an order. Role alone leaves
        // ties and Postgres is free to break them differently on every run.
        //
        // Sorted here rather than in SQL, over a handful of accounts: ORDER BY
        // would hand the tiebreak to the database collation, and then the same
        // seed would lay out differently on a cluster initialised with a
        // different locale. Ordinal has no such opinion.
        var users = (await _dbContext.Users
                .Where(u => !u.IsRemoved && u.Role != UserRole.System)
                .ToListAsync())
            .OrderByDescending(u => u.Role)
            .ThenBy(u => u.Username, StringComparer.Ordinal)
            .ToList();

        if (users.Count < 5)
        {
            result.Details.Add("Not enough users. Run SeedTestUsers first.");
            return result;
        }

        // Categorize users by role
        var admin = users.FirstOrDefault(u => u.Role == UserRole.Admin);
        var seniorMod = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator);
        var moderator = users.FirstOrDefault(u => u.Role == UserRole.Moderator);
        var mentor = users.FirstOrDefault(u => u.Role == UserRole.Mentor);
        var regularUsers = users.Where(u => u.Role == UserRole.RegularUser).ToList();

        if (admin == null || seniorMod == null || regularUsers.Count < 3)
        {
            result.Details.Add("Missing required roles. Need Admin, SeniorMod, and at least 3 regular users.");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════
        // 1. UPDATE USER PROFILES
        // ═══════════════════════════════════════════════════════════════════
        await UpdateUserProfiles(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 2. CREATE FORUM TOPICS AND COMMENTS
        // ═══════════════════════════════════════════════════════════════════
        await CreateForumContent(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 2b. ASSIGN BOARD MODERATORS
        // ═══════════════════════════════════════════════════════════════════
        await AssignBoardModerators(users, result);

        // ═══════════════════════════════════════════════════════════════════
        // 3. SEED SYSTEM ATTRIBUTE SCHEMA (MongoDB) + CREATE GAMES
        // The system "Классическая схема" must exist before games reference it
        // via AttributeSchemaId. Idempotent upsert — survives PG reseed.
        // ═══════════════════════════════════════════════════════════════════
        await SeedSystemAttributeSchemaAsync(result);
        var gameIds = await CreateGames(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 4. CREATE BLOGS
        // ═══════════════════════════════════════════════════════════════════
        await CreateBlogs(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 5. CREATE GLOBAL CHAT AND MESSAGES
        // ═══════════════════════════════════════════════════════════════════
        await CreateGlobalChatMessages(users, now, result);
        await CreateGlobalChatEvents(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 6. CREATE REVIEWS
        // ═══════════════════════════════════════════════════════════════════
        await CreateReviews(users, gameIds, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 7. CREATE USER SUBSCRIPTIONS
        // ═══════════════════════════════════════════════════════════════════
        await CreateUserSubscriptions(users, result);

        // ═══════════════════════════════════════════════════════════════════
        // 8. CREATE POLLS (MongoDB)
        // ═══════════════════════════════════════════════════════════════════
        await CreatePolls(users, now, result);

        await _dbContext.SaveChangesAsync();

        // ═══════════════════════════════════════════════════════════════════
        // 9. CREATE LIKES FOR ALL CONTENT
        // ═══════════════════════════════════════════════════════════════════
        await CreateLikes(users, result);

        // ═══════════════════════════════════════════════════════════════════
        // 9b. ENSURE A FULL LEADERBOARD TOP-10 FOR THE CURRENT MONTH
        // The statistics page defaults to the current-month period; the organic
        // seed leaves the by-rating boards (reviews cluster in one finished
        // game) and the blog boards (publications spread across ~1.5 years)
        // short there. Top the current month up to a full top-10 on every
        // board. Idempotent — a re-seed within the same month is a no-op.
        // ═══════════════════════════════════════════════════════════════════
        await EnsureLeaderboardCoverage(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 10. UPDATE LastCommentId REFERENCES
        // ═══════════════════════════════════════════════════════════════════
        await UpdateLastCommentReferences();

        // ═══════════════════════════════════════════════════════════════════
        // 11. UPDATE POPULARITY SCORES (for sidebar sorting)
        // ═══════════════════════════════════════════════════════════════════
        await UpdatePopularityScores(now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 12. SOLOHIN-LEX SPECIFIC: username history + avatar
        // ═══════════════════════════════════════════════════════════════════
        await SetupSolohinLexProfile(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 13. RECOMPUTE USER RATINGS FROM ACTUAL DATA
        // QualityRating/QuantityRating must reflect actual PostReviews and
        // Posts — the hardcoded values from UpdateUserProfiles are just
        // placeholders, the real numbers come from the joins below.
        // ═══════════════════════════════════════════════════════════════════
        await RecomputeUserRatings(result);

        // ═══════════════════════════════════════════════════════════════════
        // 14. BOOST SOLOHIN-LEX METRICS FOR FULL ACHIEVEMENT TIER COVERAGE
        // Full tier palette on the achievements page: platinum (T4) comes
        // from the 11-year registration AND from 5000+ real game posts
        // (bulk archive insert inside the method — the tier IV showcase for
        // the "Игровые посты" chain), gold stays on bans/drops (10 each),
        // silver needs a QualityRating boost, bronze/locked come from
        // organic data.
        // ═══════════════════════════════════════════════════════════════════
        await BoostSolohinLexMetrics(now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 15. SEED SOLOHIN-LEX BANS FOR "РЕЗИНОВАЯ УТОЧКА" DEMO
        // The BansReceived chain requires actual Ban records in the DB (not
        // a denormalized counter), so we insert 10 of them — gold tier,
        // gold/silver/bronze tiers are earned, platinum (T4=30) stays
        // locked. Together with game posts/rating this gives the full palette
        // of tier plaques on the achievements page.
        // ═══════════════════════════════════════════════════════════════════
        await SeedSolohinLexBans(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 16. SEED SOLOHIN-LEX DROPS FOR "ДРОПЫ" DEMO
        // The GameDrops chain reads Characters.Status=Retired+IsPlayerLeft,
        // so actual characters must be inserted. 10 of them → gold tier,
        // platinum (T4=30) locked.
        // ═══════════════════════════════════════════════════════════════════
        await SeedSolohinLexDrops(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 17. SEED SOLOHIN-LEX PLAYER CHARACTERS FOR PROFILE GAMES TABLE
        // The "Игрок" mode of the profile games table shows the character
        // status column, so SolohinLex needs deterministic characters with
        // different statuses: active / dead / left alongside actives, plus
        // a game with only former characters and a game with only an
        // application under review — the latter two are visible only via
        // the playerParticipation=Any scope of the games player filter.
        // ═══════════════════════════════════════════════════════════════════
        await SeedSolohinLexPlayerCharacters(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 18. SEED TICKETS ("обращения") FOR THE MODERATION PAGES
        // A mix of subtypes/statuses/author kinds (authenticated complaint
        // with a target, guest support request with GuestEmail, spam) so the
        // moderation ticket pages have data for every role scope.
        // ═══════════════════════════════════════════════════════════════════
        await SeedTickets(users, now, result);

        result.Details.Add($"Comprehensive seed completed at {now:dd.MM.yyyy в HH:mm}");
        return result;
    }
}
