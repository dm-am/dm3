using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Features.Moderation.Warnings;
using DM.Web.API.Shared.Http;
using DM.Web.API.Shared.Sorting;
using Xunit;
using static DM.Architecture.Tests.ClientCopies;
using AccountPasswordResetTokenInfo = DM.Domain.Account.Features.PasswordChange.PasswordResetTokenInfo;
using AccountPendingInfo = DM.Domain.Account.Features.Registration.PendingInfoResult;
using AccountUsernameChangeApprovalInfo = DM.Domain.Account.Features.UsernameChange.UsernameChangeApprovalInfo;

namespace DM.Architecture.Tests;

/// <summary>
/// The names the client and the server exchange are spelled the same on both sides: headers,
/// the keys of a refusal, the titles a refusal is told apart by, statuses, filters and sort fields.
/// </summary>
/// <remarks>
/// A misspelt name is no error on either side: the header is not read, the key is undefined, the
/// sort falls back to the default. The client spells each once; both spellings are read here.
/// </remarks>
public class ClientWireNamesShould
{
    private static readonly Assembly Host = typeof(DM.Web.API.Startup).Assembly;

    [Fact]
    public void SendTheHeadersTheServerReads()
    {
        Constant("shared/api/tokenHeaders.ts", "X_DM_ACCOUNT_TOKEN").Should().Be(TokenHeaders.Account);
        Constant("shared/api/tokenHeaders.ts", "X_DM_TICKET_TOKEN").Should().Be(TokenHeaders.Ticket);
        Constant("shared/api/audience.ts", "X_DM_AUDIENCE").Should().Be(BbAudienceHeader.HeaderName);

        var idempotency = Member<string>(Host, "DM.Web.API.Features.General.Upload.UploadApiService", "IdempotencyHeader");
        Source("shared/api/client.ts").Should().Contain($"headers[\"{idempotency}\"]",
            "a retried upload that names the key differently is stored twice");
    }

    [Fact]
    public void AskForAnAudienceTheServerRenders()
    {
        var rendered = Enum.GetValues<RenderAudience>().Select(BbAudienceHeader.Serialize).ToArray();
        var block = Regex.Match(Source("shared/api/audience.ts"),
            @"RENDER_AUDIENCE = \{(?<block>[^}]+)\}").Groups["block"].Value;
        var asked = Regex.Matches(block, @":\s*""(?<value>[^""]+)""").Select(match => match.Groups["value"].Value).ToArray();

        asked.Should().NotBeEmpty("the client names the audiences it asks for in RENDER_AUDIENCE");
        rendered.Should().Contain(asked, "an audience the server does not know is rendered as the default one");
    }

    /// <summary>A key the server writes into a refusal, and how the client spells it where it reads it.</summary>
    public static TheoryData<string, string, string> RefusalKeys => new()
    {
        { "_pendingActivation", "features/auth/ui/LoginForm.vue", "errors[\"_pendingactivation\"]" },
        { "banExpiresUtc", "features/auth/lib/loginRefusal.ts", ".banExpiresUtc" },
        { "retryAfterSeconds", "shared/api/models/common/index.ts", "retryAfterSeconds?: number" },
    };

    [Theory]
    [MemberData(nameof(RefusalKeys))]
    public void ReadTheKeyTheRefusalCarries(string key, string file, string read)
    {
        RepositoryFiles.Read("src", "DM.Web.API", "Features", "Account", "Authentication", "AuthenticationApiService.cs")
            .Should().Contain($"[\"{key}\"]", "the sign-in refusal carries the key");

        Source(file).Should().Contain(read,
            $"the client reads {key} where it is written; field keys arrive lower-cased by parseApiErrors");
    }

    [Fact]
    public void TellTheSignInRefusalsApartByTheServerTitles()
    {
        const string file = "features/auth/lib/loginRefusal.ts";

        Constant(file, "BANNED_TITLE").Should().Be(RefusalMessage.AccountBanned);
        Constant(file, "REMOVED_TITLE").Should().Be(RefusalMessage.AccountRemoved);
        Constant(file, "LOCKED_TITLE").Should().Be(RefusalMessage.LoginLocked,
            "a title the server words differently turns the refusal into the general one");
    }

    /// <summary>The client says an unfinished registration where the server's field error is absent, in its words.</summary>
    [Fact]
    public void SayAnUnfinishedRegistrationInTheServersWords() =>
        Constant("features/auth/lib/loginRefusal.ts", "REGISTRATION_PENDING")
            .Should().Be(RefusalMessage.RegistrationPending,
                "the sign-in refusal and the check of the address name one state, and two wordings would drift");

    public static TheoryData<string, Type> TokenStatuses => new()
    {
        { "PendingInfo", typeof(AccountPendingInfo) },
        { "PasswordResetTokenInfo", typeof(AccountPasswordResetTokenInfo) },
        { "UsernameChangeApprovalInfo", typeof(AccountUsernameChangeApprovalInfo) },
    };

    [Theory]
    [MemberData(nameof(TokenStatuses))]
    public void ExpectTheStatusesALinkCheckAnswersWith(string clientType, Type answer)
    {
        var declared = Regex.Match(Source("shared/api/models/account/index.ts"),
            @"export type " + clientType + @" = \{[^}]*?status: (?<union>[^;]+);").Groups["union"].Value;

        Literals(declared).Should().BeEquivalentTo(Statuses(answer),
            "a status the page does not expect falls through to its last branch");
    }

    [Fact]
    public void FilterViolatorsByTheValuesTheServerTakes()
    {
        var accepted = typeof(ViolatorsQuery).GetProperty(nameof(ViolatorsQuery.BanState))!
            .GetCustomAttribute<RegularExpressionAttribute>()!.Pattern
            .Trim('^', '$', '(', ')')
            .Split('|');

        var union = Regex.Match(Source("entities/moderation/api/moderationApi.ts"),
            @"export type ViolatorsFilter = (?<union>[^;]+);").Groups["union"].Value;

        Literals(union).Should().BeEquivalentTo(accepted, "a filter the query refuses answers 400");
    }

    /// <summary>A client list of sort fields and the query type that names what it accepts.</summary>
    public static TheoryData<string, string, Type> SortFields => new()
    {
        { "features/blog-filter/model/types.ts", "SORT_OPTIONS", typeof(DM.Web.API.Features.Blog.Blogs.BlogsQuery) },
        { "features/comment-filter/model/types.ts", "SORT_OPTIONS", typeof(DM.Domain.Core.Comments.CommentsQuery) },
        { "features/game-filter/model/types.ts", "SORT_OPTIONS", typeof(DM.Web.API.Features.Game.Games.GamesQuery) },
        { "features/poll-filter/model/types.ts", "SORT_OPTIONS", typeof(DM.Web.API.Features.Community.Polls.PollsQuery) },
        { "features/testimonial-filter/model/types.ts", "SORT_OPTIONS", typeof(DM.Domain.Community.Features.WebsiteTestimonials.WebsiteTestimonialsQuery) },
        { "features/topic-filter/model/types.ts", "SORT_OPTIONS", typeof(DM.Web.API.Features.Forum.Topics.TopicsQuery) },
        { "entities/game/model/postTypes.ts", "RatedPostsQuery", typeof(DM.Domain.Game.Features.Posts.PostsQuery) },
        { "entities/game/model/postTypes.ts", "RoomPostsFilter", typeof(DM.Domain.Game.Features.Posts.RoomPostsQuery) },
    };

    [Theory]
    [MemberData(nameof(SortFields))]
    public void SortByTheFieldsTheServerAccepts(string file, string declaration, Type query)
    {
        var source = Source(file);
        var declared = Regex.Match(source, @"\b(?:const|type|interface)\s+" + declaration + @"\b");
        declared.Success.Should().BeTrue($"{file} declares {declaration}");
        var at = declared.Index;

        var end = declaration.StartsWith("SORT", StringComparison.Ordinal)
            ? Regex.Match(source[at..], @"\n\](?: as const)?;").Index
            : source.IndexOf('}', at) - at;
        end.Should().BePositive($"{declaration} in {file} closes");
        var block = source.Substring(at, end);
        var fields = declaration.StartsWith("SORT", StringComparison.Ordinal)
            ? Regex.Matches(block, @"value: ""(?<value>[^""]+)""").Select(match => match.Groups["value"].Value).ToArray()
            : Literals(Regex.Match(block, @"sortBy\?: (?<union>[^;]+);").Groups["union"].Value);

        fields.Should().NotBeEmpty($"{declaration} lists the fields the client sorts by");
        fields.Where(field => !SortVocabulary.Allows(SortVocabulary.FieldsOf(query)!, field))
            .Should().BeEmpty($"{query.Name} refuses a sort field it does not name");
    }

    private const string ModuleMirrors = "shared/api/models/common/module-status.ts";

    /// <summary>A client mirror of a server enum, the file it lives in and the enum it mirrors.</summary>
    public static TheoryData<string, string, Type> ModuleEnums => new()
    {
        { ModuleMirrors, "ModuleStatus", typeof(DM.Domain.Core.Enums.ModuleStatus) },
        { ModuleMirrors, "ClosedReason", typeof(DM.Domain.Core.Enums.ClosedReason) },
        { ModuleMirrors, "PremoderationStatus", typeof(DM.Domain.Core.Enums.PremoderationStatus) },
        { "entities/game/model/gameTypes.ts", "GamePremoderationTransition", typeof(DM.Domain.Core.Statuses.ModulePremoderationTransition) },
        { "entities/blog/model/types.ts", "BlogPremoderationTransition", typeof(DM.Domain.Core.Statuses.ModulePremoderationTransition) },
        { "entities/ticket/api/ticketApi.ts", "TicketSubtype", typeof(DM.Domain.Core.Enums.TicketSubtype) },
        { "entities/ticket/api/ticketApi.ts", "TicketStatus", typeof(DM.Domain.Core.Enums.TicketStatus) },
        { "shared/api/models/achievements/index.ts", "AchievementMetric", typeof(DM.Domain.Core.Enums.AchievementMetric) },
    };

    [Theory]
    [MemberData(nameof(ModuleEnums))]
    public void MirrorTheEnumTheServerSerializes(string file, string mirror, Type serverEnum)
    {
        var source = Source(file);
        var asObject = Regex.Match(source, @"export (?:const " + mirror + @" =|enum " + mirror + @") \{(?<block>[^}]+)\}");
        var values = asObject.Success
            ? Regex.Matches(asObject.Groups["block"].Value, @"[:=]\s*""(?<value>[^""]+)""").Select(match => match.Groups["value"].Value).ToArray()
            : Literals(Regex.Match(source, @"export type " + mirror + @" =(?<union>[^;]+);").Groups["union"].Value);

        values.Should().BeEquivalentTo(Enum.GetNames(serverEnum),
            "the API writes an enum by its member names, and a value the client does not know falls through every branch");
    }

    [Fact]
    public void CountEveryAchievementMetricOnBothSides()
    {
        var metrics = Enum.GetNames<DM.Domain.Core.Enums.AchievementMetric>();
        var resolver = RepositoryFiles.Read(
            "src", "DM.Domain.Community", "Features", "Achievements", "AchievementMetricResolver.cs");
        var client = Source("entities/achievement/lib/getMetricValue.ts");

        // Both sides answer 0 for a metric they do not name, so the side that misses one stays silent.
        using var bothSides = new AssertionScope();
        metrics.Where(metric => !resolver.Contains($"AchievementMetric.{metric} =>", StringComparison.Ordinal))
            .Should().BeEmpty("the server awards a tier by the value AchievementMetricResolver reads");
        metrics.Where(metric => !client.Contains($"case AchievementMetric.{metric}:", StringComparison.Ordinal))
            .Should().BeEmpty("the profile shows the progress getMetricValue reads, toward the tier the server awards");
    }

    /// <summary>A string constant of the client.</summary>
    private static string Constant(string file, string name) =>
        Regex.Match(Source(file), @"\bconst\s+" + Regex.Escape(name) + @"\s*=\s*""(?<value>[^""]*)""")
            .Groups["value"].Value;

    /// <summary>The string literals of a TypeScript union.</summary>
    private static string[] Literals(string union) =>
        Regex.Matches(union, @"""(?<value>[^""]+)""").Select(match => match.Groups["value"].Value).ToArray();

    /// <summary>Every status the factories of a link check answer type produce.</summary>
    private static IEnumerable<string> Statuses(Type answer) => answer
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.ReturnType == answer)
        .Select(method => method.Invoke(null, method.GetParameters().Select(_ => (object?)string.Empty).ToArray())!)
        .Select(result => (string)answer.GetProperty("Status")!.GetValue(result)!)
        .Distinct();
}
