using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeMailSender : AccountMailSender, IUsernameChangeMailSender
{
    public UsernameChangeMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
        : base(renderer, mailSender, emailAssetsProvider, siteAddresses)
    {
    }

    /// <inheritdoc />
    public Task SendApprovalAsync(string email, Guid approvalToken)
    {
        const string subject = "Dungeon Master: смена имени одобрена";
        return Send(email, subject, new UsernameChangeApprovalViewModel(
            // The link used to sit in the path, which put the token in the access
            // log, in the Referer and in the browser history - and pointed at an
            // address the site does not route, so the letter led to a 404 besides.
            ConfirmationLink("change-username", approvalToken)));
    }

    /// <inheritdoc />
    public Task SendRejectionAsync(string email, string? reason)
    {
        const string subject = "Dungeon Master: смена имени отклонена";
        return Send(email, subject,
            // The moderator's comment travels as text. The template writes it
            // through Razor, which escapes it, so the sender no longer encodes it
            // itself: encoding it twice would show the reader &lt;b&gt; where the
            // moderator typed a tag. It is the one value in this letter a person
            // types freely, and the column behind it takes five hundred characters
            // with no validator on the way. The name above it cannot carry markup
            // in either case, because UsernamePolicy refuses angle brackets and
            // quotes.
            new UsernameChangeRejectionViewModel(reason));
    }
}
