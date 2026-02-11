using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Deleting;

/// <inheritdoc />
internal class PostPendencyDeletingService : IPostPendencyDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IPostPendencyDeletingRepository _repository;

    /// <inheritdoc />
    public PostPendencyDeletingService(
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IPostPendencyDeletingRepository repository)
    {
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task Delete(Guid pendencyId)
    {
        var pendency = await _repository.Get(pendencyId);
        if (pendency == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Post pendency not found");
        }

        _intentionManager.ThrowIfForbidden(RoomIntention.DeletePostPendency, pendency);
        await _repository.Delete(_updateBuilderFactory.Create<PostPendency>(pendencyId).Delete());
    }
}
