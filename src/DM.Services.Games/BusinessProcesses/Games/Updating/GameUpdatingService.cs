using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Games.AssistantAssignment;
using DM.Services.Gaming.BusinessProcesses.Games.Reading;
using DM.Services.Gaming.BusinessProcesses.Games.Shared;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using Game = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Gaming.BusinessProcesses.Games.Updating;

/// <inheritdoc />
internal class GameUpdatingService : IGameUpdatingService
{
    private readonly IValidator<UpdateGame> validator;
    private readonly IIntentionManager intentionManager;
    private readonly IGameReadingService gameReadingService;
    private readonly IAssignmentService assignmentService;
    private readonly IUpdateBuilderFactory updateBuilderFactory;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IGameUpdatingRepository updatingRepository;
    private readonly IGameIntentionConverter intentionConverter;
    private readonly IInvokedEventProducer producer;
    private readonly IIdentityProvider identityProvider;

    /// <inheritdoc />
    public GameUpdatingService(
        IValidator<UpdateGame> validator,
        IIntentionManager intentionManager,
        IGameReadingService gameReadingService,
        IAssignmentService assignmentService,
        IUpdateBuilderFactory updateBuilderFactory,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider,
        IGameUpdatingRepository updatingRepository,
        IGameIntentionConverter intentionConverter,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        this.validator = validator;
        this.intentionManager = intentionManager;
        this.gameReadingService = gameReadingService;
        this.assignmentService = assignmentService;
        this.updateBuilderFactory = updateBuilderFactory;
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
        this.updatingRepository = updatingRepository;
        this.intentionConverter = intentionConverter;
        this.producer = producer;
        this.identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<GameExtended> Update(UpdateGame updateGame)
    {
        await validator.ValidateAndThrowAsync(updateGame);
        var game = await gameReadingService.GetGameDetails(updateGame.GameId);
        intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var changes = updateBuilderFactory.Create<Game>(game.Id)
            .MaybeField(c => c.Title, updateGame.Title?.Trim())
            .MaybeField(c => c.NarrativeSetting, updateGame.NarrativeSetting?.Trim())
            .MaybeField(c => c.SystemName, updateGame.SystemName?.Trim())
            .MaybeField(c => c.Info, updateGame.Info)
            .MaybeField(c => c.HideTemper, updateGame.HideTemper)
            .MaybeField(c => c.HideStory, updateGame.HideStory)
            .MaybeField(c => c.HideSkills, updateGame.HideSkills)
            .MaybeField(c => c.HideInventory, updateGame.HideInventory)
            .MaybeField(c => c.HideDiceResult, updateGame.HideDiceResult)
            .MaybeField(c => c.ShowPrivateMessages, updateGame.ShowPrivateMessages)
            .MaybeField(c => c.CommentariesAccessMode, updateGame.CommentariesAccessMode)
            .MaybeField(c => c.DisableAlignment, updateGame.DisableAlignment)
            .MaybeField(c => c.Notepad, updateGame.Notepad);

        var oldAssistant = game.Assistant ?? game.PendingAssistant;
        if (updateGame.AssistantLogin != default &&
            !updateGame.AssistantLogin.Equals(oldAssistant?.Login, StringComparison.InvariantCultureIgnoreCase))
        {
            var (assistantExists, foundAssistantId) = await userRepository.FindUserId(updateGame.AssistantLogin);
            if (assistantExists)
            {
                changes.Field(g => g.AssistantId, null);
                await assignmentService.CreateAssignment(game.Id, foundAssistantId);
            }
        }

        var invokedEvents = new List<EventType> {EventType.ChangedGame};
        if (updateGame.Status.HasValue && updateGame.Status != game.Status)
        {
            var (intention, eventType) = intentionConverter.Convert(updateGame.Status.Value);
            if (intentionManager.IsAllowed(intention, game))
            {
                changes.Field(g => g.Status, updateGame.Status.Value);
                invokedEvents.Add(eventType);

                // Set ReleaseDate on first activation
                if (!game.ReleaseDate.HasValue && updateGame.Status == GameStatus.Active)
                {
                    changes = changes.Field(g => g.ReleaseDate, dateTimeProvider.Now);
                }

                // Set ClosedUtc when closing
                if (updateGame.Status == GameStatus.Closed)
                {
                    changes = changes.Field(g => g.ClosedUtc, dateTimeProvider.Now);
                    changes = changes.Field(g => g.IsRecruitmentOpen, false);
                }

                // Clear ClosedUtc when reopening
                if (game.Status == GameStatus.Closed && updateGame.Status != GameStatus.Closed)
                {
                    changes = changes.Field(g => g.ClosedUtc, (DateTimeOffset?)null);
                    changes = changes.Field(g => g.IsFinished, false);
                    changes = changes.Field(g => g.IsFrozen, false);
                }
            }
        }

        // Handle premoderation status changes
        if (updateGame.PremoderationStatus.HasValue && updateGame.PremoderationStatus != game.PremoderationStatus)
        {
            if (intentionManager.IsAllowed(GameIntention.SetStatusModeration, game))
            {
                changes.Field(g => g.PremoderationStatus, updateGame.PremoderationStatus.Value);

                // Mentor takes game for review
                if (updateGame.PremoderationStatus == PremoderationStatus.Approved ||
                    updateGame.PremoderationStatus == PremoderationStatus.AwaitingEdits)
                {
                    changes = changes.Field(g => g.MentorId, identityProvider.Current.User.UserId);
                }

                // When approved, clear mentor
                if (updateGame.PremoderationStatus == PremoderationStatus.Approved &&
                    game.PremoderationStatus != PremoderationStatus.Approved)
                {
                    changes = changes.Field(g => g.MentorId, (Guid?)null);
                }
            }
        }

        // Handle close reason flags
        if (updateGame.IsFinished.HasValue)
        {
            changes = changes.MaybeField(g => g.IsFinished, updateGame.IsFinished);
        }
        if (updateGame.IsFrozen.HasValue)
        {
            changes = changes.MaybeField(g => g.IsFrozen, updateGame.IsFrozen);
        }
        if (updateGame.IsRecruitmentOpen.HasValue)
        {
            changes = changes.MaybeField(g => g.IsRecruitmentOpen, updateGame.IsRecruitmentOpen);

            // Set RecruitmentStartedUtc when opening recruitment for the first time
            if (updateGame.IsRecruitmentOpen.Value && !game.Recruitment?.IsOpen == true)
            {
                changes = changes.Field(g => g.RecruitmentStartedUtc, dateTimeProvider.Now);
            }
        }

        if (updateGame.RecruitmentPlayerLimit.HasValue)
        {
            changes = changes.Field(g => g.RecruitmentPlayerLimit, updateGame.RecruitmentPlayerLimit);
        }

        var result = await updatingRepository.Update(changes);
        await producer.Send(invokedEvents, game.Id);
        return result;
    }
}