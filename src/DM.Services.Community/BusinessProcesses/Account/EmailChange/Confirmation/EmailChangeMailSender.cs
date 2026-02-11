using DM.Services.Core.Configuration;
using DM.Services.Mail.Rendering.Assets;
using DM.Services.Mail.Rendering.Rendering;
using DM.Services.Mail.Rendering.ViewModels;
using DM.Services.Mail.Sender;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;

/// <inheritdoc />
internal class EmailChangeMailSender : IEmailChangeMailSender
{
    private readonly IRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly IntegrationSettings _integrationSettings;

    /// <inheritdoc />
    public EmailChangeMailSender(
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
    public async Task Send(string email, string login, Guid token)
    {
        var confirmationLinkUrl = new Uri(new Uri(_integrationSettings.WebUrl), $"confirm-email/{token}");
        var emailBody = await _renderer.Render(new EmailChangeConfirmationViewModel(
            login,
            confirmationLinkUrl.ToString()));
        await _mailSender.Send(new MailLetter
        {
            Address = email,
            Subject = $"Подтверждение смены адреса электронной почты на DM.AM для {login}",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}