using System.Linq;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Features.Polls;
using DomainPollOption = DM.Domain.Community.Features.Polls.PollOption;

namespace DM.Web.API.Features.Community.Polls;

/// <inheritdoc />
internal class PollParticipationResolver : IValueResolver<DomainPollOption, PollOption, bool?>
{
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public PollParticipationResolver(
        IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public bool? Resolve(DomainPollOption source, PollOption destination, bool? destMember, ResolutionContext context)
    {
        var currentUser = _identityProvider.Current.User;
        return currentUser.IsAuthenticated
            ? source.UserIds.Contains(currentUser.UserId)
            : null;
    }
}
