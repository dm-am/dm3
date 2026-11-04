using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

/// <inheritdoc />
internal class PasswordResetMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses,
    IOptions<TokenConfiguration> tokens)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IPasswordResetMailSender
{
    /// <inheritdoc />
    public Task Send(string email, Guid token)
    {
        return Deliver(email, "восстановление пароля", new PasswordResetConfirmationViewModel(
            ConfirmationLinkUrl: ConfirmationLink("reset-password", token),
            LinkLifetimeHours: tokens.Value.PasswordResetTokenLifetimeHours));
    }
}
