using System;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc />
internal class EmailChangeWarningMailSender : IEmailChangeWarningMailSender
{
    private readonly IMailSender _mailSender;
    private readonly IEmailAssetsProvider _emailAssetsProvider;

    public EmailChangeWarningMailSender(
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider)
    {
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
    }

    /// <inheritdoc />
    public async Task SendAsync(string oldEmail, string username, string newEmail)
    {
        // Mask new email for privacy (show only first 2 chars and domain)
        var maskedNewEmail = MaskEmail(newEmail);

        var body = $@"
<html>
<body style='font-family: Arial, sans-serif; color: #333;'>
    <h2 style='color: #d9534f;'>⚠️ Запрос на смену email</h2>
    <p>Здравствуйте, {username}!</p>
    <p>Мы получили запрос на смену email-адреса для вашего аккаунта на Dungeon Master.</p>
    <table style='margin: 20px 0; border-collapse: collapse;'>
        <tr>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Текущий email:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd;'>{oldEmail}</td>
        </tr>
        <tr>
            <td style='padding: 8px; border: 1px solid #ddd;'><strong>Новый email:</strong></td>
            <td style='padding: 8px; border: 1px solid #ddd;'>{maskedNewEmail}</td>
        </tr>
    </table>
    <p><strong>Если это были вы</strong> — проигнорируйте это письмо. Подтверждение отправлено на новый адрес.</p>
    <p style='color: #d9534f;'><strong>Если это были не вы</strong> — немедленно смените пароль и свяжитесь с поддержкой!</p>
    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
    <p style='color: #666; font-size: 12px;'>
        Это автоматическое уведомление о безопасности. Мы отправляем его при любом запросе на смену email.
    </p>
</body>
</html>";

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = oldEmail,
            Subject = "⚠️ Запрос на смену email — Dungeon Master",
            Body = body,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***";

        var local = parts[0];
        var domain = parts[1];

        var maskedLocal = local.Length <= 2
            ? local
            : local[..2] + new string('*', Math.Min(local.Length - 2, 5));

        return $"{maskedLocal}@{domain}";
    }
}
