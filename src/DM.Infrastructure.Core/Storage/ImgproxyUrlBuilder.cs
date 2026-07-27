using System;
using System.Security.Cryptography;
using System.Text;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Uploads;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class ImgproxyUrlBuilder : IImgproxyUrlBuilder
{
    private readonly ImageProxyConfiguration _config;
    private readonly byte[] _keyBytes;
    private readonly byte[] _saltBytes;

    /// <inheritdoc />
    public ImgproxyUrlBuilder(IOptions<ImageProxyConfiguration> config)
    {
        _config = config.Value;
        _keyBytes = string.IsNullOrEmpty(_config.Key)
            ? Array.Empty<byte>()
            : Convert.FromHexString(_config.Key);
        _saltBytes = string.IsNullOrEmpty(_config.Salt)
            ? Array.Empty<byte>()
            : Convert.FromHexString(_config.Salt);
    }

    /// <inheritdoc />
    public string BuildSquareThumbnail(string sourceObjectKey, int size)
    {
        if (string.IsNullOrEmpty(sourceObjectKey))
        {
            return string.Empty;
        }

        // imgproxy processing options:
        //   rs:fill:{w}:{h}:1 — resize to W×H in fill mode (crop overflow, no padding).
        //                        Parameter 1 = "extend if smaller" (small sources are upscaled).
        //   g:sm              — gravity smart (detects the important part of the image automatically).
        //   q:85              — quality 85 (size/visual balance for thumbnails).
        // Format negotiation — automatic via IMGPROXY_AUTO_WEBP/AVIF + Accept header.
        var options = $"rs:fill:{size}:{size}:1/g:sm/q:85";
        var sourceUrl = _config.SourceUrlPrefix + sourceObjectKey;
        var encodedSource = Base64UrlEncode(Encoding.UTF8.GetBytes(sourceUrl));
        var path = $"/{options}/{encodedSource}";
        var signature = Sign(path);
        return $"{_config.Endpoint.TrimEnd('/')}/{signature}{path}";
    }

    private string Sign(string path)
    {
        // imgproxy URL signature = HMAC-SHA256(key, salt || path_bytes), base64url no-pad.
        // If key/salt are empty (dev without signing), then
        // IMGPROXY_ALLOW_INSECURE_URLS=true must be enabled and the URL starts with /insecure/.
        if (_keyBytes.Length == 0 || _saltBytes.Length == 0)
        {
            return "insecure";
        }

        var pathBytes = Encoding.UTF8.GetBytes(path);
        var message = new byte[_saltBytes.Length + pathBytes.Length];
        Buffer.BlockCopy(_saltBytes, 0, message, 0, _saltBytes.Length);
        Buffer.BlockCopy(pathBytes, 0, message, _saltBytes.Length, pathBytes.Length);

        using var hmac = new HMACSHA256(_keyBytes);
        var hash = hmac.ComputeHash(message);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
