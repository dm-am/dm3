using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.InitialData;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// A game of the catalog being written: its row, its master, its template
    /// and size, and its place in the catalog.
    /// </summary>
    private sealed record SeedingGame(DbGame Game, DbUser Master, GameTemplate Template, GameVariation Variation, int Index)
    {
        /// <summary>The game that carries the game-zone UI test fixtures.</summary>
        public bool IsUiTestDataGame => Template.IsUiTestData;
    }

    /// <summary>
    /// The Diopside character and its large post, which the game pass writes once,
    /// into the first finished game that can carry them.
    /// </summary>
    private sealed class GameShowcase
    {
        /// <summary>Track if we've created the Diopside character.</summary>
        public bool HasDiopsideCharacter { get; set; }

        /// <summary>Track if we've used the large Diopside post.</summary>
        public bool HasLargePost { get; set; }
    }

    private async Task<List<Guid>> CreateGames(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var gameIds = new List<Guid>();

        var existingGames = await _dbContext.Set<DbGame>().ToListAsync();

        var existingTitles = existingGames.Select(g => g.Title).ToHashSet();

        var mentor = users.First(u => u.Role == UserRole.Mentor);
        var experiencedUsers = users.Where(u => !ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        var newbieUsers = users.Where(u => ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();

        if (experiencedUsers.Count == 0)
        {
            experiencedUsers = users.Take(3).ToList();
        }

        var showcase = new GameShowcase();

        for (var gi = 0; gi < GameCatalog.Templates.Length; gi++)
        {
            var template = GameCatalog.Templates[gi];

            if (existingTitles.Contains(template.Title))
            {
                var existingGame = existingGames.First(g => g.Title == template.Title);
                gameIds.Add(existingGame.GameId);
                continue;
            }

            var variation = template.Variation;
            var isNewbieMaster = template.Premod == PremoderationStatus.AwaitingApproval;
            var master = isNewbieMaster && newbieUsers.Count > 0
                ? newbieUsers[_random.Next(newbieUsers.Count)]
                : experiencedUsers[gi % experiencedUsers.Count];

            var game = await NewGame(template, variation, gi, master, isNewbieMaster ? mentor.UserId : null);

            _dbContext.Set<DbGame>().Add(game);
            gameIds.Add(game.GameId);
            result.GamesCreated++;

            foreach (var tagId in template.Tags)
            {
                _dbContext.Set<GameTag>().Add(new GameTag
                {
                    GameTagId = _guidFactory.Create(),
                    GameId = game.GameId,
                    TagId = tagId
                });
            }

            if (template.Status == ModuleStatus.Draft) continue;

            await SeedGameContent(new SeedingGame(game, master, template, variation, gi), users, showcase, result);
        }

        var skippedGames = existingTitles.Count;
        if (result.GamesCreated > 0)
        {
            result.Details.Add($"Created {result.GamesCreated} new games (skipped {skippedGames} existing)");
        }
        else if (skippedGames > 0)
        {
            result.Details.Add($"All {skippedGames} games already exist");
        }

        if (result.CharactersCreated > 0 || result.PostsCreated > 0)
        {
            result.Details.Add($"Created {result.CharactersCreated} characters and {result.PostsCreated} posts total");
        }

        return gameIds;
    }

    /// <summary>The draft a role of the stand masters: TestMentor's, so its page offers "Начать игру".</summary>
    internal const string MentorDraftTitle = "Проект: Ледяной Север";

    /// <summary>
    /// A private draft of the mentor, outside the catalog: every draft of the catalog goes to an
    /// account no role of the stand signs in as.
    /// </summary>
    /// <remarks>
    /// After the catalog and every other game, so the public ids of those stay where they are. Experienced,
    /// so nothing waits for premoderation.
    /// </remarks>
    private async Task SeedMentorDraft(List<DbUser> users, ComprehensiveSeedResult result)
    {
        if (await _dbContext.Games.AnyAsync(g => g.Title == MentorDraftTitle))
        {
            result.Details.Add($"Game \"{MentorDraftTitle}\" already exists, skipping");
            return;
        }

        var mentor = users.FirstOrDefault(u => u.Role == UserRole.Mentor);
        if (mentor == null)
        {
            result.Details.Add($"Game \"{MentorDraftTitle}\" skipped: no mentor");
            return;
        }

        var serialNumber = await SerialNumberAllocator.NextAsync<DbGame>(_dbContext);
        var draft = new DbGame
        {
            GameId = _guidFactory.Create(),
            SerialNumber = serialNumber,
            PublicId = _publicIdService.Encode(serialNumber),
            CreatedUtc = _now.AddDays(-3),
            Status = ModuleStatus.Draft,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Private,
            MasterId = mentor.UserId,
            Title = MentorDraftTitle,
            SystemName = "Fate Core",
            NarrativeSetting = "Северные земли",
            Info = "Заготовка кампании о зимовке на краю карты. Состав партии и первую сцену еще предстоит придумать.",
            CommentsAccessMode = CommentsAccessMode.Public,
            RecruitmentPcLimit = 4,
        };
        _dbContext.Games.Add(draft);
        _dbContext.GameAttributeSchemas.Add(new DbGameAttributeSchema
        {
            GameId = draft.GameId,
            Specifications = DefaultAttributeSchema.Specifications(),
            Version = 0,
            UpdatedUtc = draft.CreatedUtc,
        });
        await _dbContext.SaveChangesAsync();

        result.GamesCreated++;
        result.Details.Add($"Draft \"{MentorDraftTitle}\" ({draft.PublicId}) of {mentor.Username}");
    }

    /// <summary>
    /// The row of a game: its dates by its status, its recruitment and its
    /// readable address.
    /// </summary>
    private async Task<DbGame> NewGame(
        GameTemplate template, GameVariation variation, int gi, DbUser master, Guid? mentorId)
    {
        var (gameCreatedUtc, gameActivatedUtc, gameClosedUtc) = GameDates(template, gi);

        DateTimeOffset? recruitmentStartedUtc = null;
        if (template.IsRecruitmentOpen && gameActivatedUtc.HasValue)
        {
            var daysSinceActivation = (int)(_now - gameActivatedUtc.Value).TotalDays;
            var recruitmentStartDaysAgo = _random.Next(1, Math.Max(2, Math.Min(30, daysSinceActivation)));
            recruitmentStartedUtc = _now.AddDays(-recruitmentStartDaysAgo);
        }

        // The readable address comes from the sequence before the insert, as in the repository (SerialNumberAllocator).
        var gameSerialNumber = await SerialNumberAllocator.NextAsync<DbGame>(_dbContext);

        return new DbGame
        {
            GameId = _guidFactory.Create(),
            CreatedUtc = gameCreatedUtc,
            ActivatedUtc = gameActivatedUtc,
            Status = template.Status,
            PremoderationStatus = template.Premod,
            ClosedReason = template.ClosedReason,
            DraftVisibility = template.DraftVisibility,
            IsRecruitmentOpen = template.IsRecruitmentOpen,
            // Mix of initial (1) and subsequent (2-3) recruitment
            RecruitmentCount = !template.IsRecruitmentOpen ? 0 : (gi % 3, gi % 5) switch
            {
                (1, _) => 2,
                (_, 0) => 3,
                _ => 1,
            },
            RecruitmentPcLimit = variation.PcLimit,
            RecruitmentStartedUtc = recruitmentStartedUtc,
            ClosedUtc = gameClosedUtc,
            MasterId = master.UserId,
            MentorId = mentorId,
            Title = template.Title,
            SystemName = template.System,
            NarrativeSetting = template.Setting,
            Info = $"Игра \"{template.Title}\". Система: {template.System}, сеттинг: {template.Setting}. Правила, мир и анкету персонажа мастер выкладывает в первой комнате, вопросы задавайте в обсуждении.",
            CommentsAccessMode = CommentsAccessMode.Public,
            SerialNumber = gameSerialNumber,
            PublicId = _publicIdService.Encode(gameSerialNumber)
        };
    }

    /// <summary>The dates of a game, by its status and its place in the catalog.</summary>
    private (DateTimeOffset Created, DateTimeOffset? Activated, DateTimeOffset? Closed) GameDates(
        GameTemplate template, int gi)
    {
        DateTimeOffset gameCreatedUtc;
        DateTimeOffset? gameActivatedUtc;
        DateTimeOffset? gameClosedUtc;

        if (template.Status == ModuleStatus.Draft)
        {
            // Drafts are recent - people working on them
            gameCreatedUtc = _now.AddDays(-_random.Next(1, 30));
            gameActivatedUtc = null;
            gameClosedUtc = null;
        }
        else if (template.Status == ModuleStatus.Active)
        {
            if (gi < 4)
            {
                // First 4 active games are "new" (activated within 7 days)
                var daysAgoCreated = _random.Next(7, 30);
                gameCreatedUtc = _now.AddDays(-daysAgoCreated);
                gameActivatedUtc = _now.AddDays(-(gi + 1)); // 1-4 days ago
            }
            else if (template.IsRecruitmentOpen)
            {
                // Recruiting games: recently started, looking for players
                var daysAgoCreated = _random.Next(30, 90);
                gameCreatedUtc = _now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(3, Math.Min(15, daysAgoCreated - 1));
                gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
            }
            else
            {
                // Established games: running for a while
                var daysAgoCreated = _random.Next(60, 180);
                gameCreatedUtc = _now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(5, Math.Min(30, daysAgoCreated - 1));
                gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
            }
            gameClosedUtc = null;
        }
        else // Closed
        {
            if (template.ClosedReason == ClosedReason.Finished)
            {
                // Finished games: old, ran their full course
                var daysAgoCreated = _random.Next(180, 365);
                gameCreatedUtc = _now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(5, 20);
                gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                // Closed recently (within last 90 days)
                var daysAgoSinceActivated = (int)(_now - gameActivatedUtc.Value).TotalDays;
                var minDaysRunning = Math.Min(60, daysAgoSinceActivated - 1);
                var closedDaysAgo = _random.Next(1, Math.Max(2, Math.Min(90, daysAgoSinceActivated - minDaysRunning)));
                gameClosedUtc = _now.AddDays(-closedDaysAgo);
            }
            else // Frozen
            {
                // Frozen games: abandoned mid-way
                var daysAgoCreated = _random.Next(90, 300);
                gameCreatedUtc = _now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(3, 15);
                gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                // Frozen some time ago (30-180 days)
                var daysAgoSinceActivated = (int)(_now - gameActivatedUtc.Value).TotalDays;
                var closedDaysAgo = _random.Next(30, Math.Max(31, Math.Min(180, daysAgoSinceActivated - 10)));
                gameClosedUtc = _now.AddDays(-closedDaysAgo);
            }
        }

        return (gameCreatedUtc, gameActivatedUtc, gameClosedUtc);
    }

    /// <summary>
    /// Everything written into a game that is not a draft, saved before the next
    /// game starts.
    /// </summary>
    private async Task SeedGameContent(
        SeedingGame seeding, List<DbUser> users, GameShowcase showcase,
        ComprehensiveSeedResult result)
    {
        SeedGameAssistants(seeding, users);
        var rooms = SeedGameRooms(seeding);
        var cast = await SeedGameCharacters(seeding, rooms, users, showcase, result);
        await SeedGamePosts(seeding, rooms.Main, cast, users, showcase, result);

        var chatsAwaitingLastMessage = new List<(Chat Chat, Guid LastMessageId)>();
        if (seeding.IsUiTestDataGame)
        {
            await SeedGameZoneFixtures(seeding, rooms, cast, users, chatsAwaitingLastMessage, result);
        }

        SeedGameReaders(seeding, cast.Players, users);
        SeedGameComments(seeding, users);

        // Saved per game, so the change tracker does not hold thousands of entities
        await SaveLinkingLastMessages(chatsAwaitingLastMessage);
    }

    /// <summary>
    /// The assistants of a game: every fifth game gets one, every twentieth two.
    /// </summary>
    private void SeedGameAssistants(SeedingGame seeding, List<DbUser> users)
    {
        var (game, master, _, _, gi) = seeding;

        // Half the newbie threshold, through the rule: an assistant has a track record.
        const int assistantPostFloor = ProbationPolicy.NewbiePostThreshold / 2;
        var availableForAssistant = users
            .Where(u => u.UserId != master.UserId && u.QuantityRating >= assistantPostFloor)
            .ToList();
        var assistantCount = gi switch
        {
            _ when gi % 20 == 0 && availableForAssistant.Count >= 2 => 2,
            _ when gi % 5 == 0 && availableForAssistant.Count >= 1 => 1,
            _ => 0,
        };
        if (assistantCount > 0)
        {
            var selectedAssistants = availableForAssistant.OrderBy(_ => _random.Next()).Take(assistantCount).ToList();
            foreach (var assistant in selectedAssistants)
            {
                _dbContext.Set<GameAssistant>().Add(new GameAssistant
                {
                    GameAssistantId = _guidFactory.Create(),
                    GameId = game.GameId,
                    UserId = assistant.UserId,
                    JoinedUtc = game.CreatedUtc.AddDays(1 + selectedAssistants.IndexOf(assistant))
                });
            }
        }
    }

    /// <summary>The readers (subscriptions) of a game, as many as its variation asks for.</summary>
    private void SeedGameReaders(SeedingGame seeding, List<DbUser> playersForGame, List<DbUser> users)
    {
        var (game, master, _, variation, _) = seeding;

        var potentialReaders = users.Where(u => u.UserId != master.UserId && !playersForGame.Contains(u)).ToList();
        var readersToAdd = Math.Min(variation.Readers, potentialReaders.Count);
        var readers = potentialReaders.OrderBy(_ => _random.Next()).Take(readersToAdd).ToList();
        AddReaders(readers, SubscriptionTargetType.Game, game.GameId, game.CreatedUtc);
    }

    /// <summary>Subscriptions of the readers, each within two weeks of the start.</summary>
    private void AddReaders(
        IEnumerable<DbUser> readers, SubscriptionTargetType targetType, Guid targetId, DateTimeOffset startUtc)
    {
        foreach (var reader in readers)
        {
            _dbContext.Set<Subscription>().Add(new Subscription
            {
                SubscriptionId = _guidFactory.Create(),
                SubscriberId = reader.UserId,
                TargetType = targetType,
                TargetId = targetId,
                Settings = SubscriptionSettings.None,
                CreatedUtc = startUtc.AddDays(_random.Next(1, 14))
            });
        }
    }

    private static readonly string[] GameCommentTexts =
    [
        "Отличная игра, мастер молодец!",
        "Сюжет очень интересный, жду продолжения.",
        "Спасибо мастеру, сцены идут ровно.",
        "Персонажи прописаны очень хорошо.",
        "Хорошая динамика, не скучно.",
    ];

    /// <summary>
    /// The comments of a game, spread evenly between its activation and its
    /// closing, or now.
    /// </summary>
    private void SeedGameComments(SeedingGame seeding, List<DbUser> users)
    {
        var (game, _, _, variation, _) = seeding;

        var gameCommentsToCreate = variation.Comments;
        var commentBaseTime = game.ActivatedUtc ?? game.CreatedUtc;
        var commentEndTime = game.ClosedUtc ?? _now;
        var commentHoursAvailable = (int)(commentEndTime - commentBaseTime).TotalHours;
        var hoursPerComment = gameCommentsToCreate > 0 ? Math.Max(1, commentHoursAvailable / gameCommentsToCreate) : 1;

        for (var j = 0; j < gameCommentsToCreate; j++)
        {
            var commentOffsetHours = Math.Min(j * hoursPerComment + _random.Next(0, hoursPerComment), Math.Max(1, commentHoursAvailable - 1));
            var commentAuthor = users[_random.Next(users.Count)];
            var gameComment = new DbComment
            {
                CommentId = _guidFactory.Create(),
                EntityId = game.GameId,
                AuthorId = commentAuthor.UserId,
                CreatedUtc = commentBaseTime.AddHours(commentOffsetHours),
                Text = GameCommentTexts[_random.Next(GameCommentTexts.Length)],
            };

            _dbContext.Set<DbComment>().Add(gameComment);
            game.CommentCount++;
            game.LastCommentId = gameComment.CommentId;
        }
    }
}
