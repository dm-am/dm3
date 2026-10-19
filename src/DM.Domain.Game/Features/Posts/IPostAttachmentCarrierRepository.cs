using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// The post a file hangs on, read past the room for a caller the file's own right admits.
/// </summary>
/// <remarks>
/// Not a read for a reader of the room: that one is <see cref="IPostRepository.Get(Guid, Guid, CancellationToken)"/>
/// with his own id. The owner of a file and Moderator+ are let past the room by the upload's intention and still
/// not past a hidden block, so the post is read for them wherever it stands, and asked only what its
/// text keeps from them (PostAttachmentVisibility). Nothing read here is shown to anybody.
/// </remarks>
public interface IPostAttachmentCarrierRepository
{
    /// <summary>The posts by id; one that is not there or was removed is absent from the answer.</summary>
    Task<IReadOnlyDictionary<Guid, Post>> GetCarriers(IReadOnlyCollection<Guid> postIds);
}
