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

    /// <inheritdoc />
    public async Task AssignGameMentor(Guid gameId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.GameExists(gameId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        var currentMentor = await _repository.GetGameMentorId(gameId, ct);
        if (currentMentor.HasValue)
        {
            throw new HttpException(HttpStatusCode.Conflict, "У игры уже есть наставник");
        }

        var userId = _identityProvider.Current.User.UserId;
        await _repository.SetGameMentor(gameId, userId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveGameMentor(Guid gameId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.GameExists(gameId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        var userId = _identityProvider.Current.User.UserId;
        var currentMentor = await _repository.GetGameMentorId(gameId, ct);

        if (!currentMentor.HasValue || currentMentor.Value != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Вы не наставник этой игры");
        }

        await _repository.SetGameMentor(gameId, null, ct);
    }

    /// <inheritdoc />
    public async Task AssignBlogMentor(Guid blogId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.BlogExists(blogId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var currentMentor = await _repository.GetBlogMentorId(blogId, ct);
        if (currentMentor.HasValue)
        {
            throw new HttpException(HttpStatusCode.Conflict, "У блога уже есть наставник");
        }

        var userId = _identityProvider.Current.User.UserId;
        await _repository.SetBlogMentor(blogId, userId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveBlogMentor(Guid blogId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.BlogExists(blogId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var userId = _identityProvider.Current.User.UserId;
        var currentMentor = await _repository.GetBlogMentorId(blogId, ct);

        if (!currentMentor.HasValue || currentMentor.Value != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Вы не наставник этого блога");
        }

        await _repository.SetBlogMentor(blogId, null, ct);
    }

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

    private void EnsureMentorRole()
    {
        var userRole = _identityProvider.Current.User.Role;
        if (userRole < UserRole.Mentor)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Нужна роль наставника или выше");
        }
    }

    /// <summary>
    /// Cross-mentor listings expose curation zones of OTHER users, so they
    /// require the moderation overview role, not just Mentor.
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
