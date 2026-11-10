using System;
using System.Linq;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbUserEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

internal static class UserEndorsementMapper
{
    public static IQueryable<UserEndorsement> ProjectToUserEndorsement(
        this IQueryable<DbUserEndorsement> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbUserEndorsement, UserEndorsement>>(
            e => new UserEndorsement
            {
                Id = e.UserEndorsementId,
                CreatedUtc = e.CreatedUtc,
                ModifiedUtc = e.ModifiedUtc,
                Text = e.Text,
                Author = GeneralUserProjections.Projection.Splice(e.Author),
                TargetUser = GeneralUserProjections.Projection.Splice(e.TargetUser)
            }));
}
