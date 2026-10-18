using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// No C# file and no method grows past its limit, and the ones already past it
/// on the day the limits arrived are held at the length they had then.
/// </summary>
/// <remarks>
/// Size is what an agent pays for on every read: a file is read whole, comments
/// included, so lines are counted the way the reader meets them. A limit the tree
/// cannot meet on its first day gets switched off on its second, so the files and
/// methods past it are frozen rather than exempted, the way the client's
/// sizeRatchet.js freezes its own.
///
/// Every record is the measured length and is checked for equality from both
/// ends, as SuppressedRulesShould checks its list: past the limit with no record
/// fails, a record that has grown fails, and so does one that has shrunk or has
/// nothing past the limit behind it any more, because a record left above its
/// file is room for the file to grow back into.
///
/// The registers only shrink. A new record widens the exceptions and takes the
/// owner's word; the lawful move is to split the code, and a record may follow
/// a split onto one of the parts with a number no larger than before. Nothing
/// here can be switched off by a pragma: there is no analyzer to suppress.
///
/// Generated migrations are left out: EF writes them, and their length is the
/// model's, not anyone's prose.
/// </remarks>
public class SizeRatchetShould
{
    private const int SourceFileLimit = 600;

    /// <summary>
    /// Kept apart from the product's limit, because a test file carries its
    /// fixtures and cases in one place and the two may be tuned separately.
    /// </summary>
    private const int TestFileLimit = 600;

    private const int MethodLimit = 80;

    /// <summary>A file past its limit, at its length in lines.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Lines">Measured length, a final newline adding no line.</param>
    private sealed record FileRecord(string File, int Lines);

    /// <summary>A method past its limit, at its length in lines.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Method">The method's name, with #2, #3 for later overloads.</param>
    /// <param name="Lines">Lines from its signature to the end of its body.</param>
    private sealed record MethodRecord(string File, string Method, int Lines);

    private static readonly FileRecord[] RecordedFiles =
    [
        new("src/DM.Domain.Account/Features/Authentication/AuthenticationService.cs", 616),
        new("src/DM.Domain.Blog/Features/Blogs/BlogService.cs", 874),
        new("src/DM.Domain.Core/Enums/EventType.cs", 803),
        new("src/DM.Domain.Game/Features/Games/GameEntityDtos.cs", 1126),
        new("src/DM.Domain.Game/Features/Games/GameModels.cs", 1459),
        new("src/DM.Domain.Game/Features/Games/GameService.cs", 904),
        new("src/DM.Infrastructure.Persistence/DmDbContext.cs", 3509),
        new("src/DM.Infrastructure.Persistence/Repositories/Blog/BlogRepository.cs", 777),
        new("src/DM.Infrastructure.Persistence/Repositories/Forum/TopicRepository.cs", 756),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameRepository.cs", 1309),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/PostRepository.cs", 928),
        new("src/DM.Infrastructure.Persistence/Repositories/Personal/UserRepository.cs", 1139),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Games.cs", 1209),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Moderation.cs", 724),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Reviews.cs", 741),
        new("src/DM.Web.API/Features/Community/Users/UserDtos.cs", 679),
        new("src/DM.Web.API/Features/General/Upload/UploadApiService.cs", 631),
        new("test/DM.Architecture.Tests/AgentToolingShould.cs", 769),
        new("test/DM.Architecture.Tests/CompositionRootShould.cs", 659),
        new("test/DM.Architecture.Tests/ConsumerMetricsShould.cs", 707),
        new("test/DM.Architecture.Tests/DeploymentConfigurationShould.cs", 2417),
        new("test/DM.Domain.Account.Tests/Features/Authentication/AuthenticationServiceShould.cs", 760),
        new("test/DM.Domain.Account.Tests/Features/UsernameChange/UsernameChangeServiceShould.cs", 697),
        new("test/DM.Domain.Blog.Tests/Authorization/BlogIntentionResolverShould.cs", 663),
        new("test/DM.Domain.Forum.Tests/Features/Topics/TopicServiceShould.cs", 704),
        new("test/DM.Domain.Game.Tests/Authorization/GameIntentionResolverShould.cs", 822),
        new("test/DM.Domain.Game.Tests/Authorization/RoomIntentionResolverShould.cs", 670),
        new("test/DM.Domain.Game.Tests/Features/PostReviews/PostReviewServiceShould.cs", 766),
        new("test/DM.Domain.Game.Tests/Features/Posts/PostServiceShould.cs", 626),
        new("test/DM.Domain.Moderation.Tests/Features/Tickets/TicketServiceShould.cs", 1396),
        new("test/DM.Domain.Moderation.Tests/Features/Warnings/BanServiceShould.cs", 1034),
        new("test/DM.Domain.Moderation.Tests/Features/Warnings/WarningServiceShould.cs", 1332),
        new("test/DM.Infrastructure.Core.Tests/BbGrammarShould.cs", 630),
        new("test/DM.Infrastructure.Core.Tests/BbParserWrapperShould.cs", 674),
        new("test/DM.Infrastructure.Core.Tests/PermissionFilteringVisitorShould.cs", 620),
        new("test/DM.Infrastructure.Persistence.Tests/Repositories/Moderation/WarningEvidenceShould.cs", 616),
        new("test/DM.Web.API.IntegrationTests/Controllers/Account/TwoFactorShould.cs", 606),
        new("test/DM.Web.API.IntegrationTests/Controllers/Game/GameControllerShould.cs", 1007),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/BlacklistRuleShould.cs", 834),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/HiddenModuleShould.cs", 799),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/OpenApiContractShould.cs", 801),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/TopicAndReviewFlagShould.cs", 646),
        new("test/DM.Web.API.IntegrationTests/Controllers/Moderation/WarningControllerShould.cs", 1539),
        new("test/DM.Web.API.IntegrationTests/DatabaseFixture.cs", 657),
        new("test/DM.Web.API.IntegrationTests/Repositories/LastCommentPointerShould.cs", 648),
        new("test/DM.Web.API.IntegrationTests/Repositories/PostRepositoryShould.cs", 952),
    ];

    private static readonly MethodRecord[] RecordedMethods =
    [
        new("src/BBCodeParser/BbParser.cs", "Parse", 110),
        new("src/BBCodeParser/Nodes/ErasedLines.cs", "Trim", 133),
        new("src/DM.Domain.Account/Features/Authentication/AuthenticationService.cs", "Authenticate", 179),
        new("src/DM.Domain.Account/Features/Authentication/AuthenticationService.cs", "Authenticate#2", 89),
        new("src/DM.Domain.Account/Features/Authentication/AuthenticationService.cs", "CompleteSecondFactor", 108),
        new("src/DM.Domain.Account/Features/PasswordChange/PasswordChangeService.cs", "Change", 85),
        new("src/DM.Domain.Blog/Authorization/BlogIntentionResolver.cs", "IsAllowed", 134),
        new("src/DM.Domain.Forum/Features/Digests/PeriodDigestProcessor.cs", "EnsureDigest", 113),
        new("src/DM.Domain.Forum/Features/Topics/TopicService.cs", "UpdateAsync", 91),
        new("src/DM.Domain.Game/Authorization/GameIntentionResolver.cs", "IsAllowed#2", 144),
        new("src/DM.Domain.Game/Features/Characters/CharacterAttributeValueFiller.cs", "Fill", 82),
        new("src/DM.Domain.Game/Features/Characters/CharacterService.cs", "ChangeStatusAsync", 106),
        new("src/DM.Domain.Game/Features/Games/GameService.cs", "CreateAsync", 103),
        new("src/DM.Domain.Game/Features/Games/GameService.cs", "GetGamesAsync", 107),
        new("src/DM.Domain.Moderation/Features/Tickets/CreateTicketIntakeValidator.cs", "CreateTicketIntakeValidator", 86),
        new("src/DM.Domain.Moderation/Features/Tickets/TicketService.cs", "CreateIntakeTicket", 90),
        new("src/DM.Domain.Moderation/Features/Warnings/BanService.cs", "CreateBan", 101),
        new("src/DM.Domain.Moderation/Features/Warnings/WarningService.cs", "CreateWarning", 98),
        new("src/DM.Domain.Personal/Features/Notifications/NotificationDestination.cs", "DestinationOf", 83),
        new("src/DM.Domain.Personal/Features/Profiles/UserService.cs", "UpdateAsync", 96),
        new("src/DM.Infrastructure.Core/Logging/LoggingConfiguration.cs", "AddDmLogging", 180),
        new("src/DM.Infrastructure.Core/Storage/ImageProcessingService.cs", "ProcessAsync", 137),
        new("src/DM.Infrastructure.Persistence/DmDbContext.cs", "OnModelCreating", 2895),
        new("src/DM.Infrastructure.Persistence/PersistenceRegistrationExtensions.cs", "AddDmPersistence", 145),
        new("src/DM.Infrastructure.Persistence/Repositories/Blog/BlogRepository.cs", "GetFilteredQuery", 104),
        new("src/DM.Infrastructure.Persistence/Repositories/Community/CommunityStatsRepository.cs", "GetLeaderboards", 198),
        new("src/DM.Infrastructure.Persistence/Repositories/Community/CommunityStatsRepository.cs", "GetLiveStats", 94),
        new("src/DM.Infrastructure.Persistence/Repositories/Community/PollRepository.cs", "Update", 90),
        new("src/DM.Infrastructure.Persistence/Repositories/Forum/TopicRepository.cs", "Get", 108),
        new("src/DM.Infrastructure.Persistence/Repositories/Forum/TopicRepository.cs", "Update", 142),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/CharacterRepository.cs", "Update", 94),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameAttributeSchemaRepository.cs", "Replace", 93),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameInvitationRepository.cs", "GetUsers", 87),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameRepository.cs", "ApplyFilters", 195),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameRepository.cs", "ApplySorting", 152),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameRepository.cs", "Create", 84),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameRepository.cs", "EnrichGamesAsync", 166),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/GameRepository.cs", "Update", 115),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/PostRepository.cs", "GetRated", 152),
        new("src/DM.Infrastructure.Persistence/Repositories/Game/PostReviewRepository.cs", "UpdateAsync", 86),
        new("src/DM.Infrastructure.Persistence/Repositories/Moderation/WarningEntityResolver.cs", "DescribeComments", 165),
        new("src/DM.Infrastructure.Persistence/Repositories/Personal/UserRepository.cs", "BuildPageQuery", 103),
        new("src/DM.Infrastructure.Persistence/Repositories/Personal/UserRepository.cs", "GetQuery", 110),
        new("src/DM.Infrastructure.Persistence/Repositories/Personal/UserRepository.cs", "PopulateListCounts", 90),
        new("src/DM.Infrastructure.Persistence/Repositories/Personal/UserRepository.cs", "PopulatePostReviewCounts", 280),
        new("src/DM.Infrastructure.Persistence/Repositories/Search/MessageSearchRepository.cs", "Search", 181),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Blogs.cs", "CreateBlogs", 311),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Characters.cs", "SeedCharactersAndPostsForGames", 116),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Characters.cs", "SeedSolohinLexDrops", 82),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Characters.cs", "SeedSolohinLexPlayerCharacters", 129),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Forum.cs", "CreateForumContent", 306),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Games.cs", "CreateGames", 1185),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Leaderboards.cs", "CreateLikes", 86),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Leaderboards.cs", "EnsureLeaderboardCoverage", 244),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Messaging.cs", "CreateDirectChats", 111),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Messaging.cs", "CreateGlobalChatEvents", 173),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Moderation.cs", "SeedTickets", 133),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Moderation.cs", "SeedWarnings", 88),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Polls.cs", "CreatePolls", 415),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.PostReviews.cs", "SpreadPostReviews", 212),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.RemovedAccount.cs", "SeedRemovedAccount", 202),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Reviews.cs", "CreateReviews", 720),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.UserProfile.cs", "BoostSolohinLexMetrics", 89),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.UserProfile.cs", "SetupSolohinLexProfile", 266),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Users.cs", "CreateUserSubscriptions", 94),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.Users.cs", "SeedTestUsers", 171),
        new("src/DM.Tools.Seeder/Seeding/DataSeeder.cs", "SeedComprehensiveData", 216),
        new("src/DM.Web.API/Features/Account/Authentication/AuthenticationApiService.cs", "Login", 110),
        new("src/DM.Web.API/Features/General/Upload/UploadApiService.cs", "DirectUploadCore", 125),
        new("src/DM.Web.API/Features/Moderation/Profiles/ModeratedProfileApiService.cs", "GetModeratedProfile", 84),
        new("src/DM.Web.API/Features/Personal/Webhooks/WebhookController.cs", "HandleWebhook", 100),
        new("src/DM.Web.API/Shared/Authentication/WebAuthenticationService.cs", "Authenticate", 95),
        new("src/DM.Web.API/Startup.cs", "Configure", 156),
        new("src/DM.Web.API/Startup.cs", "ConfigureServices", 167),
        new("src/DM.Workers.NotificationDispatcher/Dispatching/NotificationProcessor.cs", "Process", 96),
        new("src/DM.Workers.NotificationDispatcher/Startup.cs", "ConfigureServices", 87),
        new("test/DM.Architecture.Tests/AgentToolingShould.cs", "RunEveryGateTheWorkflowRunsThatBelongsOnThisMachine", 89),
        new("test/DM.Architecture.Tests/DocumentedStatusCodesShould.cs", "MatchTheAttributesTheSchemaIsBuiltFrom", 85),
        new("test/DM.Architecture.Tests/QueryVocabularyShould.cs", "Published", 92),
        new("test/DM.Domain.Game.Tests/Features/Games/GameServiceShould.cs", "GameServiceShould", 89),
        new("test/DM.Domain.Game.Tests/Features/Games/GameStatusTransitionShould.cs", "GameStatusTransitionShould", 85),
        new("test/DM.Infrastructure.Core.Tests/BbGrammarShould.cs", "Piece", 129),
        new("test/DM.Infrastructure.Core.Tests/BbGrammarShould.cs", "WithoutErasedLines", 93),
        new("test/DM.Testing/Markup/HiddenTextOracle.cs", "Read", 89),
        new("test/DM.Web.API.IntegrationTests/Controllers/Game/GameTurnMarkerShould.cs", "Seed", 96),
        new("test/DM.Web.API.IntegrationTests/Controllers/Game/PostAttachmentAccessShould.cs", "InitializeAsync", 102),
        new("test/DM.Web.API.IntegrationTests/Controllers/Game/RoomListShould.cs", "Seed", 82),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/BlacklistRuleShould.cs", "SeedBlacklistedAsync", 197),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/BlacklistRuleShould.cs", "SendAsync", 83),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/CanCommentFlagShould.cs", "SeedAsync", 111),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/DiscussionReadingShould.cs", "SeedAsync", 97),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/HiddenModuleShould.cs", "AddGame", 109),
        new("test/DM.Web.API.IntegrationTests/Controllers/General/RemovedAuthorContentShould.cs", "SeedRemovedAuthorAsync", 117),
        new("test/DM.Web.API.IntegrationTests/Controllers/Messaging/ChatControllerShould.cs", "SumTheUnreadMarkersOfEveryConversationOfTheReader", 90),
        new("test/DM.Web.API.IntegrationTests/Controllers/Moderation/ContestSeriesAwardsShould.cs", "ListRecipientsOfTheSeriesOnly", 137),
        new("test/DM.Web.API.IntegrationTests/CustomWebApplicationFactory.cs", "ConfigureWebHost", 125),
        new("test/DM.Web.API.IntegrationTests/DatabaseFixture.cs", "SeedTestimonials", 86),
        new("test/DM.Web.API.IntegrationTests/Repositories/OutboxDeliveryShould.cs", "DeliverEverythingStoredWhileTheBrokerWasDown", 90),
    ];

    /// <summary>
    /// Every file past its limit is recorded at its length, and every record has a
    /// file at that length behind it.
    /// </summary>
    [Fact]
    public void HoldEveryFilePastItsLimitAtItsRecordedLength()
    {
        var measured = Sources()
            .Select(file => (File: file.Path, Lines: LineCount(file.Text), file.Limit))
            .Where(file => file.Lines > file.Limit)
            .ToDictionary(file => file.File, file => file.Lines, StringComparer.Ordinal);

        var problems = Disagreements(
            RecordedFiles.ToDictionary(entry => entry.File, entry => entry.Lines, StringComparer.Ordinal),
            measured,
            key => $"new(\"{key}\", {measured.GetValueOrDefault(key)}),");

        // Joined, so that the message lists every one rather than the first few.
        string.Join(Environment.NewLine, problems).Should().BeEmpty(
                "a file is held at its recorded length: split a file past the limit rather " +
                "than record it (a new record needs the owner's word), and lower a record " +
                "the moment its file shrinks");
    }

    /// <summary>
    /// Every method past its limit is recorded at its length, and every record has a
    /// method at that length behind it.
    /// </summary>
    [Fact]
    public void HoldEveryMethodPastItsLimitAtItsRecordedLength()
    {
        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in Sources())
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var span in SourceText.MemberSpans(file.Text))
            {
                var count = seen[span.Name] = seen.GetValueOrDefault(span.Name) + 1;
                var name = count == 1 ? span.Name : $"{span.Name}#{count}";
                if (span.Lines > MethodLimit)
                {
                    measured[Key(file.Path, name)] = span.Lines;
                }
            }
        }

        var problems = Disagreements(
            RecordedMethods.ToDictionary(entry => Key(entry.File, entry.Method), entry => entry.Lines, StringComparer.Ordinal),
            measured,
            key => $"new(\"{key.Replace(" :: ", "\", \"", StringComparison.Ordinal)}\", {measured.GetValueOrDefault(key)}),");

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
                "a method is held at its recorded length: split a method past the limit " +
                "rather than record it (a new record needs the owner's word), and lower a " +
                "record the moment its method shrinks");
    }

    private static string Key(string file, string method) => $"{file} :: {method}";

    /// <summary>
    /// The C# files of src and test a commit would carry, each with its limit.
    /// </summary>
    /// <remarks>
    /// Asked of git, as DocumentSizeShould asks: a walk of the disk also reads
    /// what the ignore list keeps out, such as the dependencies of the client,
    /// so a run here would measure files that a run in CI never sees.
    /// </remarks>
    private static IEnumerable<(string Path, string Text, int Limit)> Sources()
    {
        var root = DM.Testing.RepositoryLayout.Root;
        foreach (var path in RepositoryFiles.CommittableFiles())
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            int? limit = relative.StartsWith("src/", StringComparison.Ordinal) ? SourceFileLimit
                : relative.StartsWith("test/", StringComparison.Ordinal) ? TestFileLimit
                : null;
            if (limit is null
                || !relative.EndsWith(".cs", StringComparison.Ordinal)
                || relative.Contains("/Migrations/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (relative, File.ReadAllText(path), limit.Value);
        }
    }

    /// <summary>Counted the way ESLint's max-lines counts: a final newline adds no line.</summary>
    private static int LineCount(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return lines.Length > 1 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
    }

    /// <summary>One line per measure on which the record and the tree disagree.</summary>
    private static List<string> Disagreements(
        IReadOnlyDictionary<string, int> recorded,
        IReadOnlyDictionary<string, int> measured,
        Func<string, string> entryFor)
    {
        var problems = new List<string>();
        foreach (var key in recorded.Keys.Union(measured.Keys).Order(StringComparer.Ordinal))
        {
            var hasRecord = recorded.TryGetValue(key, out var record);
            var hasSize = measured.TryGetValue(key, out var size);
            if (hasRecord && hasSize && record == size)
            {
                continue;
            }

            problems.Add((hasRecord, hasSize) switch
            {
                (false, _) => $"{key}: {size} lines is past the limit and has no record ({entryFor(key)})",
                (_, false) => $"{key}: back within the limit or gone: drop the record of {record}",
                _ when size > record => $"{key}: grew from {record} to {size} lines",
                _ => $"{key}: shrank from {record} to {size} lines: lower the record to {size}",
            });
        }

        return problems;
    }
}
