using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

internal interface IEmailChangeMailSender
{
    Task Send(string email, Guid token);
}
