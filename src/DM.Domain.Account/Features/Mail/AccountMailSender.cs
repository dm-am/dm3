using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Mail;

// Abstract, so the container scan, which registers concrete classes only, never makes it a service
// of its own.
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
    /// out of the browser history.
    /// </remarks>
    protected string ConfirmationLink(string page, Guid token) =>
        new Uri(new Uri(_siteAddresses.PublicUrl), $"{page}#token={token}").ToString();

    /// <summary>
    /// A mailed link to a page of the site that carries no secret.
    /// </summary>
    protected string SiteLink(string page) =>
        new Uri(new Uri(_siteAddresses.PublicUrl), page).ToString();

    /// <summary>
    /// Render the view model and send it, with the logo the templates reference.
    /// </summary>
    /// <param name="email">Address of the reader</param>
    /// <param name="topic">What the subject says after the name of the site</param>
    /// <param name="viewModel">Data of the template</param>
    protected async Task Deliver<TViewModel>(string email, string topic, TViewModel viewModel)
    {
        var body = await _renderer.RenderAsync(viewModel);
        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = MailSubject.Of(topic),
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
