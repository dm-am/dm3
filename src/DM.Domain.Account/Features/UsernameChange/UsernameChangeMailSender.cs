using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeMailSender : IUsernameChangeMailSender
{
    private readonly ITemplateRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly SiteAddressConfiguration _siteAddresses;

    public UsernameChangeMailSender(
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

    /// <inheritdoc />
    public async Task SendApprovalAsync(string email, string username, Guid approvalToken)
    {
        var approvalLink = new Uri(
            new Uri(_siteAddresses.PublicUrl),
            $"account/username-change/{approvalToken}");

        var body = await _renderer.RenderAsync(new UsernameChangeApprovalViewModel(
            username,
            approvalLink.ToString()));

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Dungeon Master: запрос на смену имени одобрен",
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }

    /// <inheritdoc />
    public async Task SendRejectionAsync(string email, string username, string? reason)
    {
        // The moderator's comment travels as text. The template writes it through
        // Razor, which escapes it, so the sender no longer encodes it itself:
        // encoding it twice would show the reader &lt;b&gt; where the moderator
        // typed a tag. It is the one value in this letter a person types freely,
        // and the column behind it takes five hundred characters with no validator
        // on the way. The name above it cannot carry markup in either case, because
        // UsernamePolicy refuses angle brackets and quotes.
        var body = await _renderer.RenderAsync(new UsernameChangeRejectionViewModel(
            username,
            reason));

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Dungeon Master: запрос на смену имени отклонен",
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
