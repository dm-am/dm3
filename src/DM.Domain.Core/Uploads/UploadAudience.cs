using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Who the uploader chose to see a file: everybody who reads where it hangs, or
/// the characters named.
/// </summary>
/// <param name="VisibleTo">"Все" or "Выбранные".</param>
/// <param name="CharacterIds">The characters chosen; read only for <see cref="UploadVisibleTo.Selected"/>.</param>
public sealed record UploadAudience(UploadVisibleTo VisibleTo, IReadOnlyCollection<Guid> CharacterIds)
{
    /// <summary>Everybody, the default of every file.</summary>
    public static UploadAudience All { get; } = new(UploadVisibleTo.All, []);
}
