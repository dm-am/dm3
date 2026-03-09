using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace DM.Domain.Core.Parsing;

/// <summary>
/// Parses User-Agent strings to extract device/browser information
/// </summary>
public static partial class UserAgentParser
{
    /// <summary>
    /// Parses a User-Agent string into a human-readable device description
    /// </summary>
    /// <param name="userAgent">The User-Agent header value</param>
    /// <returns>Human-readable device description</returns>
    public static string Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "Неизвестное устройство";
        }

        var browser = DetectBrowser(userAgent);
        var os = DetectOS(userAgent);

        if (browser == null && os == null)
        {
            return "Неизвестное устройство";
        }

        return string.Join(" на ", new[] { browser, os }.Where(x => x != null));
    }

    private static string? DetectBrowser(string userAgent)
    {
        // Order matters: more specific patterns first
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
            return "Edge";
        if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            return "Opera";
        if (userAgent.Contains("YaBrowser", StringComparison.OrdinalIgnoreCase))
            return "Яндекс.Браузер";
        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) &&
            !userAgent.Contains("Chromium", StringComparison.OrdinalIgnoreCase))
            return "Chrome";
        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
            return "Firefox";
        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) &&
            !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            return "Safari";
        if (userAgent.Contains("MSIE", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Trident/", StringComparison.OrdinalIgnoreCase))
            return "Internet Explorer";

        return null;
    }

    private static string? DetectOS(string userAgent)
    {
        // Mobile detection first
        if (IPhoneRegex().IsMatch(userAgent))
            return "iPhone";
        if (IPadRegex().IsMatch(userAgent))
            return "iPad";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            return DetectAndroidDevice(userAgent);

        // Desktop OS
        if (userAgent.Contains("Windows NT 10", StringComparison.OrdinalIgnoreCase))
            return "Windows";
        if (userAgent.Contains("Windows NT", StringComparison.OrdinalIgnoreCase))
            return "Windows";
        if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
            return "macOS";
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            return "Linux";

        return null;
    }

    private static string DetectAndroidDevice(string userAgent)
    {
        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
            return "Android";
        return "Android-планшет";
    }

    [GeneratedRegex(@"iPhone", RegexOptions.IgnoreCase)]
    private static partial Regex IPhoneRegex();

    [GeneratedRegex(@"iPad", RegexOptions.IgnoreCase)]
    private static partial Regex IPadRegex();
}
