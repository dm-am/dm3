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
    private readonly IValidator<UpdateMessage> validator;
    private readonly IMessageReadingService messageReadingService;
    private readonly IIntentionManager intentionManager;
    private readonly IUpdateBuilderFactory updateBuilderFactory;
    private readonly IMessageUpdatingRepository repository;
    private readonly IInvokedEventProducer producer;

    /// <inheritdoc />
    public MessageUpdatingService(
        IValidator<UpdateMessage> validator,
        IMessageReadingService messageReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IMessageUpdatingRepository repository,
        IInvokedEventProducer producer)
    {
        this.validator = validator;
        this.messageReadingService = messageReadingService;
        this.intentionManager = intentionManager;
        this.updateBuilderFactory = updateBuilderFactory;
        this.repository = repository;
        this.producer = producer;
    }

    /// <inheritdoc />
    public async Task<Message> Update(UpdateMessage updateMessage)
    {
        await validator.ValidateAndThrowAsync(updateMessage);
        var message = await messageReadingService.Get(updateMessage.MessageId);

        intentionManager.ThrowIfForbidden(MessageIntention.Edit, message);
        var updateBuilder = updateBuilderFactory.Create<MessageDal>(updateMessage.MessageId)
            .MaybeField(f => f.Text, updateMessage.Text?.Trim());

        var updatedMessage = await repository.Update(updateBuilder);
        await producer.Send(EventType.ChangedMessage, updatedMessage.Id);
        return updatedMessage;
    }
}
