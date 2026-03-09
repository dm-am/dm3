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
    private readonly IntegrationSettings _integrationSettings;

    public RegistrationMailSender(
        ITemplateRenderer renderer,
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
        var emailBody = await _renderer.RenderAsync(new RegistrationConfirmationViewModel(
            ConfirmationLinkUrl: confirmationLinkUrl.ToString()));
        await _mailSender.Send(new EmailLetter
        {
            Address = email,
            Subject = "Подтвердите email на DM.AM",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
