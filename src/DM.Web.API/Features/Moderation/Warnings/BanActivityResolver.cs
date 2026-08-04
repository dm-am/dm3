using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Web.API.Features.Moderation.Bans;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Resolves whether a ban is in force at the current moment.
/// </summary>
/// <remarks>
/// The mapping used to spell the predicate out inline and got it wrong twice: it
/// dropped the start bound, so a ban whose window had not opened yet was listed
/// as active while the enforcement ignored it, and it read the wall clock
/// instead of the provider every ban query uses. Both answers now come from the
/// domain ban, which delegates to the single definition of "banned right now".
/// </remarks>
internal class BanActivityResolver :
    IValueResolver<DomainBan, Ban, bool>,
    IValueResolver<DomainBan, PublicBan, bool>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanActivityResolver(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public bool Resolve(DomainBan source, Ban destination, bool destMember, ResolutionContext context) =>
        source.IsInForceAt(_dateTimeProvider.Now);

    /// <inheritdoc />
    public bool Resolve(DomainBan source, PublicBan destination, bool destMember, ResolutionContext context) =>
        source.IsInForceAt(_dateTimeProvider.Now);
}
