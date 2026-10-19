using System;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.PostPendencies;

public interface IPostPendencyService
{
    Task<PostPendency> CreateAsync(CreatePostPendency createPostPendency);

    Task DeleteAsync(Guid pendencyId);
}
