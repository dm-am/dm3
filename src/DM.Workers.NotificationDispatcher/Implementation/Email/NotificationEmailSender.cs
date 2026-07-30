using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

using DM.Domain.Core.Mail;
namespace DM.Workers.NotificationDispatcher.Implementation.Email;

/// <inheritdoc />
internal class NotificationEmailSender : MongoCollectionRepository<UserSettings>, INotificationEmailSender
{
    private readonly DmDbContext _dbContext;
    private readonly IMailSender _mailSender;
    private readonly ILogger<NotificationEmailSender> _logger;

    public NotificationEmailSender(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IMailSender mailSender,
        ILogger<NotificationEmailSender> logger) : base(mongoClient)
    {
        _dbContext = dbContext;
        _mailSender = mailSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendIfEnabled(CreateNotification notification, EventType eventType, CancellationToken ct = default)
    {
        var category = NotificationCategoryMapper.GetCategory(eventType);
        if (category == null)
        {
            return; // Event type not mapped to any category
        }

        // Get user IDs that need email notifications
        var userIds = notification.UsersInterested.ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        // Get user settings from MongoDB
        var settingsList = await Collection
            .Find(Filter.In(s => s.UserId, userIds))
            .ToListAsync(ct);

        var settingsDict = settingsList.ToDictionary(s => s.UserId);

        // Get user emails from PostgreSQL
        var userEmails = await _dbContext.Users
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Email })
            .ToDictionaryAsync(u => u.UserId, u => u.Email, ct);

        foreach (var userId in userIds)
        {
            try
            {
                if (!userEmails.TryGetValue(userId, out var email) || string.IsNullOrEmpty(email))
                {
                    continue;
                }

                // Check if email notifications are enabled for this user
                if (!settingsDict.TryGetValue(userId, out var settings))
                {
                    // User has no settings = use defaults (email disabled by default)
                    continue;
                }

                var emailPrefs = settings.EmailPreferences;
                if (emailPrefs == null || !emailPrefs.Enabled)
                {
                    continue; // Email channel disabled
                }

                if (!emailPrefs.EnabledCategories.Contains(category.Value))
                {
                    continue; // Category not enabled for email
                }

                // Send the email
                var subject = $"Dungeon Master: {NotificationText.GetTitle(eventType)}";

                var body = BuildEmailBody(eventType, notification.Metadata);

                await _mailSender.SendAsync(new EmailLetter
                {
                    Address = email,
                    Subject = subject,
                    Body = body
                });

                // Recipient identified by user, not by address: the log store has no
                // retention and an address names a person.
                _logger.LogDebug("Sent email notification for event {EventType}", eventType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email notification to user {UserId} for event {EventType}",
                    userId, eventType);
                // Continue with other users
            }
        }
    }

    private static string BuildEmailBody(EventType eventType, object metadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"></head><body>");
        sb.AppendLine("<div style=\"font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;\">");

        // Header
        sb.AppendLine("<div style=\"background: #4a90d9; color: white; padding: 20px; text-align: center;\">");
        sb.AppendLine("<h1 style=\"margin: 0;\">Dungeon Master</h1>");
        sb.AppendLine("</div>");

        // Content
        sb.AppendLine("<div style=\"padding: 20px; background: #f9f9f9;\">");

        var subject = NotificationText.GetTitle(eventType);
        sb.AppendLine($"<h2 style=\"color: #333;\">{subject}</h2>");

        // Format metadata as readable content
        if (metadata != null)
        {
            try
            {
                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                // Extract useful fields from metadata
                using var doc = JsonDocument.Parse(metadataJson);
                var root = doc.RootElement;

                sb.AppendLine("<dl style=\"margin: 0;\">");
                foreach (var prop in root.EnumerateObject())
                {
                    var name = NotificationText.FormatPropertyName(prop.Name);
                    var value = NotificationText.FormatPropertyValue(prop.Value);
                    if (!string.IsNullOrEmpty(value))
                    {
                        sb.AppendLine($"<dt style=\"font-weight: bold; color: #555; margin-top: 10px;\">{name}</dt>");
                        sb.AppendLine($"<dd style=\"margin-left: 0; color: #333;\">{value}</dd>");
                    }
                }
                sb.AppendLine("</dl>");
            }
            catch
            {
                // If metadata parsing fails, just show it as JSON
                sb.AppendLine("<pre style=\"background: #eee; padding: 10px; overflow: auto;\">");
                sb.AppendLine(JsonSerializer.Serialize(metadata));
                sb.AppendLine("</pre>");
            }
        }

        sb.AppendLine("</div>");

        // Footer
        sb.AppendLine("<div style=\"padding: 15px; background: #eee; text-align: center; color: #666; font-size: 12px;\">");
        sb.AppendLine("<p>Это автоматическое уведомление с сайта <a href=\"https://dm.am\">Dungeon Master</a></p>");
        sb.AppendLine("<p>Вы можете отключить email-уведомления в настройках профиля.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div></body></html>");

        return sb.ToString();
    }
}
