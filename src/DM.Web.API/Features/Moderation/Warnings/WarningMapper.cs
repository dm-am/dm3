using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainBan = DM.Domain.Moderation.Features.Bans.Ban;
using DomainBanLogEntry = DM.Domain.Moderation.Features.Bans.BanLogEntry;
using DomainCreateWarning = DM.Domain.Moderation.Features.Warnings.CreateWarning;
using DomainWarning = DM.Domain.Moderation.Features.Warnings.Warning;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Compile-time mapper for warnings and bans. The clock-dependent fields delegate
/// to the domain ban, the one definition of "permanent" and "banned now" enforcement uses.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
[UseStaticMapper(typeof(UserRefMappers))]
public partial class WarningMapper
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
    /// The offending text, rendered by the rules of the place it was taken from.
    /// </summary>
    /// <remarks>
    /// No render context: a hidden block is erased for everybody and the author-edit
    /// audience is refused, so the source never leaves the server through this field.
    /// </remarks>
    private static CommonBbText? Evidence(DomainWarning warning) => warning.EntitySnapshot switch
    {
        null => null,
        var text => warning.EntityType switch
        {
            WarningEntityType.Message => new GlobalChatBbText { Value = text },
            // Unknown: a copy carried over with no place, given to moderation alone.
            WarningEntityType.Comment or WarningEntityType.Topic or WarningEntityType.GameReview or
                WarningEntityType.PostReview or WarningEntityType.Unknown => new CommonBbText { Value = text },
            // "О себе" of a warned profile is kept in the same column, and it is given with
            // the profile, by the rules of the profile.
            _ => null
        }
    };

    /// <summary>
    /// The profile a warning on a profile was issued for, as it stood then
    /// </summary>
    /// <remarks>
    /// "О себе" is rendered as to a guest, with no context, as the evidence is.
    /// A part the profile did not have is left out rather than given empty.
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

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreateWarning ToCreateWarning(CreateWarningRequest request);

    /// <summary>
    /// Domain warning to a row of the open log: the reason, both names, the
    /// second signature when the points were changed, and the offending text
    /// with its place where the reader reads it there, the address only where
    /// he could follow it
    /// </summary>
    /// <remarks>
    /// The row of its reader, whose reading of a place closed to a guest the
    /// service has asked; moderation reads every text in GET /v1/moderation/warnings.
    /// </remarks>
    public WarningLogEntry ToLogEntry(DomainWarning warning)
    {
        var entry = ToLogEntryCore(warning);
        var namesContent = warning.EntityId != Guid.Empty && warning.EntityType != WarningEntityType.Unknown;
        entry.EntityType = namesContent ? warning.EntityType.ToString() : null;
        entry.Profile = Profile(warning);

        // A place the reader does not read, or none found, is one block of
        // restricted access: the text, the place, its address and the date go
        // alike, so a refusal reads as an absence. A place that is gone and a text
        // moderation took off the site keep the text and the title, and lose the
        // link: it would lead to nothing.
        var restricted = namesContent && !warning.LogShowsText;
        entry.AccessRestricted = restricted ? true : null;
        entry.Evidence = warning.LogShowsText ? Evidence(warning) : null;
        entry.PlaceKind = restricted ? null : warning.PlaceKind;
        entry.PlaceTitle = warning.LogShowsText ? warning.PlaceTitle : null;
        entry.EntityCreatedUtc = restricted ? null : warning.EntityCreatedUtc;
        entry.EntityUrl = warning.LogClosure == null ? warning.EntityUrl : null;
        return entry;
    }

    [MapProperty(nameof(DomainWarning.WarningId), nameof(WarningLogEntry.Id))]
    [MapProperty(nameof(DomainWarning.TargetUser), nameof(WarningLogEntry.User))]
    [MapProperty(nameof(DomainWarning.Author), nameof(WarningLogEntry.Moderator))]
    [MapProperty(nameof(DomainWarning.Text), nameof(WarningLogEntry.Reason))]
    [MapProperty(nameof(DomainWarning.PointsChangedByUsername), nameof(WarningLogEntry.PointsChangedBy))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.EntityType))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.AccessRestricted))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.Evidence))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.Profile))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.EntityUrl))]
    [MapperIgnoreTarget(nameof(WarningLogEntry.PlaceTitle))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial WarningLogEntry ToLogEntryCore(DomainWarning warning);

    /// <summary>
    /// Domain ban to the moderator-facing DTO
    /// </summary>
    public Ban ToBan(DomainBan ban)
    {
        var result = ToBanCore(ban);
        (result.Type, result.IsActive) = StateOf(ban);
        return result;
    }

    /// <summary>
    /// An entry of the domain ban list to the moderator-facing one
    /// </summary>
    public BanLogEntry ToBanLogEntry(DomainBanLogEntry entry) => new()
    {
        Number = entry.Number,
        Kind = entry.Kind,
        OccurredUtc = entry.OccurredUtc,
        Ban = ToBan(entry.Ban)
    };

    /// <summary>
    /// Domain ban to the public view
    /// </summary>
    public PublicBan ToPublicBan(DomainBan ban)
    {
        var result = ToPublicBanCore(ban);
        (result.Type, result.IsActive) = StateOf(ban);
        return result;
    }

    private (BanType Type, bool IsActive) StateOf(DomainBan ban)
    {
        var now = _dateTimeProvider.Now;
        return (ResolveBanType(ban, now), ban.IsInForceAt(now));
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

    // Permanence is an end date far in the future, against a threshold on the domain ban.
    private static BanType ResolveBanType(DomainBan ban, DateTimeOffset moment)
    {
        if (ban.IsVoluntary)
        {
            return BanType.Voluntary;
        }

        return ban.IsPermanentAt(moment) ? BanType.Permanent : BanType.Temporary;
    }
}
