using DM.Services.Mail.Rendering.Assets;
using DM.Services.Mail.Rendering.Rendering;
using DM.Services.Mail.Rendering.ViewModels;
using DM.Services.Mail.Sender;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange.Confirmation;

/// <inheritdoc />
internal class PasswordChangeNotificationSender : IPasswordChangeNotificationSender
{
    private readonly IRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;

    /// <inheritdoc />
    public PasswordChangeNotificationSender(
        IRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
    }

    /// <inheritdoc />
    public async Task Send(string email, string login)
    {
        var emailBody = await _renderer.Render(new PasswordChangeNotificationViewModel(
            Login: login));
        await _mailSender.Send(new MailLetter
        {
            Address = email,
            Subject = "Ваш пароль на DM.AM был изменён",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
