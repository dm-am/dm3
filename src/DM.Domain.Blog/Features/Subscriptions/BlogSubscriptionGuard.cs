using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Subscriptions;

namespace DM.Domain.Blog.Features.Subscriptions;

/// <inheritdoc />
/// <remarks>
/// The one place the blog's rule on being subscribed to is written. Both handles
/// that create a blog subscription ask it: the blog's own and the generic
/// endpoint, which previously created the row knowing nothing about blacklists.
///
/// Subscribing is writing. It puts the user on the blog's list of readers and on
/// the fan-out of every publication the blog announces. Reading the blog is open
/// to a blacklisted user and stays open; joining it is what the blacklist
/// refuses. A blog has no command for removing a reader, so the blacklist entry
/// ends the subscription itself, and this keeps the user from walking straight
/// back in.
/// </remarks>
internal class BlogSubscriptionGuard : ISubscriptionTargetGuard
{
    private readonly IBlogBlacklistRepository _blacklistRepository;

    public BlogSubscriptionGuard(IBlogBlacklistRepository blacklistRepository)
    {
        _blacklistRepository = blacklistRepository;
    }

    /// <inheritdoc />
    public SubscriptionTargetType TargetType => SubscriptionTargetType.Blog;

    /// <inheritdoc />
    public async Task<string?> Refusal(Guid targetId, Guid subscriberId, CancellationToken ct = default) =>
        await _blacklistRepository.IsBlocked(targetId, subscriberId, ct)
            ? RefusalMessage.BlacklistedFromBlog
            : null;
}
