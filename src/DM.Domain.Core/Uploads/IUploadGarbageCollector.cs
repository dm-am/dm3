using System;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Удаляет obsolete uploads (старые аватары после замены, soft-deleted
/// записи) — снимает orphan-объекты с S3 и hard-deletes DB-записи.
///
/// Вызывается из <c>UserService.UpdateAsync</c> при PATCH avatarUploadId
/// (синхронная чистка предыдущих аватаров пользователя), а также из
/// фонового worker'а раз в N часов для подметания uploads, помеченных
/// IsRemoved=true вручную или другими сервисами.
/// </summary>
public interface IUploadGarbageCollector
{
    /// <summary>
    /// Обработать uploads сущности после замены: оставить только самый
    /// свежий (по CreatedUtc), остальные пометить IsRemoved и удалить
    /// связанные S3-объекты (FilePath / MediumFilePath / SmallFilePath).
    /// </summary>
    /// <param name="entityId">Идентификатор сущности (User/Character).</param>
    Task CollectObsoleteAsync(Guid entityId);
}
