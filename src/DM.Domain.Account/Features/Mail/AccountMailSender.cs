using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Mail;

/// <summary>
/// What every letter the account module sends has in common.
/// </summary>
/// <remarks>
/// Five senders declared the same four dependencies, the same constructor, the
/// same way of building a confirmation link and the same logo attachment. Only
/// the page the link points at, the view model and the subject differ, and those
/// stay with the sender that owns them.
///
/// Abstract on purpose: the container scan registers concrete classes only
/// (DefaultTypesRegistrationExtensions.IsDefaultRegistrationCandidate filters
/// IsAbstract), so this type never becomes a service of its own, and the public
/// constructor each sender declares is still the one the container activates.
/// </remarks>
internal abstract class AccountMailSender
{
    private readonly ITemplateRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly SiteAddressConfiguration _siteAddresses;

    protected AccountMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
        _siteAddresses = siteAddresses.Value;
    }

    /// <summary>
    /// A mailed confirmation link: the page, and the token in the FRAGMENT.
    /// </summary>
    /// <remarks>
    /// A fragment is not sent to a server, so the value stays out of the access
    /// log of the edge, out of the Referer of whatever the page then requests and
    /// out of the browser history. In the path it was in all three.
    /// </remarks>
    protected string ConfirmationLink(string page, Guid token) =>
        new Uri(new Uri(_siteAddresses.PublicUrl), $"{page}#token={token}").ToString();

    /// <summary>
    /// Render the view model and send it, with the logo the templates reference.
    /// </summary>
    protected async Task Send<TViewModel>(string email, string subject, TViewModel viewModel)
    {
        var body = await _renderer.RenderAsync(viewModel);
        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = subject,
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
