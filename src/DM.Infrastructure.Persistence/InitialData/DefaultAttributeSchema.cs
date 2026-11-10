using System;
using System.Collections.Generic;
using System.Linq;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;

namespace DM.Infrastructure.Persistence.InitialData;

/// <summary>
/// The character sheet the site ships with: the template the catalogue always
/// holds, and the sheet the development seed gives every game it creates.
/// </summary>
/// <remarks>
/// Initial data of the schema, so a site built by the migration alone has a sheet to
/// start a game from. Every identifier is pinned: the migration carries them as
/// literals and seeded characters address the specifications by them.
/// </remarks>
public static class DefaultAttributeSchema
{
    /// <summary>The template row</summary>
    public static readonly Guid TemplateId = new("b1a5c0de-0000-4000-8000-000000000001");

    /// <summary>The name the catalogue lists it under</summary>
    public const string Title = "Классическая схема";

    /// <summary>"Мировоззрение", a list of <see cref="AlignmentNames" /></summary>
    public static readonly Guid AlignmentId = new("b1a5c0de-0000-4000-8000-000000000103");

    /// <summary>"Внешность"</summary>
    public static readonly Guid AppearanceId = new("b1a5c0de-0000-4000-8000-000000000104");

    /// <summary>"Характер"</summary>
    public static readonly Guid TemperId = new("b1a5c0de-0000-4000-8000-000000000105");

    /// <summary>"История"</summary>
    public static readonly Guid StoryId = new("b1a5c0de-0000-4000-8000-000000000106");

    /// <summary>"Навыки"</summary>
    public static readonly Guid SkillsId = new("b1a5c0de-0000-4000-8000-000000000107");

    /// <summary>"Инвентарь"</summary>
    public static readonly Guid InventoryId = new("b1a5c0de-0000-4000-8000-000000000108");

    private static readonly Guid HitPointsId = new("b1a5c0de-0000-4000-8000-000000000109");
    private static readonly Guid StrengthId = new("b1a5c0de-0000-4000-8000-00000000010a");
    private static readonly Guid DexterityId = new("b1a5c0de-0000-4000-8000-00000000010b");
    private static readonly Guid EnduranceId = new("b1a5c0de-0000-4000-8000-00000000010c");
    private static readonly Guid IntelligenceId = new("b1a5c0de-0000-4000-8000-00000000010d");
    private static readonly Guid WisdomId = new("b1a5c0de-0000-4000-8000-00000000010e");
    private static readonly Guid CharismaId = new("b1a5c0de-0000-4000-8000-00000000010f");

    // A literal, because the migration carries the row as literals and a clock read would make
    // every build of the model differ from it.
    private static readonly DateTimeOffset Shipped = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The nine D&amp;D alignments, the options of "Мировоззрение" and the
    /// stored value of a character's alignment. The classic law/chaos by
    /// good/evil grid, in that order.
    /// </summary>
    public static IReadOnlyList<string> AlignmentNames { get; } =
    [
        "Законопослушный добрый",
        "Нейтральный добрый",
        "Хаотичный добрый",
        "Законопослушный нейтральный",
        "Нейтральный",
        "Хаотичный нейтральный",
        "Законопослушный злой",
        "Нейтральный злой",
        "Хаотичный злой"
    ];

    private static readonly (string Value, int Modifier)[] AbilityRungs =
    [
        ("очень плохо", -20),
        ("плохо", -10),
        ("средне", 0),
        ("хорошо", 10),
        ("очень хорошо", 20)
    ];

    private static readonly (Guid Id, string Title)[] Abilities =
    [
        (StrengthId, "Сила"),
        (DexterityId, "Ловкость"),
        (EnduranceId, "Выносливость"),
        (IntelligenceId, "Интеллект"),
        (WisdomId, "Мудрость"),
        (CharismaId, "Обаяние")
    ];

    private static readonly (Guid Id, string Title)[] Narrative =
    [
        (AppearanceId, "Внешность"),
        (TemperId, "Характер"),
        (StoryId, "История"),
        (SkillsId, "Навыки"),
        (InventoryId, "Инвентарь")
    ];

    private const int BbCodeFieldLength = 20000;

    /// <summary>
    /// The sheet: hit points, the alignment descriptor, the abilities as a word
    /// with a modifier, and the formatted-text fields. Nothing is required and
    /// nothing is hidden - a master decides both in their own copy.
    /// </summary>
    /// <remarks>
    /// A new list on every call: a game's sheet is its own document, and a copy
    /// shared between rows would carry one game's edit into the others.
    /// </remarks>
    public static List<AttributeSpecification> Specifications()
    {
        var specifications = new List<AttributeSpecification>
        {
            new()
            {
                Id = HitPointsId, Title = "HP", Order = 0,
                Constraints = new NumberAttributeConstraints()
            },
            new()
            {
                Id = AlignmentId, Title = "Мировоззрение", Order = 1, IsDescriptor = true,
                Constraints = new ListAttributeConstraints
                {
                    Kind = ListValueKind.Text,
                    Values = AlignmentNames
                        .Select(a => new ListAttributeValue { Value = a })
                        .ToList()
                }
            }
        };

        var abilitiesFrom = specifications.Count;
        specifications.AddRange(Abilities.Select((ability, index) => new AttributeSpecification
        {
            Id = ability.Id,
            Title = ability.Title,
            Order = abilitiesFrom + index,
            Constraints = new ListAttributeConstraints
            {
                Kind = ListValueKind.TextNumber,
                Values = AbilityRungs
                    .Select(rung => new ListAttributeValue { Value = rung.Value, Modifier = rung.Modifier })
                    .ToList()
            }
        }));

        var narrativeFrom = specifications.Count;
        specifications.AddRange(Narrative.Select((field, index) => new AttributeSpecification
        {
            Id = field.Id,
            Title = field.Title,
            Order = narrativeFrom + index,
            Constraints = new BbCodeAttributeConstraints { MaxLength = BbCodeFieldLength }
        }));

        return specifications;
    }

    /// <summary>The template row as the schema ships it</summary>
    public static AttributeSchemaTemplate Template() => new()
    {
        TemplateId = TemplateId,
        Title = Title,
        Specifications = Specifications(),
        IsDefault = true,
        CreatedUtc = Shipped,
        UpdatedUtc = Shipped
    };
}
