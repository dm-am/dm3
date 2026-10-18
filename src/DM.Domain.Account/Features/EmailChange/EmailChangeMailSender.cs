using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc />
internal class EmailChangeMailSender : AccountMailSender, IEmailChangeMailSender
{
    /// <inheritdoc />
    public EmailChangeMailSender(
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
        const string subject = "Dungeon Master: смена почты";
        return Send(email, subject, new EmailChangeConfirmationViewModel(
            ConfirmationLink("confirm-email", token)));
    }
}
