using System.Threading.Tasks;

namespace DM.Domain.Account.Features.PasswordChange;

internal interface IPasswordChangeMailSender
{
    Task Send(string email);
}
