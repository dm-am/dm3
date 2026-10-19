using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Moderation.Features.Mentorships;

/// <inheritdoc />
internal class MentorshipService : IMentorshipService
{
    private readonly IMentorshipRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    public MentorshipService(
        IMentorshipRepository repository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// What a game or a blog differs by under curation: its reads, its write and its refusals.
    /// </summary>
    private sealed record Curated(
        Func<Guid, CancellationToken, Task<bool>> Exists,
        Func<Guid, CancellationToken, Task<Guid?>> GetMentorId,
        Func<Guid, Guid?, CancellationToken, Task> SetMentor,
        string NotFound,
        string AlreadyCurated,
        string NotYours);

    private Curated Game => new(
        _repository.GameExists, _repository.GetGameMentorId, _repository.SetGameMentor,
        RefusalMessage.GameNotFound, "У игры уже есть наставник", "Вы не наставник этой игры");

    private Curated Blog => new(
        _repository.BlogExists, _repository.GetBlogMentorId, _repository.SetBlogMentor,
        RefusalMessage.BlogNotFound, "У блога уже есть наставник", "Вы не наставник этого блога");

    /// <inheritdoc />
    public Task AssignGameMentor(Guid gameId, CancellationToken ct = default) => Assign(Game, gameId, ct);

    /// <inheritdoc />
    public Task RemoveGameMentor(Guid gameId, CancellationToken ct = default) => Remove(Game, gameId, ct);

    /// <inheritdoc />
    public Task AssignBlogMentor(Guid blogId, CancellationToken ct = default) => Assign(Blog, blogId, ct);

    /// <inheritdoc />
    public Task RemoveBlogMentor(Guid blogId, CancellationToken ct = default) => Remove(Blog, blogId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MentorshipAssignment>> GetGameMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default)
    {
        EnsureModeratorRole();
        if (mentorIds.Count == 0)
        {
            return Array.Empty<MentorshipAssignment>();
        }

        return await _repository.GetGameMentorships(mentorIds, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<MentorshipAssignment>> GetBlogMentorships(
        IReadOnlyCollection<Guid> mentorIds, CancellationToken ct = default)
    {
        EnsureModeratorRole();
        if (mentorIds.Count == 0)
        {
            return Array.Empty<MentorshipAssignment>();
        }

        return await _repository.GetBlogMentorships(mentorIds, ct);
    }

    private async Task Assign(Curated target, Guid targetId, CancellationToken ct)
    {
        EnsureMentorRole();
        await ThrowIfMissing(target, targetId, ct);

        var currentMentor = await target.GetMentorId(targetId, ct);
        if (currentMentor.HasValue)
        {
            throw new HttpException(HttpStatusCode.Conflict, target.AlreadyCurated);
        }

        var userId = _identityProvider.Current.User.UserId;
        await target.SetMentor(targetId, userId, ct);
    }

    private async Task Remove(Curated target, Guid targetId, CancellationToken ct)
    {
        EnsureMentorRole();
        await ThrowIfMissing(target, targetId, ct);

        var userId = _identityProvider.Current.User.UserId;
        var currentMentor = await target.GetMentorId(targetId, ct);

        if (currentMentor != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, target.NotYours);
        }

        await target.SetMentor(targetId, null, ct);
    }

    private static async Task ThrowIfMissing(Curated target, Guid targetId, CancellationToken ct)
    {
        if (!await target.Exists(targetId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, target.NotFound);
        }
    }

    private void EnsureMentorRole()
    {
        var userRole = _identityProvider.Current.User.Role;
        if (userRole < UserRole.Mentor)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нужна роль наставника или выше");
        }
    }

    /// <summary>
    /// Cross-mentor listings expose the curation of other users, so they need the
    /// moderation overview role, not just Mentor.
    /// </summary>
    private void EnsureModeratorRole()
    {
        var userRole = _identityProvider.Current.User.Role;
        if (userRole < UserRole.Moderator)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нужна роль модератора или выше");
        }
    }
}
