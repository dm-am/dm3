namespace DM.Domain.Account.Features.Registration;

internal interface IUserFactory
{
    CreateUser CreateFromPending(PendingRegistration pending, string username);
}
