using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dates;
using DM.Domain.Core.Geo;
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
    private readonly IIpCountryLookup _countryLookup;

    public SuspiciousLoginNotificationSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IIpCountryLookup countryLookup)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
        _countryLookup = countryLookup;
    }

    /// <inheritdoc />
    public async Task SendAsync(
        string email, string? ipAddress, string? userAgent, string? timeZoneId)
    {
        var deviceInfo = UserAgentParser.Parse(userAgent);
        var timestamp = ReaderTime.Format(DateTimeOffset.UtcNow, timeZoneId);

        // The country stands in the letter for the reader and the address for
        // support: few people know their own address, and none of them knows it
        // on a phone, where it changes with the connection.
        var emailBody = await _renderer.RenderAsync(new SuspiciousLoginViewModel(
            IpAddress: ipAddress,
            DeviceInfo: deviceInfo,
            Timestamp: timestamp,
            Country: _countryLookup.Find(ipAddress)));

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = "Dungeon Master: вход с незнакомого устройства",
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
