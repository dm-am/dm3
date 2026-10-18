using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.PasswordChange;

/// <inheritdoc cref="IPasswordChangeMailSender" />
internal class PasswordChangeMailSender : AccountMailSender, IPasswordChangeMailSender
{
    public PasswordChangeMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task Send(string email)
    {
        const string subject = "Dungeon Master: пароль изменен";
        return Send(email, subject, new PasswordChangeNotificationViewModel(
            SupportUrl: SiteLink("support?reason=access")));
    }
}
