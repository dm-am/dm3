using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc />
internal class EmailChangeMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IEmailChangeMailSender
{
    /// <inheritdoc />
    public Task Send(string email, Guid token)
    {
        return Deliver(email, "смена почты", new EmailChangeConfirmationViewModel(
            ConfirmationLink("confirm-email", token)));
    }
}
