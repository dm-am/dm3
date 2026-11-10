using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

internal interface IPasswordResetMailSender
{
    Task Send(string email, Guid token);
}
