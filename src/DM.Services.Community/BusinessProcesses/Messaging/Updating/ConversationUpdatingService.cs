using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using DM.Services.DataAccess.RelationalStorage;
using FluentValidation;
using Conversation = DM.Services.Community.BusinessProcesses.Messaging.Reading.Conversation;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <inheritdoc />
internal class ConversationUpdatingService : IConversationUpdatingService
{
    private readonly IValidator<UpdateConversation> _validator;
    private readonly IConversationUpdatingRepository _repository;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IGuidFactory _guidFactory;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ConversationUpdatingService(
        IValidator<UpdateConversation> validator,
        IConversationUpdatingRepository repository,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IUnreadCountersRepository unreadCountersRepository,
        IGuidFactory guidFactory,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _repository = repository;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _unreadCountersRepository = unreadCountersRepository;
        _guidFactory = guidFactory;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Conversation> Update(UpdateConversation updateConversation)
    {
        await _validator.ValidateAndThrowAsync(updateConversation);

        var conversation = await _repository.Get(updateConversation.ConversationId);
        if (conversation == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Conversation not found");
        }

        _intentionManager.ThrowIfForbidden(ConversationIntention.UpdateConversation, conversation);

        #pragma warning disable CS8603 // Possible null reference return - false positive with MaybeField fluent chain
        var updateBuilder = _updateBuilderFactory.Create<DbConversation>(updateConversation.ConversationId)
            .MaybeField(c => c.Title, updateConversation.Title?.Trim());
        #pragma warning restore CS8603

        var addParticipants = updateConversation.AddParticipants?
            .Except(conversation.Participants.Select(p => p.UserId))
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        var linksToAdd = addParticipants.Select(userId => new UserConversationLink
        {
            UserConversationLinkId = _guidFactory.Create(),
            ConversationId = updateConversation.ConversationId,
            UserId = userId,
            IsRemoved = false
        });

        var removeParticipants = updateConversation.RemoveParticipants?
            .Except(new[] { _identityProvider.Current.User.UserId })
            .ToArray() ?? Array.Empty<Guid>();

        var result = await _repository.Update(updateBuilder, linksToAdd, removeParticipants);

        if (addParticipants.Length > 0)
        {
            await _unreadCountersRepository.Create(
                updateConversation.ConversationId,
                UnreadEntryType.Message,
                addParticipants);
        }

        return result;
    }
}
