using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.PasswordChange;

/// <inheritdoc />
internal class PasswordChangeMailSender : IPasswordChangeMailSender
{
    private readonly ITemplateRenderer _renderer;
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;

    /// <inheritdoc />
    public PasswordChangeMailSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
    }

    /// <inheritdoc />
    public async Task Send(string email, string username)
    {
        var emailBody = await _renderer.RenderAsync(new PasswordChangeNotificationViewModel(
            Username: username));
        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Ваш пароль на Dungeon Master был изменен",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
