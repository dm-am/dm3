using System;
using System.IO;
using System.Threading.Tasks;

namespace DM.Infrastructure.Core.Storage;

/// <summary>
/// CDN uploader
/// </summary>
public interface IUploader
{
    /// <summary>
    /// Upload file
    /// </summary>
    /// <param name="streamAccessor">File stream accessor</param>
    /// <param name="fileName">File name</param>
    /// <returns>Upload URL</returns>
    Task<string> Upload(Func<Stream> streamAccessor, string fileName);
}
