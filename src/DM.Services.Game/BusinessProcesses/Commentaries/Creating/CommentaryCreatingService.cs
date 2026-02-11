using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Commentaries;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Game.BusinessProcesses.Commentaries.Creating;

/// <inheritdoc />
internal class CommentaryCreatingService : ICommentaryCreatingService
{
    private readonly IValidator<CreateComment> _validator;
    private readonly IGameReadingService _gameReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICommentaryFactory _commentaryFactory;
    private readonly ICommentaryCreatingRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentaryCreatingService(
        IValidator<CreateComment> validator,
        IGameReadingService gameReadingService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ICommentaryFactory commentaryFactory,
        ICommentaryCreatingRepository repository,
        IUnreadCountersRepository countersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _gameReadingService = gameReadingService;
        _intentionManager = intentionManager;
        _commentaryFactory = commentaryFactory;
        _repository = repository;
        _countersRepository = countersRepository;
        _invokedEventProducer = invokedEventProducer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateComment createComment)
    {
        await _validator.ValidateAndThrowAsync(createComment);

        var game = await _gameReadingService.GetGame(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(GameIntention.CreateComment, game);

        var comment = _commentaryFactory.Create(createComment, _identityProvider.Current.User.UserId);
        var createdComment = await _repository.Create(comment);
        await _countersRepository.Increment(game.Id, UnreadEntryType.Message);
        await _invokedEventProducer.Send(EventType.NewGameComment, comment.CommentId);

        return createdComment;
    }
}