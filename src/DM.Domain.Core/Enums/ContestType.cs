namespace DM.Domain.Core.Enums;

/// <summary>
/// Тип конкурса. Каждый тип имеет свою сквозную нумерацию (Literary #1-N,
/// Art #1-M и т.д.). Расширяется по мере добавления новых типов конкурсов.
/// </summary>
public enum ContestType
{
    /// <summary>Литературный конкурс.</summary>
    Literary = 0,

    /// <summary>Арт-конкурс (для будущих визуальных конкурсов).</summary>
    Art = 1,
}
