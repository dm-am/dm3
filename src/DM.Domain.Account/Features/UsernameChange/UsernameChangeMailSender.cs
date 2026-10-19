using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeMailSender(
    ITemplateRenderer renderer,
    IMailSender mailSender,
    IEmailAssetsProvider emailAssetsProvider,
    IOptions<SiteAddressConfiguration> siteAddresses,
    IOptions<TokenConfiguration> tokens)
    : AccountMailSender(renderer, mailSender, emailAssetsProvider, siteAddresses), IUsernameChangeMailSender
{
    /// <inheritdoc />
    public Task SendApprovalAsync(string email, Guid approvalToken)
    {
        return Deliver(email, "смена имени одобрена", new UsernameChangeApprovalViewModel(
            ApprovalLinkUrl: ConfirmationLink("change-username", approvalToken),
            LinkLifetimeHours: tokens.Value.UsernameChangeApprovalTokenLifetimeHours));
    }

    /// <inheritdoc />
    public Task SendRejectionAsync(string email, string? reason)
    {
        return Deliver(email, "смена имени отклонена",
            // Plain text: Razor escapes it, encoding it here too would show &lt;b&gt;.
            new UsernameChangeRejectionViewModel(reason));
    }
}
