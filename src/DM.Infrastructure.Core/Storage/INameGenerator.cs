using System.Threading.Tasks;
using DM.Domain.Core.Uploads;

namespace DM.Infrastructure.Core.Storage;

/// <summary>
/// Upload name generator
/// </summary>
internal interface INameGenerator
{
    /// <summary>
    /// Generate upload name and extension
    /// </summary>
    /// <param name="createUpload"></param>
    /// <returns></returns>
    Task<(string name, string extension)> Generate(CreateUpload createUpload);
}
