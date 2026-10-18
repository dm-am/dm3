using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;
using DomainBanLogEntry = DM.Domain.Moderation.Features.Warnings.BanLogEntry;
using DomainBanLogEntryKind = DM.Domain.Moderation.Features.Warnings.BanLogEntryKind;
using DomainWarning = DM.Domain.Moderation.Features.Warnings.Warning;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Compile-time mapper for warnings and bans. The clock-dependent fields the
/// AutoMapper profile filled through BanTypeResolver/BanActivityResolver are
/// ordinary methods here, and both keep delegating the actual predicates to
/// the domain ban - the single definition of "permanent" and "banned right
/// now" that enforcement uses.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class WarningMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    private readonly IDateTimeProvider _dateTimeProvider;

    public WarningMapper(UserMapper userMapper, IDateTimeProvider dateTimeProvider)
    {
        _userMapper = userMapper;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Domain warning to the moderator-facing DTO, the evidence rendered
    /// </summary>
    public Warning ToWarning(DomainWarning warning)
    {
        var result = ToWarningCore(warning);
        result.EntityId = warning.EntityId != Guid.Empty ? warning.EntityId : null;
        result.Evidence = Evidence(warning);
        result.Profile = Profile(warning);
        return result;
    }

    /// <summary>
    /// The offending text, to be rendered on the way out by the rules of the
    /// place it was taken from: the global chat has a surface of its own, and
    /// the topic and the discussions share the comment's.
    /// </summary>
    /// <remarks>
    /// No render context is attached. The text is shown as its place shows it to
    /// a reader holding no role there, and a hidden block, which none of these
    /// places declares, is erased for everybody, the moderator reading the list
    /// included. Without a context the author-edit audience is refused as well,
    /// so the source never leaves the server through this field.
    /// </remarks>
    private static CommonBbText? Evidence(DomainWarning warning) => warning.EntitySnapshot switch
    {
        null => null,
        // "О себе" of a warned profile is kept in the same column, and it is
        // given with the profile, by the rules of the profile
        _ when warning.EntityType == WarningEntityType.Profile => null,
        var text when warning.EntityType == WarningEntityType.Message => new GlobalChatBbText { Value = text },
        var text => new CommonBbText { Value = text }
    };

    /// <summary>
    /// The profile a warning on a profile was issued for, as it stood then
    /// </summary>
    /// <remarks>
    /// "О себе" is rendered as the profile renders it to a guest: no context is
    /// attached, so the source never leaves the server through this field, as with
    /// the evidence. The picture is built by the code every avatar on the site is
    /// built by, from the upload the warning pinned. A part the profile did not
    /// have is left out rather than given empty.
    /// </remarks>
    private WarningProfile? Profile(DomainWarning warning)
    {
        if (warning.Profile is not { } profile)
        {
            return null;
        }

        return new WarningProfile
        {
            Status = string.IsNullOrWhiteSpace(profile.Status) ? null : profile.Status,
            Info = string.IsNullOrWhiteSpace(profile.Info) ? null : new InfoBbText { Value = profile.Info },
            Picture = profile.Picture is { SourceObjectKey: not null and not "" } picture
                ? _userMapper.ToUserPicture(picture)
                : null
        };
    }

    /// <summary>
    /// Domain warning to the public view: no reason, no moderator identity,
    /// no causation refs - a stranger sees points and dates
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial PublicWarning ToPublicWarning(DomainWarning warning);

    /// <summary>
    /// Domain warning to a row of the open log: the reason, both names, the
    /// second signature when the points were changed, and the offending text
    /// with its place, the address only where a guest could follow it
    /// </summary>
    public WarningLogEntry ToLogEntry(DomainWarning warning)
    {
        var entry = ToLogEntryCore(warning);
        var namesContent = warning.EntityId != Guid.Empty && warning.EntityType != WarningEntityType.Unknown;
        entry.EntityType = namesContent ? warning.EntityType.ToString() : null;
        entry.Evidence = Evidence(warning);
        entry.Profile = Profile(warning);

        // A closed place, a place that is gone and a text moderation took off the
        // site keep the text and the name of the place, and lose the link: it
        // would lead a guest to a page he cannot read, or to nothing.
        entry.EntityUrl = warning.LogClosure == null ? warning.EntityUrl : null;
        return entry;
    }

    [MapProperty(nameof(DomainWarning.WarningId), nameof(WarningLogEntry.Id))]
    [MapProperty(nameof(DomainWarning.TargetUser), nameof(WarningLogEntry.User))]
    [MapProperty(nameof(DomainWarning.Author), nameof(WarningLogEntry.Moderator))]
    [MapProperty(nameof(DomainWarning.Text), nameof(WarningLogEntry.Reason))]
    [MapProperty(nameof(DomainWarning.PointsChangedByUsername), nameof(WarningLogEntry.PointsChangedBy))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.EntityType))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.Evidence))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.Profile))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.EntityUrl))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial WarningLogEntry ToLogEntryCore(DomainWarning warning);

    /// <summary>
    /// Domain ban to the moderator-facing DTO
    /// </summary>
    public Ban ToBan(DomainBan ban)
    {
        var result = ToBanCore(ban);
        var now = _dateTimeProvider.Now;
        result.Type = ResolveBanType(ban, now);
        result.IsActive = ban.IsInForceAt(now);
        return result;
    }

    /// <summary>
    /// An entry of the domain ban list to the moderator-facing one
    /// </summary>
    public BanLogEntry ToBanLogEntry(DomainBanLogEntry entry) => new()
    {
        Number = entry.Number,
        Kind = entry.Kind == DomainBanLogEntryKind.Lifted ? BanLogEntryKind.Lifted : BanLogEntryKind.Issued,
        OccurredUtc = entry.OccurredUtc,
        Ban = ToBan(entry.Ban)
    };

    /// <summary>
    /// Domain ban to the public view
    /// </summary>
    public PublicBan ToPublicBan(DomainBan ban)
    {
        var result = ToPublicBanCore(ban);
        var now = _dateTimeProvider.Now;
        result.Type = ResolveBanType(ban, now);
        result.IsActive = ban.IsInForceAt(now);
        return result;
    }

    [MapProperty(nameof(DomainWarning.WarningId), nameof(Warning.Id))]
    [MapProperty(nameof(DomainWarning.TargetUser), nameof(Warning.User))]
    [MapProperty(nameof(DomainWarning.Author), nameof(Warning.Moderator))]
    [MapProperty(nameof(DomainWarning.Text), nameof(Warning.Reason))]
    [MapProperty(nameof(DomainWarning.PointsChangedByUsername), nameof(Warning.PointsChangedBy))]
    [MapperIgnoreTarget(nameof(Warning.EntityId))]
    [MapperIgnoreTarget(nameof(Warning.Evidence))]
    [MapperIgnoreTarget(nameof(Warning.Profile))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Warning ToWarningCore(DomainWarning warning);

    [MapProperty(nameof(DomainBan.BanId), nameof(Ban.Id))]
    [MapProperty(nameof(DomainBan.TargetUser), nameof(Ban.User))]
    [MapProperty(nameof(DomainBan.Author), nameof(Ban.Moderator))]
    [MapProperty(nameof(DomainBan.EndedUtc), nameof(Ban.ExpiresUtc))]
    [MapProperty(nameof(DomainBan.AccessRestrictionPolicy), nameof(Ban.AccessPolicy))]
    [MapperIgnoreTarget(nameof(Ban.Type))]
    [MapperIgnoreTarget(nameof(Ban.IsActive))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Ban ToBanCore(DomainBan ban);

    [MapProperty(nameof(DomainBan.EndedUtc), nameof(PublicBan.ExpiresUtc))]
    [MapperIgnoreTarget(nameof(PublicBan.Type))]
    [MapperIgnoreTarget(nameof(PublicBan.IsActive))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PublicBan ToPublicBanCore(DomainBan ban);

    // Permanence has no column of its own - it is carried by an end date far
    // in the future - so reading it back is a comparison against a threshold
    // that lives on the domain ban, next to the length a permanent ban is
    // written with.
    private static BanType ResolveBanType(DomainBan ban, DateTimeOffset moment)
    {
        if (ban.IsVoluntary)
        {
            return BanType.Voluntary;
        }

        return ban.IsPermanentAt(moment) ? BanType.Permanent : BanType.Temporary;
    }
}
