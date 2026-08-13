using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Registration confirmation email sender for email-first flow
/// </summary>
internal class RegistrationMailSender : IRegistrationMailSender
{
    private readonly ITemplateRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly SiteAddressConfiguration _siteAddresses;

    public RegistrationMailSender(
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
    public async Task Send(string email, Guid token)
    {
        var confirmationLinkUrl = new Uri(new Uri(_siteAddresses.PublicUrl), $"activate#token={token}");
        var emailBody = await _renderer.RenderAsync(new RegistrationConfirmationViewModel(
            ConfirmationLinkUrl: confirmationLinkUrl.ToString()));
        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Dungeon Master: подтвердите почту",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
