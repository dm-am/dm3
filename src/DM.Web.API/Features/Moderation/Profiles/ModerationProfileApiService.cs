using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Web.API.Shared.Dto;
using DomainUserIpInfo = DM.Domain.Account.Features.Authentication.UserIpInfo;
using DomainLinkedProfile = DM.Domain.Account.Features.Authentication.LinkedProfile;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <inheritdoc />
internal class ModerationProfileApiService : IModerationProfileApiService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserService _userService;
    private readonly ILoginRecordRepository _loginRecordRepository;
    private readonly DM.Web.API.Features.Moderation.Warnings.IWarningApiService _warningApiService;
    private readonly DM.Web.API.Features.Moderation.Bans.IBanApiService _banApiService;
    private readonly IModeratedProfileNoteApiService _modNoteApiService;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ModerationProfileApiService(
        IIdentityProvider identityProvider,
        IUserService userService,
        ILoginRecordRepository loginRecordRepository,
        DM.Web.API.Features.Moderation.Warnings.IWarningApiService warningApiService,
        DM.Web.API.Features.Moderation.Bans.IBanApiService banApiService,
        IModeratedProfileNoteApiService modNoteApiService,
        IDateTimeProvider dateTimeProvider)
    {
        _identityProvider = identityProvider;
        _userService = userService;
        _loginRecordRepository = loginRecordRepository;
        _warningApiService = warningApiService;
        _banApiService = banApiService;
        _modNoteApiService = modNoteApiService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Envelope<ModerationProfileDto>> GetModerationProfile(string login)
    {
        var caller = _identityProvider.Current.User;
        var callerRole = caller.Role;
        var isAdmin = callerRole >= UserRole.Admin;
        var isSeniorMod = callerRole >= UserRole.SeniorModerator;

        // Get target user
        var user = await _userService.GetDetailsAsync(login);
        if (user == null)
            throw new HttpException(HttpStatusCode.NotFound, $"User '{login}' not found");

        // Sequential on purpose: every query below runs on this request's
        // single DbContext, and EF forbids concurrent operations on one
        // context. Admin-only queries are skipped for lower roles.
        var linkedProfiles = await _loginRecordRepository.GetLinkedProfiles(user.UserId);
        var modNotes = await _modNoteApiService.GetNotes(login);
        var warningsInfo = await _warningApiService.GetUserWarnings(login);
        var banStatus = await _banApiService.GetUserBanStatus(login);

        var ips = isAdmin
            ? MapIpInfos(await _loginRecordRepository.GetUserIps(user.UserId))
            : null;
        var loginHistory = isAdmin
            ? MapLoginHistory(await _loginRecordRepository.GetLoginHistory(user.UserId))
            : null;

        return new Envelope<ModerationProfileDto>(new ModerationProfileDto
        {
            Username = user.Username,
            UserId = user.UserId,
            RegistrationUtc = user.CreatedUtc,

            // Admin-only fields (null for non-admin callers)
            Email = isAdmin ? user.Email : null,
            IpAddresses = ips,
            LoginHistory = loginHistory,

            // Moderator+ fields
            LinkedProfiles = MapLinkedProfiles(linkedProfiles),
            ModeratorNotes = MapModNotes(modNotes, caller.UserId, isSeniorMod),

            Violations = new ViolationSummaryDto
            {
                TotalWarnings = warningsInfo.ActiveCount,
                ActiveWarningPoints = warningsInfo.TotalPoints,
                TotalBans = banStatus.History?.Count() ?? 0,
                IsCurrentlyBanned = banStatus.IsBanned,
                CurrentBanEndUtc = banStatus.ActiveBan?.ExpiresUtc,
                CurrentBanReason = banStatus.ActiveBan?.Comment
            },

            Permissions = new ModerationPermissionsDto
            {
                CanViewEmail = isAdmin,
                CanViewIpAddresses = isAdmin,
                CanViewLoginHistory = isAdmin,
                CanViewLinkedProfiles = true, // Controller already requires Moderator+
                CanViewModNotes = true,
                CanCreateModNote = true,
                CanIssueWarning = true,
                CanIssueBan = isSeniorMod,
                CanLiftBan = isSeniorMod
            }
        });
    }

    private static IReadOnlyList<UserIpInfoDto> MapIpInfos(IReadOnlyList<DomainUserIpInfo> ips)
    {
        if (ips == null) return [];
        return ips.Select(ip => new UserIpInfoDto
        {
            IpAddress = ip.IpAddress,
            FirstSeenUtc = ip.FirstSeenUtc,
            LastSeenUtc = ip.LastSeenUtc,
            LoginsCount = ip.LoginsCount
        }).ToList();
    }

    private static IReadOnlyList<LoginRecordDto> MapLoginHistory(IReadOnlyList<UserLoginRecord> records)
    {
        if (records == null) return [];
        return records.Select(r => new LoginRecordDto
        {
            LoginUtc = r.LoginUtc,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent,
            IsSuccessful = r.IsSuccessful
        }).ToList();
    }

    private static IReadOnlyList<LinkedProfileDto> MapLinkedProfiles(IReadOnlyList<DomainLinkedProfile> profiles)
    {
        return profiles.Select(p => new LinkedProfileDto
        {
            UserId = p.UserId,
            Username = p.Username,
            SharedIpsCount = p.SharedIpsCount,
            LastSharedLoginUtc = p.LastSharedLoginUtc
        }).ToList();
    }

    private static IReadOnlyList<ModNoteDto> MapModNotes(
        IEnumerable<ModeratedProfileNote> notes,
        Guid callerId,
        bool isSeniorMod)
    {
        return notes.Select(n => new ModNoteDto
        {
            Id = n.Id,
            AuthorUsername = n.Author?.Username ?? string.Empty,
            AuthorId = n.Author?.Id ?? Guid.Empty,
            Text = n.Text,
            CreatedUtc = n.CreatedUtc,
            ModifiedUtc = n.ModifiedUtc,
            CanEdit = isSeniorMod || (n.Author?.Id == callerId),
            CanDelete = isSeniorMod || (n.Author?.Id == callerId)
        }).ToList();
    }
}
