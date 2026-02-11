using System.Threading.Tasks;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.Blacklist.Deleting;

/// <summary>
/// Service for deleting blacklist links
/// </summary>
public interface IBlacklistDeletingService
{
    /// <summary>
    /// Delete existing blacklist link
    /// </summary>
    /// <param name="operateBlacklistLink">DTO model</param>
    /// <returns></returns>
    Task Delete(OperateBlacklistLink operateBlacklistLink);
}