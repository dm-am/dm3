using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using MessageDal = DM.Services.DataAccess.BusinessObjects.Messaging.Message;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <inheritdoc />
internal class MessageUpdatingService : IMessageUpdatingService
{
    private readonly IValidator<UpdateMessage> _validator;
    private readonly IMessageReadingService _messageReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IMessageUpdatingRepository _repository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public MessageUpdatingService(
        IValidator<UpdateMessage> validator,
        IMessageReadingService messageReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IMessageUpdatingRepository repository,
        IInvokedEventProducer producer)
    {
        _validator = validator;
        _messageReadingService = messageReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<Message> Update(UpdateMessage updateMessage)
    {
        await _validator.ValidateAndThrowAsync(updateMessage);
        var message = await _messageReadingService.Get(updateMessage.MessageId);

        _intentionManager.ThrowIfForbidden(MessageIntention.Edit, message);
        var updateBuilder = _updateBuilderFactory.Create<MessageDal>(updateMessage.MessageId)
            .MaybeField(f => f.Text, updateMessage.Text?.Trim());

        var updatedMessage = await _repository.Update(updateBuilder);
        await _producer.Send(EventType.ChangedMessage, updatedMessage.Id);
        return updatedMessage;
    }
}
