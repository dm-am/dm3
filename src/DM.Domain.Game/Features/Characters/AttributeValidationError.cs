using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Extensions;

namespace DM.Domain.Game.Features.Characters;

public static class AttributeValidationError
{
    public static string InvalidSpecification => "Такого атрибута нет в схеме игры";

    public static string RequiredMissing => "Атрибут обязателен";

    public static string ManyRequiredMissing(IEnumerable<(Guid Id, string Title)> specifications) =>
        $"Не заполнены обязательные атрибуты: {string.Join(", ", specifications.Select(s => $"{s.Id.EncodeToReadable()} ({s.Title})"))}";

    public static string ManyDuplicated(IEnumerable<Guid> specifications) =>
        $"Атрибуты переданы больше одного раза: {string.Join(", ", specifications.Select(s => s.EncodeToReadable()))}";

    public static string NotANumber => "Нужно число";

    public static string StringTooLong(int maxLength) => $"Не длиннее {maxLength} символов";

    public static string NotPresentInList(IEnumerable<string> possibleValues) =>
        $"Выберите одно из значений: {string.Join(", ", possibleValues)}";
}
