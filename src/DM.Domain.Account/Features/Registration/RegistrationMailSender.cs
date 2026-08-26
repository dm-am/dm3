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
internal class RegistrationMailSender : AccountMailSender, IRegistrationMailSender
{
    public RegistrationMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task Send(string email, Guid token)
    {
        var subject = "Dungeon Master: подтвердите почту";
        return Send(email, subject, new RegistrationConfirmationViewModel(
            ConfirmationLinkUrl: ConfirmationLink("activate", token)));
    }
}
