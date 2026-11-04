using System;
using System.Linq.Expressions;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// The account a login or an address names, compared regardless of letter case the way
/// the unique indexes IX_Users_Username_Lower and IX_Users_Email_Lower compare.
/// </summary>
internal static class AccountNames
{
    public static Expression<Func<User, bool>> UsernameIs(string username) =>
        u => u.Username.ToLower() == username.ToLower();

    public static Expression<Func<User, bool>> EmailIs(string email) =>
        u => u.Email.ToLower() == email.ToLower();
}
