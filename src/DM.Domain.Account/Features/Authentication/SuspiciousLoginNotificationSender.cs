using System.Threading.Tasks;
using DM.Domain.Account.Features.Mail;
using DM.Domain.Core.Abstractions;
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
    private readonly IDateTimeProvider _dateTimeProvider;

    public SuspiciousLoginNotificationSender(
        ITemplateRenderer renderer,
        IMailSender mailSender,
        IEmailAssetsProvider emailAssetsProvider,
        IIpCountryLookup countryLookup,
        IDateTimeProvider dateTimeProvider)
    {
        _renderer = renderer;
        _mailSender = mailSender;
        _emailAssetsProvider = emailAssetsProvider;
        _countryLookup = countryLookup;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task SendAsync(
        string email, string? ipAddress, string? userAgent, string? timeZoneId)
    {
        var deviceInfo = UserAgentParser.Parse(userAgent);
        var timestamp = ReaderTime.Format(_dateTimeProvider.Now, timeZoneId);

        // The country stands in the letter for the reader and the address for support.
        var emailBody = await _renderer.RenderAsync(new SuspiciousLoginViewModel(
            IpAddress: ipAddress,
            DeviceInfo: deviceInfo,
            Timestamp: timestamp,
            Country: _countryLookup.Find(ipAddress)));

        await _mailSender.SendAsync(new EmailLetter
        {
            Address = email,
            Subject = MailSubject.Of("вход с незнакомого устройства"),
            Body = emailBody,
            LinkedResources = [_emailAssetsProvider.GetLogo()]
        });
    }
}
