using DM.Services.Core.Configuration;
using DM.Services.Mail.Rendering.Assets;
using DM.Services.Mail.Rendering.Rendering;
using DM.Services.Mail.Rendering.ViewModels;
using DM.Services.Mail.Sender;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.Registration.Confirmation;

/// <summary>
/// Registration confirmation email sender for email-first flow
/// </summary>
internal class RegistrationMailSender : IRegistrationMailSender
{
    private readonly IRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly IntegrationSettings _integrationSettings;

    public RegistrationMailSender(
        IRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<IntegrationSettings> integrationSettings)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
        _integrationSettings = integrationSettings.Value;
    }

    /// <inheritdoc />
    public async Task Send(string email, Guid token)
    {
        var confirmationLinkUrl = new Uri(new Uri(_integrationSettings.WebUrl), $"activate/{token}");
        var emailBody = await _renderer.Render(new RegistrationConfirmationViewModel(
            ConfirmationLinkUrl: confirmationLinkUrl.ToString()));
        await _mailSender.Send(new MailLetter
        {
            Address = email,
            Subject = "Подтвердите email на DM.AM",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
