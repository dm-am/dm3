using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

/// <inheritdoc />
internal class PasswordResetMailSender : IPasswordResetMailSender
{
    private readonly ITemplateRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly SiteAddressConfiguration _siteAddresses;

    /// <inheritdoc />
    public PasswordResetMailSender(
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
    public async Task Send(string email, string username, Guid token)
    {
        var confirmationLinkUrl = new Uri(new Uri(_siteAddresses.PublicUrl), $"reset-password#token={token}");
        var emailBody = await _renderer.RenderAsync(new PasswordResetConfirmationViewModel(
            Username: username,
            ConfirmationLinkUrl: confirmationLinkUrl.ToString()));
        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = $"Dungeon Master: подтверждение сброса пароля для {username}",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
