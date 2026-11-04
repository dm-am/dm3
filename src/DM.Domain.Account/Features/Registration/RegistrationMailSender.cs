using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Registration confirmation email sender for email-first flow
/// </summary>
internal class RegistrationMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses,
    IOptions<TokenConfiguration> tokens)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IRegistrationMailSender
{
    /// <inheritdoc />
    public Task Send(string email, Guid token)
    {
        return Deliver(email, "подтвердите почту", new RegistrationConfirmationViewModel(
            ConfirmationLinkUrl: ConfirmationLink("activate", token),
            LinkLifetimeHours: tokens.Value.ActivationTokenLifetimeHours));
    }
}
