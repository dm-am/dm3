using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

/// <inheritdoc />
internal class PasswordResetMailSender : AccountMailSender, IPasswordResetMailSender
{
    /// <inheritdoc />
    public PasswordResetMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task Send(string email, string username, Guid token)
    {
        var subject = $"Dungeon Master: подтверждение сброса пароля для {username}";
        return Send(email, subject, new PasswordResetConfirmationViewModel(
            Username: username,
            ConfirmationLinkUrl: ConfirmationLink("reset-password", token)));
    }
}
