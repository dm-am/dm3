using DM.Services.Core.Configuration;
using DM.Services.Mail.Rendering.Assets;
using DM.Services.Mail.Rendering.Rendering;
using DM.Services.Mail.Rendering.ViewModels;
using DM.Services.Mail.Sender;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset.Confirmation;

/// <inheritdoc />
internal class PasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly IntegrationSettings _integrationSettings;

    /// <inheritdoc />
    public PasswordResetEmailSender(
        IRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<IntegrationSettings> integrationOptions)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
        _integrationSettings = integrationOptions.Value;
    }

    /// <inheritdoc />
    public async Task Send(string email, string login, Guid token)
    {
        var confirmationLinkUrl = new Uri(new Uri(_integrationSettings.WebUrl), $"password/{token}");
        var emailBody = await _renderer.Render(new PasswordResetConfirmationViewModel(
            Login: login,
            ConfirmationLinkUrl: confirmationLinkUrl.ToString()));
        await _mailSender.Send(new MailLetter
        {
            Address = email,
            Subject = $"Подтверждение сброса пароля на DM.AM для {login}",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}