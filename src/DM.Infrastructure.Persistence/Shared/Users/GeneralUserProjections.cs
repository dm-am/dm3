using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using CoreUserContact = DM.Domain.Core.Users.UserContact;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// SSOT for projecting <see cref="GeneralUser"/> from the user row inside an
/// EF Select - the replacement for the nested ProjectTo user map that almost
/// every list projection composed.
///
/// The formula is a stored expression, inlined at every nested call site
/// with <see cref="ExpressionSplicer.Splice{T,TResult}"/>
/// (<c>GeneralUserProjections.Projection.Splice(x.Author)</c>). Inlining is
/// what keeps the SELECT narrow: the provider sees exactly the member
/// accesses the formula names and fetches only those columns. As an opaque
/// method the same formula forced EF to materialise the whole users row -
/// password hash, salt, pending email, bio - on every list path that nests
/// an author.
///
/// The avatar navigation is referenced inside the formula and becomes part
/// of the query the same way: after expansion the provider sees
/// <c>x.Author.AvatarUpload</c> and joins it.
///
/// Aggregate counts (reviews, endorsements, hosted games and the rest) are a
/// separate batch only the user repository runs; a nested user never carries
/// them, and their members stay null here.
/// </summary>
public static class GeneralUserProjections
{
    // ═══ The one formula for the shared row fields, whatever the target
    // tier. Tier formulas below are built from these bindings - never write
    // a second copy of this member list. ═══
    private static readonly Expression<Func<DbUser, GeneralUser>> RowFields =
        u => new GeneralUser
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role,
            AccessPolicy = u.AccessPolicy,
            LastActivityUtc = u.LastActivityUtc,
            Picture = AvatarProjections.Projection.Splice(u.AvatarUpload),
            Status = u.Status,
            Name = u.Name,
            Location = u.Location,
            Gender = u.Gender,
            BirthdayDate = u.BirthdayDate,
            ShowBirthday = u.ShowBirthday,
            RatingDisabled = u.RatingDisabled,
            QualityRating = u.QualityRating,
            QuantityRating = u.QuantityRating,
            RegisteredUtc = u.CreatedUtc,
            IsUnderModerationWatch = u.IsUnderModerationWatch
        };

    /// <summary>
    /// The nested-user formula, null-guarded for optional navigations
    /// (an absent editor or addressee projects as null, not as an empty
    /// user). Splice it wherever a projection nests a user; the projection
    /// root must then go through <see cref="ExpressionSplicer.Expand{TDelegate}"/>.
    /// </summary>
    public static readonly Expression<Func<DbUser?, GeneralUser>> Projection =
        u => u == null ? null! : RowFields.Splice(u!);

    /// <summary>
    /// The authenticated tier: the row fields plus the credential columns
    /// and the ban window. Whether a ban is in force is a question about
    /// time and belongs to whoever holds a clock, not to the projection;
    /// lifted bans are excluded here, in the open - they used to be dropped
    /// by the global soft-delete filter, which meant a filter written for
    /// tidying rows was quietly carrying an authorization rule.
    /// </summary>
    private static readonly Expression<Func<DbUser, AuthenticatedUser>> AuthenticatedRowFields =
        WithRowFields<AuthenticatedUser>(u => new AuthenticatedUser
        {
            Salt = u.Salt,
            PasswordHash = u.PasswordHash,
            PasswordHashVersion = u.PasswordHashVersion,
            IsRemoved = u.IsRemoved,
            AccessRestrictions = u.BansReceived
                .Where(b => b.LiftedUtc == null)
                .Select(b => new AccessRestriction(b.AccessRestrictionPolicy, b.StartedUtc, b.EndedUtc))
                .ToList()
        });

    /// <summary>
    /// The details tier: the row fields plus the account row facts
    /// (registration moment, bio, ordered contacts). Settings live in their
    /// own document and are read separately by the repository.
    /// </summary>
    private static readonly Expression<Func<DbUser, UserDetails>> DetailsRowFields =
        WithRowFields<UserDetails>(u => new UserDetails
        {
            CreatedUtc = u.CreatedUtc,
            Info = u.Info!,
            Contacts = u.Contacts
                .OrderBy(c => c.SortOrder)
                .Select(c => new CoreUserContact
                {
                    ContactType = c.ContactType,
                    ContactValue = c.ContactValue,
                    SortOrder = c.SortOrder
                })
                .ToList()
        });

    /// <summary>
    /// EF projection over the users table itself
    /// </summary>
    public static IQueryable<GeneralUser> ProjectToGeneralUser(this IQueryable<DbUser> query) =>
        query.Select(ExpressionSplicer.Expand(RowFields));

    /// <summary>
    /// EF projection to the authenticated identity
    /// </summary>
    public static IQueryable<AuthenticatedUser> ProjectToAuthenticatedUser(this IQueryable<DbUser> query) =>
        query.Select(ExpressionSplicer.Expand(AuthenticatedRowFields));

    /// <summary>
    /// The roster card: who a person is in a list of members, and nothing else.
    /// Blog readers and assistants, game master, mentor, assistants, players and
    /// readers are all answered with it.
    /// </summary>
    /// <remarks>
    /// NOT the narrow half of <see cref="Projection"/> and not composable with
    /// it. The avatar comes from <see cref="AvatarProjections.From" /> - the
    /// COMPILED formula - which the provider cannot translate, so it fetches the
    /// upload row and runs the formula on the client. That is what these
    /// eight queries did member for member before they were one, and it is
    /// deliberately left alone here: changing it is a performance decision about
    /// eight endpoints, not a tidy-up.
    ///
    /// Splice it into a projection root that goes through
    /// <see cref="ExpressionSplicer.Expand{TDelegate}" /> (or
    /// <see cref="ExpressionSplicer.SelectSpliced{TSource,TResult}" />).
    /// </remarks>
    internal static readonly Expression<Func<DbUser, GeneralUser>> RosterCard =
        u => new GeneralUser
        {
            UserId = u.UserId,
            Username = u.Username,
            Role = u.Role,
            Status = u.Status,
            LastActivityUtc = u.LastActivityUtc,
            Picture = AvatarProjections.From(u.AvatarUpload),
        };

    /// <summary>
    /// The account behind a login, compared the way the unique index over
    /// lower("Username") compares. Unlike <see cref="AccountReservation" /> this
    /// keeps the soft-delete filter on: it answers who is acting, not whether the
    /// login is held by somebody.
    /// </summary>
    public static Task<AuthenticatedUser?> FindAuthenticatedUser(
        this IQueryable<DbUser> query, string username) =>
        query
            .Where(u => u.Username.ToLower() == username.ToLower())
            .ProjectToAuthenticatedUser()
            .FirstOrDefaultAsync();

    /// <summary>
    /// EF projection to the user details
    /// </summary>
    public static IQueryable<UserDetails> ProjectToUserDetails(this IQueryable<DbUser> query) =>
        query.Select(ExpressionSplicer.Expand(DetailsRowFields));

    /// <summary>
    /// Builds a tier formula: one member-init over the derived type carrying
    /// the <see cref="RowFields"/> bindings first and the tier's own
    /// bindings after them. The shared member list stays a single text - the
    /// tiers only append to it.
    /// </summary>
    private static Expression<Func<DbUser, TTier>> WithRowFields<TTier>(
        Expression<Func<DbUser, TTier>> tierFields)
        where TTier : GeneralUser =>
        ExpressionSplicer.WithBaseBindings(RowFields, tierFields);
}
