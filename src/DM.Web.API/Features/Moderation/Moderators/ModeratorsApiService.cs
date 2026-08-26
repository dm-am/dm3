using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Moderation.Features.Mentorships;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Moderators;

/// <inheritdoc />
/// <remarks>
/// Composes existing domain services (same aggregation pattern as other
/// api-layer services): users by role from the community profile service,
/// board assignments from the forum board service (Board.ModeratorIds), and
/// game/blog curation zones from the mentorship service.
/// </remarks>
internal class ModeratorsApiService : IModeratorsApiService
{
    /// <summary>
    /// Roles shown on the moderation team page, highest first. System (robot)
    /// is deliberately excluded: it cannot login and has no moderation zones.
    /// </summary>
    private static readonly UserRole[] ModeratorRoles =
    [
        UserRole.Admin,
        UserRole.SeniorModerator,
        UserRole.Moderator
    ];

    private readonly ICommunityProfileService _profileService;
    private readonly IBoardService _boardService;
    private readonly IMentorshipService _mentorshipService;
    private readonly UserMapper _mapper;

    /// <inheritdoc />
    public ModeratorsApiService(
        ICommunityProfileService profileService,
        IBoardService boardService,
        IMentorshipService mentorshipService,
        UserMapper mapper)
    {
        _profileService = profileService;
        _boardService = boardService;
        _mentorshipService = mentorshipService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ModeratorOverview>> GetModerators()
    {
        // Sequential awaits: the scoped DbContext must not be used concurrently.
        // Each per-role listing is cached long-lived by the profile service.
        var users = new List<GeneralUser>();
        foreach (var role in ModeratorRoles)
        {
            users.AddRange(await _profileService.GetUsersByRole(role));
        }

        var userIds = users.Select(u => u.UserId).ToArray();
        var boards = (await _boardService.GetBoardsList()).ToArray();
        var gameZones = await _mentorshipService.GetGameMentorships(userIds);
        var blogZones = await _mentorshipService.GetBlogMentorships(userIds);

        var overviews = users.Select(user => new ModeratorOverview
        {
            User = _mapper.ToUser(user),
            Boards = boards
                .Where(b => b.ModeratorIds.Contains(user.UserId))
                .Select(b => new ModerationZone { Id = b.Id, Title = b.Title })
                .ToArray(),
            CuratedGames = ToZones(gameZones, user.UserId),
            CuratedBlogs = ToZones(blogZones, user.UserId)
        });

        return new ListEnvelope<ModeratorOverview>(overviews);
    }

    private static ModerationZone[] ToZones(
        IReadOnlyCollection<MentorshipAssignment> assignments, Guid userId) =>
        assignments
            .Where(a => a.MentorId == userId)
            .Select(a => new ModerationZone { Id = a.TargetId, Title = a.Title })
            .ToArray();
}
