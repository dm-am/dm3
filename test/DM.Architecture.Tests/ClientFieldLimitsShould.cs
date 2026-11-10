using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Account.Configuration;
using DM.Domain.Community.Features.Catalogs;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Content;
using DM.Domain.Core.Uploads;
using DM.Domain.Moderation.Features.Tickets;
using DM.Web.API.Features.Account.Authentication;
using DM.Web.API.Features.Account.Credentials;
using DM.Web.API.Features.Account.Registration;
using DM.Web.API.Features.Blog.Blogs;
using DM.Web.API.Features.Blog.Publications;
using DM.Web.API.Features.Community.WebsiteTestimonials;
using DM.Web.API.Features.Moderation.Warnings;
using DM.Web.API.Features.Personal.Notepads;
using Xunit;
using static DM.Architecture.Tests.ClientCopies;

namespace DM.Architecture.Tests;

/// <summary>
/// The forms stop the typing, and the pickers refuse a file, where the server refuses.
/// </summary>
/// <remarks>
/// One constant per bound on the client, read out of its source and held to every server place
/// that answers the save with it.
/// </remarks>
public class ClientFieldLimitsShould
{
    private const string Blog = "shared/lib/constants/blog.ts";
    private const string GameConstants = "shared/lib/constants/game.ts";
    private const string User = "shared/lib/constants/user.ts";
    private const string Email = "features/auth/lib/useRegistrationEmail.ts";
    private const string CommunityConstants = "shared/lib/constants/community.ts";
    private const string Dice = "entities/game/ui/DiceComposer.vue";
    private const string Endorsement = "pages/profile/ProfileEndorsementForm.vue";
    private const string Ticket = "entities/ticket/lib/limits.ts";
    private const string Attachments = "features/upload/model/postAttachments.ts";
    private const string Code = "shared/lib/composables/useCodeInput.ts";
    private const string Notepad = "widgets/notepad/NotepadEntryEditor.vue";
    private const string UsernameInput = "entities/user/ui/UsernameInput.vue";
    private const string Catalog = "entities/achievement/lib/limits.ts";
    private const string ModerationReason = "features/moderation-actions/lib/limits.ts";

    private static readonly Assembly Account = typeof(DM.Domain.Account.Authorization.AccountIntention).Assembly;
    private static readonly Assembly Community = typeof(DM.Domain.Community.Authorization.PollIntention).Assembly;
    private static readonly Assembly Game = typeof(DM.Domain.Game.Authorization.GameIntention).Assembly;

    public static TheoryData<string, string, string, long> Copies => new()
    {
        { Blog, "BLOG_TITLE_MAX_LENGTH", "CreateBlogRequest.Title", Maximum(typeof(CreateBlogRequest), nameof(CreateBlogRequest.Title)) },
        { Blog, "BLOG_TITLE_MAX_LENGTH", "UpdateBlogRequest.Title", Maximum(typeof(UpdateBlogRequest), nameof(UpdateBlogRequest.Title)) },
        { Blog, "RUBRIC_TITLE_MAX_LENGTH", "CreateRubricRequest.Title", Maximum(typeof(CreateRubricRequest), nameof(CreateRubricRequest.Title)) },
        { Blog, "RUBRIC_TITLE_MAX_LENGTH", "UpdateRubricRequest.Title", Maximum(typeof(UpdateRubricRequest), nameof(UpdateRubricRequest.Title)) },
        { Blog, "PUBLICATION_TITLE_MAX_LENGTH", "CreatePublicationRequest.Title", Maximum(typeof(CreatePublicationRequest), nameof(CreatePublicationRequest.Title)) },
        { Blog, "PUBLICATION_TITLE_MAX_LENGTH", "UpdatePublicationRequest.Title", Maximum(typeof(UpdatePublicationRequest), nameof(UpdatePublicationRequest.Title)) },

        { GameConstants, "GAME_TITLE_MAX_LENGTH", "GameFieldLimits.TitleMaxLength", GameFieldLimits.TitleMaxLength },
        { GameConstants, "GAME_INFO_MIN_LENGTH", "GameFieldLimits.InfoMinLength", GameFieldLimits.InfoMinLength },
        { GameConstants, "ATTRIBUTE_SCHEMA_TITLE_MAX_LENGTH", "AttributeSchemaFieldLimits.TitleMaxLength",
            Member<int>(Game, "DM.Domain.Game.Features.AttributeSchemas.AttributeSchemaFieldLimits", "TitleMaxLength") },
        { GameConstants, "ROOM_TITLE_MAX_LENGTH", "RoomFieldLimits.TitleMaxLength",
            Member<int>(Game, "DM.Domain.Game.Features.Rooms.RoomFieldLimits", "TitleMaxLength") },
        { Dice, "MAX_DICE", "CreatePostValidator.MaxDiceCount",
            Member<int>(Game, "DM.Domain.Game.Features.Posts.CreatePostValidator", "MaxDiceCount") },
        { Dice, "MIN_EDGES", "CreatePostValidator.MinEdges",
            Member<int>(Game, "DM.Domain.Game.Features.Posts.CreatePostValidator", "MinEdges") },
        { Dice, "MAX_EDGES", "CreatePostValidator.MaxEdges",
            Member<int>(Game, "DM.Domain.Game.Features.Posts.CreatePostValidator", "MaxEdges") },
        { "entities/game/model/diceLine.ts", "UNLIMITED_EXPLOSIONS", "CreatePostValidator.MaxExplosionCount",
            Member<int>(Game, "DM.Domain.Game.Features.Posts.CreatePostValidator", "MaxExplosionCount") },

        { User, "USERNAME_MIN_LENGTH", "ActivationRequest.Username", Minimum(typeof(ActivationRequest), nameof(ActivationRequest.Username)) },
        { User, "USERNAME_MAX_LENGTH", "ActivationRequest.Username", Maximum(typeof(ActivationRequest), nameof(ActivationRequest.Username)) },
        { User, "USERNAME_MIN_LENGTH", "UsernameChangeCompletionRequest.Username",
            Minimum(typeof(UsernameChangeCompletionRequest), nameof(UsernameChangeCompletionRequest.Username)) },
        { User, "USERNAME_MAX_LENGTH", "UsernameChangeCompletionRequest.Username",
            Maximum(typeof(UsernameChangeCompletionRequest), nameof(UsernameChangeCompletionRequest.Username)) },
        { User, "PASSWORD_MIN_LENGTH", "RegistrationRequest.Password", Minimum(typeof(RegistrationRequest), nameof(RegistrationRequest.Password)) },
        { User, "PASSWORD_MIN_LENGTH", "PasswordChangeRequest.NewPassword", Minimum(typeof(PasswordChangeRequest), nameof(PasswordChangeRequest.NewPassword)) },
        { User, "PASSWORD_MIN_LENGTH", "PasswordPolicyConfiguration.MinimumLength", new PasswordPolicyConfiguration().MinimumLength },
        { User, "PASSWORD_MIN_LENGTH", "appsettings.json PasswordPolicyConfiguration", Setting(nameof(PasswordPolicyConfiguration), "MinimumLength") },
        { User, "USERNAME_CHANGE_REASON_MAX_LENGTH", "CreateUsernameChangeRequestValidator",
            ValidatorMaximum(Account, "DM.Domain.Account.Features.UsernameChange.CreateUsernameChangeRequestValidator", "Reason") },
        { Email, "EMAIL_MAX_LENGTH", "RegistrationRequest.Email", Maximum(typeof(RegistrationRequest), nameof(RegistrationRequest.Email)) },
        { Email, "EMAIL_MAX_LENGTH", "EmailChangeRequest.Email", Maximum(typeof(EmailChangeRequest), nameof(EmailChangeRequest.Email)) },
        { Email, "EMAIL_MAX_LENGTH", "LoginRequest.Email", Maximum(typeof(LoginRequest), nameof(LoginRequest.Email)) },

        { CommunityConstants, "TESTIMONIAL_TEXT_MAX_LENGTH", "CreateWebsiteTestimonialRequest.Text",
            Maximum(typeof(CreateWebsiteTestimonialRequest), nameof(CreateWebsiteTestimonialRequest.Text)) },
        { CommunityConstants, "TESTIMONIAL_TEXT_MAX_LENGTH", "UpdateWebsiteTestimonialRequest.Text",
            Maximum(typeof(UpdateWebsiteTestimonialRequest), nameof(UpdateWebsiteTestimonialRequest.Text)) },
        { CommunityConstants, "FUNDRAISING_TITLE_MAX_LENGTH", "UpdateFundraisingGoalValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Fundraising.UpdateFundraisingGoalValidator", "Title") },
        { Endorsement, "MAX_LENGTH", "UserEndorsementFieldLimits.TextMaxLength",
            Member<int>(Community, "DM.Domain.Community.Features.UserEndorsements.UserEndorsementFieldLimits", "TextMaxLength") },
        { "pages/profile/ProfileViolations.vue", "WARNING_LIMIT", "Violator.WarningPointsScale", Violator.WarningPointsScale },
        { User, "NEWBIE_POST_THRESHOLD", "ProbationPolicy.NewbiePostThreshold", ProbationPolicy.NewbiePostThreshold },
        { "shared/lib/bbcode/bbcodeTagSet.ts", "MAX_PIXELS", "ParameterLimits.MaxPixels", BBCodeParser.Tags.ParameterLimits.MaxPixels },

        { Code, "DIGIT_LENGTH", "TotpCalculator.Digits",
            Member<int>(Account, "DM.Domain.Account.Features.TwoFactor.TotpCalculator", "Digits") },
        { Code, "RECOVERY_LENGTH", "RecoveryCodeFactory.CodeLength",
            Member<int>(Account, "DM.Domain.Account.Features.TwoFactor.RecoveryCodeFactory", "CodeLength") },
        { Code, "RECOVERY_GROUP", "RecoveryCodeFactory.GroupSize",
            Member<int>(Account, "DM.Domain.Account.Features.TwoFactor.RecoveryCodeFactory", "GroupSize") },

        { Ticket, "TICKET_CONTACT_MAX_LENGTH", "TicketTextLimits.ContactMaxLength", TicketTextLimits.ContactMaxLength },
        { Ticket, "TICKET_VIOLATION_URL_MAX_LENGTH", "TicketTextLimits.ViolationUrlMaxLength", TicketTextLimits.ViolationUrlMaxLength },
        { Attachments, "MAX_POST_ATTACHMENTS", "UploadPolicy.MaxPostAttachments", UploadPolicy.MaxPostAttachments },
        { Attachments, "MAX_POST_ATTACHMENT_BYTES", "UploadPolicy.MaxPostAttachmentSizeBytes", UploadPolicy.MaxPostAttachmentSizeBytes },

        { Notepad, "TITLE_MAX_LENGTH", "CreateNotepadEntryRequest.Title",
            Maximum(typeof(CreateNotepadEntryRequest), nameof(CreateNotepadEntryRequest.Title)) },
        { Notepad, "TITLE_MAX_LENGTH", "UpdateNotepadEntryRequest.Title",
            Maximum(typeof(UpdateNotepadEntryRequest), nameof(UpdateNotepadEntryRequest.Title)) },
        { Notepad, "TITLE_MAX_LENGTH", "NotepadEntries.Title",
            Maximum(typeof(DM.Infrastructure.Persistence.Entities.Personal.Notepads.NotepadEntry), "Title") },

        { Catalog, "CATALOG_CODE_MAX_LENGTH", "CatalogFieldLimits.CodeMaxLength", CatalogFieldLimits.CodeMaxLength },
        { Catalog, "CATALOG_TITLE_MAX_LENGTH", "CatalogFieldLimits.TitleMaxLength", CatalogFieldLimits.TitleMaxLength },
        { Catalog, "CATALOG_DESCRIPTION_MAX_LENGTH", "CatalogFieldLimits.DescriptionMaxLength", CatalogFieldLimits.DescriptionMaxLength },
        { Catalog, "CATALOG_ICON_NAME_MAX_LENGTH", "CatalogFieldLimits.IconNameMaxLength", CatalogFieldLimits.IconNameMaxLength },
        { Catalog, "CATALOG_CODE_MAX_LENGTH", "CreateAchievementTypeValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Achievements.CreateAchievementTypeValidator", "Code") },
        { Catalog, "CATALOG_TITLE_MAX_LENGTH", "CreateAchievementTypeValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Achievements.CreateAchievementTypeValidator", "Title") },
        { Catalog, "CATALOG_TITLE_MAX_LENGTH", "UpdateAchievementCategoryValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Achievements.UpdateAchievementCategoryValidator", "Title") },
        { Catalog, "CATALOG_DESCRIPTION_MAX_LENGTH", "UpdateAchievementCategoryValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Achievements.UpdateAchievementCategoryValidator", "Description") },
        { Catalog, "CATALOG_CODE_MAX_LENGTH", "CreateAwardTypeValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Awards.CreateAwardTypeValidator", "Code") },
        { Catalog, "CATALOG_TITLE_MAX_LENGTH", "UpdateAwardTypeValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Awards.UpdateAwardTypeValidator", "Title") },
        { Catalog, "CATALOG_DESCRIPTION_MAX_LENGTH", "UpdateAwardTypeValidator",
            ValidatorMaximum(Community, "DM.Domain.Community.Features.Awards.UpdateAwardTypeValidator", "Description") },
        { Catalog, "CATALOG_TITLE_MAX_LENGTH", "AchievementTypes.Title",
            Maximum(typeof(DM.Infrastructure.Persistence.Entities.Community.AchievementType), "Title") },
        { Catalog, "CATALOG_ICON_NAME_MAX_LENGTH", "AwardTypes.IconName",
            Maximum(typeof(DM.Infrastructure.Persistence.Entities.Community.AwardType), "IconName") },
        { Catalog, "MIN_TIER", "TierScale.MinTier", TierScale.MinTier },
        { Catalog, "MAX_TIER", "TierScale.MaxTier", TierScale.MaxTier },
        { Catalog, "CONTEST_SERIES_MIN_YEAR", "ContestSeries.MinYear",
            DM.Domain.Community.Features.Awards.ContestSeries.MinYear },
        { Catalog, "CONTEST_SERIES_YEARS_AHEAD", "ContestSeries.YearsAhead",
            DM.Domain.Community.Features.Awards.ContestSeries.YearsAhead },
        { ModerationReason, "REASON_MAX_LENGTH", "WarningPolicy.ReasonMaxLength",
            DM.Domain.Moderation.Features.Warnings.WarningPolicy.ReasonMaxLength },

        { "shared/lib/bbcode/bbcodeTagSet.ts", "MAX_DEPTH", "BbParser.TreeMaxDepth",
            Member<int>(typeof(BBCodeParser.BbParser).Assembly, "BBCodeParser.BbParser", "TreeMaxDepth") },
    };

    [Theory]
    [MemberData(nameof(Copies))]
    public void HoldTheClientCopyToTheServer(string file, string constant, string server, long bound) =>
        Number(file, constant).Should().Be(bound,
            $"{constant} in {file} copies {server}: past it the form lets through what the save " +
            "refuses, short of it the form refuses what would have saved");

    public static TheoryData<string, string, int> Statements => new()
    {
        { UsernameInput, @"Минимум (?<n>\d+) символа", Minimum(typeof(ActivationRequest), nameof(ActivationRequest.Username)) },
        { UsernameInput, @"Максимум (?<n>\d+) символов", Maximum(typeof(ActivationRequest), nameof(ActivationRequest.Username)) },
        { UsernameInput, @"(?<n>\d+)-\d+ символов", Minimum(typeof(ActivationRequest), nameof(ActivationRequest.Username)) },
        { UsernameInput, @"\d+-(?<n>\d+) символов", Maximum(typeof(ActivationRequest), nameof(ActivationRequest.Username)) },
        { Catalog, @"(?<n>\d+) золото", TierScale.Gold },
        { Catalog, @"(?<n>\d+) серебро", TierScale.Silver },
        { Catalog, @"(?<n>\d+) бронза", TierScale.Bronze },
        { Catalog, @"(?<n>\d+) платина", TierScale.Platinum },
    };

    [Theory]
    [MemberData(nameof(Statements))]
    public void StateTheBoundTheServerHolds(string file, string pattern, int bound) =>
        Stated(file, pattern).Should().Be(bound,
            $"{file} states a bound in words, and the words are the server's number");

    [Fact]
    public void ReadARecoveryCodeInTheServerAlphabet() =>
        Regex.Match(Source(Code), @"const CROCKFORD = ""(?<alphabet>[^""]+)""").Groups["alphabet"].Value
            .Should().Be(Member<string>(
                    typeof(DM.Domain.Account.Authorization.AccountIntention).Assembly,
                    "DM.Domain.Account.Features.TwoFactor.RecoveryCodeFactory", "Alphabet"),
                "a symbol the field drops is one the code was made of");

    [Fact]
    public void AcceptThePictureFormatsTheServerDecodes()
    {
        Formats(Attachments, @"const ALLOWED_MIME = \[(?<list>[^\]]+)\]")
            .Should().BeEquivalentTo(ImageProcessingDefaults.PostAttachmentContentTypes,
                "a format the picker offers and the server refuses fails after the upload, and one the " +
                "server takes and the picker refuses cannot be attached at all");

        Formats("entities/user/model/useAvatarUpload.ts", @"AVATAR_ACCEPT = ""(?<list>[^""]+)""")
            .Should().BeEquivalentTo(ImageProcessingDefaults.AvatarContentTypes,
                "the avatar picker refuses by the server's list");
    }

    private static string[] Formats(string file, string pattern)
    {
        var list = Regex.Match(Source(file), pattern).Groups["list"].Value;
        return list
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(format => format.Trim('"'))
            .ToArray();
    }
}
