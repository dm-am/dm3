using System;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Parsing;
using DM.Domain.Core.Mail.ViewModels;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class SuspiciousLoginNotificationSender : ISuspiciousLoginNotificationSender
{
    private readonly ITemplateRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;

    public SuspiciousLoginNotificationSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
    }

    /// <inheritdoc />
    public async Task SendAsync(string email, string username, string? ipAddress, string? userAgent)
    {
        var deviceInfo = UserAgentParser.Parse(userAgent);
        var timestamp = DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm UTC");

        var emailBody = await _renderer.RenderAsync(new SuspiciousLoginViewModel(
            Username: username,
            IpAddress: ipAddress,
            DeviceInfo: deviceInfo,
            Timestamp: timestamp));

        await _mailSender.Send(new EmailLetter
        {
            Address = email,
            Subject = "Вход в аккаунт с нового устройства — DM.AM",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
