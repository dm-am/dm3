using System.Threading.Tasks;
using DM.Domain.Core.Mail;

namespace DM.Domain.Account.Features.UsernameChange;

/// <inheritdoc />
internal class UsernameChangeMailSender : IUsernameChangeMailSender
{
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;

    public UsernameChangeMailSender(
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider)
    {
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
    }

    /// <inheritdoc />
    public async Task SendApprovalAsync(string email, string username, string approvalLink)
    {
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

        await _mailSender.Send(new EmailLetter
        {
            Address = email,
            Subject = "Запрос на смену имени одобрен — DM.AM",
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
    <h2>Запрос на смену имени отклонён</h2>
    <p>Здравствуйте, {username}!</p>
    <p>К сожалению, ваш запрос на смену имени пользователя был <strong>отклонён</strong> модератором.</p>
    <p>{reasonText}</p>
    <p>Вы можете подать новый запрос, предоставив более подробную причину для смены имени.</p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
    <p style='color: #666; font-size: 12px;'>Если у вас есть вопросы, обратитесь к модераторам.</p>
</body>
</html>";

        await _mailSender.Send(new EmailLetter
        {
            Address = email,
            Subject = "Запрос на смену имени отклонён — DM.AM",
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
