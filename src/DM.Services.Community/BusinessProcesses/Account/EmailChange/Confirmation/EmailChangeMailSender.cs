using DM.Services.Core.Configuration;
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
    private readonly IRenderer renderer;
    private readonly IMailSender mailSender;
    private readonly IntegrationSettings integrationSettings;

    /// <inheritdoc />
    public EmailChangeMailSender(
        IRenderer renderer,
        IMailSender mailSender,
        IOptions<IntegrationSettings> integrationSettings)
    {
        this.renderer = renderer;
        this.mailSender = mailSender;
        this.integrationSettings = integrationSettings.Value;
    }

    /// <inheritdoc />
    public async Task Send(string email, string login, Guid token)
    {
        var confirmationLinkUrl = new Uri(new Uri(integrationSettings.WebUrl), $"activate/{token}");
        var emailBody = await renderer.Render(new RegistrationConfirmationViewModel(
            login,
            confirmationLinkUrl.ToString()));
        await mailSender.Send(new MailLetter
        {
            Address = email,
            Subject = $"Подтверждение смены адреса электронной почты на DM.AM для {login}",
            Body = emailBody
        });
    }
}