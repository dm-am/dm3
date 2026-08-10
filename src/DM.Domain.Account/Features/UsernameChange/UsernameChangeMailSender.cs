using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeMailSender : IUsernameChangeMailSender
{
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly SiteAddressConfiguration _siteAddresses;

    public UsernameChangeMailSender(
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses)
    {
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
        _siteAddresses = siteAddresses.Value;
    }

    /// <inheritdoc />
    public async Task SendApprovalAsync(string email, string username, Guid approvalToken)
    {
        var approvalLink = new Uri(
            new Uri(_siteAddresses.PublicUrl),
            $"account/username-change/{approvalToken}");

        var body = $@"
<html>
<body style='font-family: Arial, sans-serif; color: #333;'>
    <h2>Запрос на смену имени одобрен</h2>
    <p>Здравствуйте, {username}!</p>
    <p>Ваш запрос на смену имени пользователя был <strong>одобрен</strong> модератором.</p>
    <p>Для завершения смены имени перейдите по ссылке:</p>
    <p><a href='{approvalLink}' style='display: inline-block; padding: 12px 24px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 4px;'>Выбрать новое имя</a></p>
    <p><small>Ссылка действительна 48 часов.</small></p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
    <p style='color: #666; font-size: 12px;'>Если вы не запрашивали смену имени, проигнорируйте это письмо.</p>
</body>
</html>";

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Dungeon Master: запрос на смену имени одобрен",
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }

    /// <inheritdoc />
    public async Task SendRejectionAsync(string email, string username, string? reason)
    {
        var reasonText = string.IsNullOrEmpty(reason)
            ? "Причина не указана."
            : $"Причина: {reason}";

        var body = $@"
<html>
<body style='font-family: Arial, sans-serif; color: #333;'>
    <h2>Запрос на смену имени отклонен</h2>
    <p>Здравствуйте, {username}!</p>
    <p>К сожалению, ваш запрос на смену имени пользователя был <strong>отклонен</strong> модератором.</p>
    <p>{reasonText}</p>
    <p>Вы можете подать новый запрос, предоставив более подробную причину для смены имени.</p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
    <p style='color: #666; font-size: 12px;'>Если у вас есть вопросы, обратитесь к модераторам.</p>
</body>
</html>";

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Dungeon Master: запрос на смену имени отклонен",
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
