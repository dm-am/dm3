using System;
using System.IO;
using System.Threading.Tasks;

namespace DM.Infrastructure.Core.Storage;

/// <summary>
/// CDN uploader
/// </summary>
internal interface IUploader
{
    /// <summary>
    /// Upload file
    /// </summary>
    /// <param name="streamAccessor"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    Task<string> Upload(Func<Stream> streamAccessor, string fileName);
}
