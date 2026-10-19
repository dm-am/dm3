using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DM.Domain.Blog.Features.Popularity;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Game.Features.Popularity;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

/// <summary>
/// Development data seeder. Writes straight to the stores: it sets the timestamps, counters and
/// ratings the domain services derive and would refuse.
/// </summary>
internal sealed partial class DataSeeder
{
    private readonly DmDbContext _dbContext;
    private readonly ISecurityManager _securityManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IPollRepository _pollRepository;
    private readonly PublicIdService _publicIdService;
    private readonly IImageProcessingService _imageProcessing;
    private readonly Amazon.S3.IAmazonS3 _s3Client;
    private readonly IGamePopularityProcessor _gamePopularity;
    private readonly IBlogPopularityProcessor _blogPopularity;
    private readonly CdnConfiguration _cdnConfig;
    private readonly ISymmetricCryptoService _cryptoService;

    /// <summary>The account the stand is looked at through.</summary>
    private const string PrimaryLogin = "SolohinLex";

    /// <summary>The password of every development account.</summary>
    private const string TestPassword = "Test123!";

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
        ISecurityManager securityManager,
        IGuidFactory guidFactory,
        SeedDeterminism determinism,
        IPollRepository pollRepository,
        PublicIdService publicIdService,
        IImageProcessingService imageProcessing,
        Amazon.S3.IAmazonS3 s3Client,
        IGamePopularityProcessor gamePopularity,
        IBlogPopularityProcessor blogPopularity,
        IOptions<CdnConfiguration> cdnOptions,
        ISymmetricCryptoService cryptoService)
    {
        _dbContext = dbContext;
        _securityManager = securityManager;
        _guidFactory = guidFactory;
        _pollRepository = pollRepository;
        _publicIdService = publicIdService;
        _imageProcessing = imageProcessing;
        _s3Client = s3Client;
        _gamePopularity = gamePopularity;
        _blogPopularity = blogPopularity;
        _cdnConfig = cdnOptions.Value;
        _cryptoService = cryptoService;
        _random = determinism.Random;
        _now = determinism.Epoch;
    }

    /// <summary>
    /// Start of the window the site means by "за неделю": seven days back.
    /// </summary>
    /// <remarks>
    /// The homepage block filters by this boundary (getWeekStartUtc): the showcase post lands inside, coverage
    /// stays out. Rolling, not the calendar Monday, which empties the block once the seed is a week old.
    /// </remarks>
    private static DateTimeOffset WeekStartUtc(DateTimeOffset moment) => moment.AddDays(-7);

    /// <summary>
    /// A fixed identifier, number within series: drawn from the factory, it would move every identifier drawn after it.
    /// </summary>
    private static Guid FixedGuid(int series, int number) => new($"00000000-0000-0000-{series:x4}-{number:x12}");

    /// <summary>
    /// Seed the content set: forum topics and comments, games, characters, posts,
    /// blogs, publications, chat messages, reviews, polls and ratings.
    /// Requires the base users, so run <see cref="SeedTestUsers"/> first.
    /// </summary>
    /// <remarks>
    /// The order of the stages is part of the fixture: SeedDeterminism pins every row to the order of draws.
    /// </remarks>
    /// <returns>Per-aggregate creation counts</returns>
    public async Task<ComprehensiveSeedResult> SeedComprehensiveData()
    {
        var result = new ComprehensiveSeedResult();

        // Who masters, writes and plays where follows this order, so it has to be total. Sorted here, ordinally:
        // ORDER BY would leave the tiebreak to the collation of the cluster.
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

        var admin = users.FirstOrDefault(u => u.Role == UserRole.Admin);
        var seniorMod = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator);
        var regularUsers = users.Where(u => u.Role == UserRole.RegularUser).ToList();

        if (admin == null || seniorMod == null || regularUsers.Count < 3)
        {
            result.Details.Add("Missing required roles. Need Admin, SeniorMod, and at least 3 regular users.");
            return result;
        }

        var gameIds = await SeedContent(users, result);
        await SeedReviewsAndLikes(users, gameIds, result);
        await SeedSolohinLex(users, result);
        await SeedModeration(users, result);
        await SeedRemovedAccountAndNotifications(users, result);
        await SeedRareStates(users, result);
        await SeedFundraising(admin, result);

        result.Details.Add($"Comprehensive seed completed at {_now:dd.MM.yyyy в HH:mm}");
        return result;
    }

    /// <summary>
    /// The content the rest of the run reads: profiles, the forum, the games,
    /// the blogs and the chats.
    /// </summary>
    /// <returns>The games of the seed, for the reviews to name.</returns>
    private async Task<List<Guid>> SeedContent(List<DbUser> users, ComprehensiveSeedResult result)
    {
        await UpdateUserProfiles(users, result);

        await CreateForumContent(users, result);

        await AssignBoardModerators(users, result);

        // A game written through the context gets its sheet here; the repository writes it for the others.
        var gameIds = await CreateGames(users, result);
        await SeedGameAttributeSchemasAsync(result);

        await CreateBlogs(users, result);

        await CreateGlobalChatMessages(users, result);
        await CreateGlobalChatEvents(users, result);
        await CreateDirectChats(users, result);
        await ConnectBotChannels(users, result);

        return gameIds;
    }

    /// <summary>
    /// What readers leave on the content: reviews, subscriptions, polls,
    /// likes and post reviews, then the boards and the scores read from them.
    /// </summary>
    private async Task SeedReviewsAndLikes(
        List<DbUser> users, List<Guid> gameIds, ComprehensiveSeedResult result)
    {
        await CreateReviews(users, gameIds, result);

        await CreateUserSubscriptions(users, result);

        await CreatePolls(users, result);

        await _dbContext.SaveChangesAsync();

        await CreateLikes(users, result);

        // The organic reviews cluster in one game and one week; this pass rates posts months deep in most games.
        await SpreadPostReviews(users, result);

        // The statistics page defaults to the current month: top every board up to a full top-10.
        await EnsureLeaderboardCoverage(users, result);

        await UpdateGlobalChatLastMessage();

        await UpdatePopularityScores(result);
    }

    /// <summary>
    /// The primary account of the stand: its profile, the ratings recomputed
    /// from what the run wrote, the achievements, the bans, the drops and the
    /// characters of the profile games table.
    /// </summary>
    private async Task SeedSolohinLex(List<DbUser> users, ComprehensiveSeedResult result)
    {
        await SetupSolohinLexProfile(users, result);

        // The ratings from UpdateUserProfiles are placeholders; the real ones come from the reviews and posts.
        await RecomputeUserRatings(result);

        // Every tier plaque on the achievements page: platinum from the registration and 5000+ posts, gold from
        // bans and drops, silver from a QualityRating boost, bronze and locked from organic data.
        await BoostSolohinLexMetrics(result);

        // BansReceived counts Ban rows: 10 served bans earn bronze, silver and gold.
        await SeedSolohinLexBans(users, result);

        // GameDrops counts retired characters that left: 10 earn gold.
        await SeedSolohinLexDrops(users, result);

        // The "Игрок" mode of the profile games table shows the character status column.
        await SeedSolohinLexPlayerCharacters(users, result);
    }

    /// <summary>The tickets and the warnings the moderation pages are read against.</summary>
    private async Task SeedModeration(List<DbUser> users, ComprehensiveSeedResult result)
    {
        await SeedTickets(users, result);

        await SeedWarnings(users, result);
    }

    /// <summary>The removed account, and after everything else the notifications.</summary>
    private async Task SeedRemovedAccountAndNotifications(
        List<DbUser> users, ComprehensiveSeedResult result)
    {
        // After the content: the step removes the account it writes, and the steps above read live accounts.
        // The notifications after all the content, for the identifiers they draw: only the rare states come later.
        await SeedRemovedAccount(users, result);
        await SeedNotifications(users, result);
    }

    /// <summary>
    /// The states the drawn content never reaches, each written once into rows of its own: a finished
    /// game whose hidden parts an outsider must not read, a draft a role of the stand masters, pending
    /// invitations, a second factor and its removal by an administrator, a hidden award type.
    /// </summary>
    /// <remarks>
    /// Last of all: what it draws moves nothing written above, and its games take the serial numbers
    /// after every other game. Nothing above is changed, the rows are only added beside it.
    /// </remarks>
    private async Task SeedRareStates(List<DbUser> users, ComprehensiveSeedResult result)
    {
        await SeedHiddenGameContent(users, result);
        await SeedMentorDraft(users, result);
        await SeedPendingInvitations(users, result);
        await SeedSecondFactor(users, result);
        await SeedHiddenAwardType(result);
    }
}
