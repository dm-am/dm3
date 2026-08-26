using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Enums;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using DM.Domain.Core.Configuration;
using DM.Domain.Core.Site;
using DM.Domain.Core.Mail;
using Microsoft.Extensions.Options;

namespace DM.Workers.NotificationDispatcher.Email;

/// <inheritdoc />
internal class NotificationEmailSender : INotificationEmailSender
{
    private readonly DmDbContext _dbContext;
    private readonly IMailSender _mailSender;
    private readonly ILogger<NotificationEmailSender> _logger;
    private readonly SiteAddressConfiguration _siteAddresses;

    public NotificationEmailSender(
        DmDbContext dbContext,
        IMailSender mailSender,
        IOptions<SiteAddressConfiguration> siteAddresses,
        ILogger<NotificationEmailSender> logger)
    {
        _dbContext = dbContext;
        _mailSender = mailSender;
        _siteAddresses = siteAddresses.Value;
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

        // Get user settings
        var settingsDict = await _dbContext.UserSettings
            .Where(s => userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, ct);

        // Get user emails
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

                // No settings row means the defaults, and email is off by default.
                settingsDict.TryGetValue(userId, out var settings);
                if (!NotificationChannels.ShouldSend(settings?.EmailPreferences, category.Value))
                {
                    continue;
                }

                // Send the email
                var subject = $"Dungeon Master: {NotificationText.GetTitle(eventType)}";

                var body = BuildEmailBody(eventType, notification.Metadata, _siteAddresses);

                await _mailSender.SendAsync(new EmailLetter
                {
                    Address = email,
                    Subject = subject,
                    Body = body
                });

                // Recipient identified by user, not by address: entries live for a
                // month and an address names a person.
                _logger.LogDebug("Sent email notification for event {EventType}", eventType);
            }
            catch (Exception ex)
            {
                // The message was consumed successfully - from the pipeline's side
                // it was, the swallowing is here - so a relay refusing every letter
                // is invisible to every rule about the queue.
                MessagingMetrics.DeliveryFailed.Add(1,
                    MessagingMetrics.Channel("email"),
                    new KeyValuePair<string, object?>("event", eventType.ToString()));
                _logger.LogWarning(ex, "Failed to send email notification to user {UserId} for event {EventType}",
                    userId, eventType);
                // Continue with other users
            }
        }
    }

    /// <summary>
    /// The letter as HTML.
    /// </summary>
    /// <remarks>
    /// Metadata carries titles and names their authors typed, and this is where they
    /// become markup, so this is where they are escaped, through the encoder the Razor
    /// templates in DM.Infrastructure.Mail render through. A game titled with an
    /// anchor tag used to arrive as a working link to another site inside a letter
    /// signed by this one.
    ///
    /// Internal rather than private so that the escaping can be checked without a
    /// database, a broker and a mailbox.
    /// </remarks>
    /// <param name="eventType">Event the letter is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the link and the list in the footer</param>
    internal static string BuildEmailBody(EventType eventType, object metadata, SiteAddressConfiguration addresses)
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

        var subject = NotificationText.EscapeHtml(NotificationText.GetTitle(eventType));
        sb.AppendLine($"<h2 style=\"color: #333;\">{subject}</h2>");

        // Format metadata as readable content
        var fields = NotificationText.ReadMetadata(metadata);
        if (fields.Count > 0)
        {
            sb.AppendLine("<dl style=\"margin: 0;\">");
            foreach (var (name, value) in fields)
            {
                sb.AppendLine(
                    $"<dt style=\"font-weight: bold; color: #555; margin-top: 10px;\">{NotificationText.EscapeHtml(name)}</dt>");
                sb.AppendLine(
                    $"<dd style=\"margin-left: 0; color: #333;\">{NotificationText.EscapeHtml(value)}</dd>");
            }

            sb.AppendLine("</dl>");
        }

        // The reader of a letter is the one reader with no notification list in front
        // of him, so the letter carries the way back to what it is about. Absolute,
        // because a path in a letter resolves against nothing; built from the table
        // the two bot channels read as well, so all three lead where the list leads.
        var target = NotificationLink.GetUrl(eventType, metadata, addresses);
        if (target != null)
        {
            sb.AppendLine("<p style=\"margin-top: 20px;\">" +
                          $"<a href=\"{NotificationText.EscapeHtml(target)}\">Перейти</a></p>");
        }

        sb.AppendLine("</div>");

        // Footer
        sb.AppendLine("<div style=\"padding: 15px; background: #eee; text-align: center; color: #666; font-size: 12px;\">");
        // The link is built on the address this deployment answers on, because a
        // letter built on one address and read by somebody who only reaches the
        // other one leads nowhere. The list below is the other half of that: a
        // mailbox is the only place a reader can still be reached once the address
        // he uses stops answering, and this is the letter he gets most often.
        //
        // The list is a fact of the product rather than a setting: as a setting it
        // was one every deployment had to fill in and none ever did, so the line
        // was empty everywhere.
        sb.AppendLine($"<p>Это автоматическое уведомление с сайта <a href=\"{addresses.PublicUrl}\">Dungeon Master</a></p>");
        if (SiteAddresses.Hosts.Count > 1)
        {
            sb.AppendLine($"<p>Сайт открывается по адресам: {string.Join(", ", SiteAddresses.Hosts)}</p>");
        }
        sb.AppendLine("<p>Вы можете отключить email-уведомления в настройках профиля.</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div></body></html>");

        return sb.ToString();
    }
}
