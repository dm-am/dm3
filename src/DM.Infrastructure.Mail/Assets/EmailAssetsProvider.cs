using System;
using System.IO;
using System.Reflection;
using DM.Domain.Core.Mail;

namespace DM.Infrastructure.Mail.Assets;

/// <inheritdoc />
internal class EmailAssetsProvider : IEmailAssetsProvider
{
    private readonly Lazy<byte[]> _logoBytes;

    public EmailAssetsProvider()
    {
        _logoBytes = new Lazy<byte[]>(LoadLogo);
    }

    /// <inheritdoc />
    public DM.Domain.Core.Mail.LinkedResource GetLogo() => new()
    {
        ContentId = "logo",
        MimeType = "image/png",
        Content = _logoBytes.Value
    };

    private static byte[] LoadLogo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("logo.png")
            ?? throw new InvalidOperationException("Logo resource not found in assembly");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
