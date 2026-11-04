using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Enums;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Domain.Core.Mail.ViewModels;
using Microsoft.Extensions.Options;

namespace DM.Workers.NotificationDispatcher.Email;

/// <inheritdoc />
internal class NotificationEmailSender : INotificationEmailSender
{
    private readonly DmDbContext _dbContext;
    private readonly IMailSender _mailSender;
    private readonly ITemplateRenderer _renderer;
    private readonly IEmailAssetsProvider _emailAssetsProvider;
    private readonly ILogger<NotificationEmailSender> _logger;
    private readonly SiteAddressConfiguration _siteAddresses;

    public NotificationEmailSender(
        DmDbContext dbContext,
        IMailSender mailSender,
        ITemplateRenderer renderer,
        IEmailAssetsProvider emailAssetsProvider,
        IOptions<SiteAddressConfiguration> siteAddresses,
        ILogger<NotificationEmailSender> logger)
    {
        _dbContext = dbContext;
        _mailSender = mailSender;
        _renderer = renderer;
        _emailAssetsProvider = emailAssetsProvider;
        _siteAddresses = siteAddresses.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendIfEnabled(CreateNotification notification, EventType eventType, CancellationToken ct)
    {
        var category = NotificationCategoryMapper.GetCategory(eventType);
        if (category == null)
        {
            return; // Event type not mapped to any category
        }

        var userIds = notification.UsersInterested.ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        var settingsDict = await _dbContext.UserSettings
            .Where(s => userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, ct);

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

                var body = await _renderer.RenderAsync(Letter(eventType, notification.Metadata, _siteAddresses));

                await _mailSender.SendAsync(new EmailLetter
                {
                    Address = email,
                    Subject = MailSubject.Of(NotificationWording.GetTitle(eventType)),
                    Body = body,
                    LinkedResources = [_emailAssetsProvider.GetLogo()]
                });
            }
            catch (Exception ex)
            {
                // The message counts as consumed, so this counter is the only trace of a refusing relay.
                MessagingMetrics.DeliveryFailed.Add(1,
                    MessagingMetrics.Channel("email"),
                    new KeyValuePair<string, object?>("event", eventType.ToString()));
                _logger.LogWarning(ex, "Failed to send email notification to user {UserId} for event {EventType}",
                    userId, eventType);
            }
        }
    }

    /// <summary>
    /// What the letter says. The template escapes the metadata, which carries text authors typed.
    /// </summary>
    /// <param name="eventType">Event the letter is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the link</param>
    internal static NotificationLetterViewModel Letter(
        EventType eventType, object metadata, SiteAddressConfiguration addresses) =>
        new(
            Title: NotificationWording.GetTitle(eventType),
            Facts: NotificationWording.ReadMetadata(metadata),
            // Absolute: a path in a letter resolves against nothing.
            LinkUrl: NotificationLink.GetUrl(eventType, metadata, addresses),
            LinkCaption: NotificationLink.Caption);
}
