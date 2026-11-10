namespace DM.Domain.Account.Features.Authentication;

internal interface ISessionFactory
{
    CreateSession Create(bool persistent, SessionContext? context = null);
}
