using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <inheritdoc />
internal class ActivationService : IActivationService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IActivationRepository _repository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public ActivationService(
        IDateTimeProvider dateTimeProvider,
        IUpdateBuilderFactory updateBuilderFactory,
        IActivationRepository repository,
        IInvokedEventProducer producer)
    {
        _dateTimeProvider = dateTimeProvider;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _producer = producer;
    }
        
    /// <inheritdoc />
    public async Task<Guid> Activate(Guid tokenId)
    {
        var userId = await _repository.FindUserToActivate(tokenId, _dateTimeProvider.Now - TimeSpan.FromDays(2));
        if (!userId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone,
                "Activation token is invalid! Address the technical support for further assistance");
        }

        var updateUser = _updateBuilderFactory.Create<User>(userId.Value).Field(u => u.Activated, true);
        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        await _repository.ActivateUser(updateUser, updateToken);

        await _producer.Send(EventType.ActivatedUser, userId.Value);
        return userId.Value;
    }
}