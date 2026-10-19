import {
  AttributeSpecificationType,
  type AttributeSpecification,
  type Character,
} from "@/entities/game";

type SpecSeed = Pick<
  AttributeSpecification,
  "id" | "title" | "type" | "order"
> &
  Partial<AttributeSpecification>;

export function makeSpec(seed: SpecSeed): AttributeSpecification {
  return {
    required: false,
    isDescriptor: false,
    isHidden: false,
    maxLength: null,
    values: [],
    ...seed,
  };
}

export function makeSchema(specs?: AttributeSpecification[]): {
  specifications: AttributeSpecification[];
} {
  return {
    specifications: specs ?? [
      makeSpec({
        id: "text-spec",
        title: "Род занятий",
        type: AttributeSpecificationType.Text,
        order: 1,
        required: true,
        maxLength: 100,
      }),
      makeSpec({
        id: "number-spec",
        title: "Возраст",
        type: AttributeSpecificationType.Number,
        order: 2,
        maxLength: 3,
      }),
      makeSpec({
        id: "textlist-spec",
        title: "Класс",
        type: AttributeSpecificationType.TextList,
        order: 3,
        values: [
          { value: "Воин", modifier: null },
          { value: "Маг", modifier: null },
        ],
      }),
      makeSpec({
        id: "numberlist-spec",
        title: "Сила",
        type: AttributeSpecificationType.NumberList,
        order: 4,
        values: [
          { value: "10", modifier: null },
          { value: "12", modifier: null },
        ],
      }),
      makeSpec({
        id: "textnumberlist-spec",
        title: "Оружие",
        type: AttributeSpecificationType.TextNumberList,
        order: 5,
        values: [
          { value: "Меч", modifier: 2 },
          { value: "Лук", modifier: -1 },
        ],
      }),
      makeSpec({
        id: "bbcode-spec",
        title: "Биография",
        type: AttributeSpecificationType.BbCode,
        order: 6,
        maxLength: 5000,
      }),
    ],
  };
}

export function makeCharacter(
  attributes: Record<string, unknown>[],
): Character {
  return {
    id: "character-1",
    status: "Active",
    name: "Арагорн",
    isNpc: false,
    privacy: { isNpc: false, editByMaster: false, editPostByMaster: false },
    attributes,
    totalPostsCount: 0,
  } as unknown as Character;
}
