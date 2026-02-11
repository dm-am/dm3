#pragma warning disable CS8603 // MaybeField chaining produces false positive null warnings
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.AssistantAssignment;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Games.Shared;
using DM.Services.Game.Dto.Input;
using DM.Services.Game.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

/// <inheritdoc />
internal class GameUpdatingService : IGameUpdatingService
{
    private readonly IValidator<UpdateGame> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReadingService _gameReadingService;
    private readonly IAssignmentService _assignmentService;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGameUpdatingRepository _updatingRepository;
    private readonly IGameIntentionConverter _intentionConverter;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

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
        _validator = validator;
        _intentionManager = intentionManager;
        _gameReadingService = gameReadingService;
        _assignmentService = assignmentService;
        _updateBuilderFactory = updateBuilderFactory;
        _userRepository = userRepository;
        _dateTimeProvider = dateTimeProvider;
        _updatingRepository = updatingRepository;
        _intentionConverter = intentionConverter;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<GameExtended> Update(UpdateGame updateGame)
    {
        await _validator.ValidateAndThrowAsync(updateGame);
        var game = await _gameReadingService.GetGameDetails(updateGame.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var changes = _updateBuilderFactory.Create<DbGame>(game.Id)
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
            .MaybeField(c => c.HidePostStats, updateGame.HidePostStats)
            .MaybeField(c => c.CommentariesAccessMode, updateGame.CommentariesAccessMode)
            .MaybeField(c => c.DisableAlignment, updateGame.DisableAlignment)
            .MaybeField(c => c.Notepad, updateGame.Notepad);

        var oldAssistant = game.Assistant ?? game.PendingAssistant;
        if (updateGame.AssistantLogin != default &&
            !updateGame.AssistantLogin.Equals(oldAssistant?.Login, StringComparison.InvariantCultureIgnoreCase))
        {
            var (assistantExists, foundAssistantId) = await _userRepository.FindUserId(updateGame.AssistantLogin);
            if (assistantExists)
            {
                changes.Field(g => g.AssistantId, null);
                await _assignmentService.CreateAssignment(game.Id, foundAssistantId);
            }
        }

        var invokedEvents = new List<EventType> {EventType.ChangedGame};
        if (updateGame.Status.HasValue && updateGame.Status != game.Status)
        {
            var (intention, eventType) = _intentionConverter.Convert(updateGame.Status.Value);
            if (_intentionManager.IsAllowed(intention, game))
            {
                changes.Field(g => g.Status, updateGame.Status.Value);
                invokedEvents.Add(eventType);

                // Set ReleaseDate on first activation
                if (!game.ReleaseDate.HasValue && updateGame.Status == ModuleStatus.Active)
                {
                    changes = changes.Field(g => g.ReleaseDate, _dateTimeProvider.Now);
                }

                // Set ClosedUtc when closing
                if (updateGame.Status == ModuleStatus.Closed)
                {
                    changes = changes.Field(g => g.ClosedUtc, _dateTimeProvider.Now);
                    changes = changes.Field(g => g.IsRecruitmentOpen, false);
                }

                // Clear ClosedUtc when reopening
                if (game.Status == ModuleStatus.Closed && updateGame.Status != ModuleStatus.Closed)
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
            if (_intentionManager.IsAllowed(GameIntention.SetStatusModeration, game))
            {
                changes.Field(g => g.PremoderationStatus, updateGame.PremoderationStatus.Value);

                // Mentor takes game for review
                if (updateGame.PremoderationStatus == PremoderationStatus.Approved ||
                    updateGame.PremoderationStatus == PremoderationStatus.AwaitingEdits)
                {
                    changes = changes.Field(g => g.MentorId, _identityProvider.Current.User.UserId);
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
                changes = changes.Field(g => g.RecruitmentStartedUtc, _dateTimeProvider.Now);
            }
        }

        if (updateGame.RecruitmentPlayerLimit.HasValue)
        {
            changes = changes.Field(g => g.RecruitmentPlayerLimit, updateGame.RecruitmentPlayerLimit);
        }

        var result = await _updatingRepository.Update(changes);
        await _producer.Send(invokedEvents, game.Id);
        return result;
    }
}