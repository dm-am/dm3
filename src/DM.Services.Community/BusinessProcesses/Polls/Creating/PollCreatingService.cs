using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Polls.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Polls.Creating;

/// <inheritdoc />
internal class PollCreatingService : IPollCreatingService
{
    private readonly IValidator<CreatePoll> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IPollFactory _factory;
    private readonly IPollCreatingRepository _repository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public PollCreatingService(
        IValidator<CreatePoll> validator,
        IIntentionManager intentionManager,
        IPollFactory factory,
        IPollCreatingRepository repository,
        IInvokedEventProducer producer)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<Poll> Create(CreatePoll createPoll)
    {
        await _validator.ValidateAndThrowAsync(createPoll);
        _intentionManager.ThrowIfForbidden(PollIntention.Create);

        var poll = _factory.Create(createPoll);
        var result = await _repository.Create(poll);
        await _producer.Send(EventType.NewPoll, result.Id);

        return result;
    }
}