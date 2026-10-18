namespace DM.Domain.Core.Configuration;

/// <summary>
/// Type of S3 service provider
/// </summary>
public enum S3Provider
{
    /// <summary>
    /// Basic AWS S3
    /// </summary>
    Amazon = 0,

    /// <summary>
    /// Heroku CloudCube
    /// </summary>
    CloudCube = 1,

    /// <summary>
    /// An S3-compatible store run beside the site, reached by path-style addresses
    /// </summary>
    SelfHosted = 2
}
