using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

internal interface IRegistrationMailSender
{
    Task Send(string email, Guid token);
}
