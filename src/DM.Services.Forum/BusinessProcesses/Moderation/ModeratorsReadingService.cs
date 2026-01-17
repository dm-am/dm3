using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Forum.BusinessProcesses.Boards;

namespace DM.Services.Forum.BusinessProcesses.Moderation;

/// <inheritdoc />
internal class ModeratorsReadingService : IModeratorsReadingService
{
    private readonly IForumReadingService _forumReadingService;
    private readonly IModeratorRepository _moderatorRepository;

    /// <inheritdoc />
    public ModeratorsReadingService(
        IForumReadingService forumReadingService,
        IModeratorRepository moderatorRepository)
    {
        _forumReadingService = forumReadingService;
        _moderatorRepository = moderatorRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetModerators(string forumTitle)
    {
        var forum = await _forumReadingService.GetForum(forumTitle);
        return await _moderatorRepository.Get(forum.Id);
    }
}