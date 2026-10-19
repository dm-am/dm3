import {
  AttributeSpecificationType,
  type AttributeSchemaDraft,
  type AttributeSpecification,
  type AttributeValueSpecification,
} from "./gameTypes";

export const SPEC_TYPE_LABELS: Record<AttributeSpecificationType, string> = {
  [AttributeSpecificationType.Text]: "Текст",
  [AttributeSpecificationType.Number]: "Число",
  [AttributeSpecificationType.TextList]: "Текстовый список",
  [AttributeSpecificationType.NumberList]: "Числовой список",
  [AttributeSpecificationType.TextNumberList]: "Список с модификаторами",
  [AttributeSpecificationType.BbCode]: "BBCode",
};

export const SPEC_TYPE_OPTIONS = Object.values(AttributeSpecificationType).map(
  (value) => ({ value, label: SPEC_TYPE_LABELS[value] }),
);

/** Types whose constraint is a maximal length (single-value inputs). */
export function usesMaxLength(type: AttributeSpecificationType): boolean {
  return (
    type === AttributeSpecificationType.Text ||
    type === AttributeSpecificationType.Number ||
    type === AttributeSpecificationType.BbCode
  );
}

/** Types whose constraint is a list of possible values. */
export function usesValues(type: AttributeSpecificationType): boolean {
  return (
    type === AttributeSpecificationType.TextList ||
    type === AttributeSpecificationType.NumberList ||
    type === AttributeSpecificationType.TextNumberList
  );
}

/** List types that carry a per-value numeric modifier. */
export function usesModifier(type: AttributeSpecificationType): boolean {
  return (
    type === AttributeSpecificationType.NumberList ||
    type === AttributeSpecificationType.TextNumberList
  );
}

export function isBbCode(type: AttributeSpecificationType): boolean {
  return type === AttributeSpecificationType.BbCode;
}

/** The server hands out the ids again on save. */
function newSpecId(): string {
  return crypto.randomUUID();
}

export function createEmptySchema(): AttributeSchemaDraft {
  return {
    title: "",
    specifications: [],
  };
}

export function createEmptySpec(order: number): AttributeSpecification {
  return {
    id: newSpecId(),
    title: "",
    required: false,
    type: AttributeSpecificationType.Text,
    order,
    isDescriptor: false,
    isHidden: false,
    maxLength: null,
    values: [],
  };
}

function cloneValues(
  values: AttributeValueSpecification[] | null | undefined,
): AttributeValueSpecification[] {
  if (!values) return [];
  return values.map((v) => ({ value: v.value, modifier: v.modifier }));
}

// Ids kept: a replacement keeps the values of every specification whose id it sends back.
export function cloneSpecs(
  specs: AttributeSpecification[],
): AttributeSpecification[] {
  return specs.map((s) => ({ ...s, values: cloneValues(s.values) }));
}

// New ids, so a save never reuses the specifications of the source schema.
export function cloneSpecsWithNewIds(
  specs: AttributeSpecification[],
): AttributeSpecification[] {
  return cloneSpecs(specs).map((s, index) => ({
    ...s,
    id: newSpecId(),
    order: index,
  }));
}

export function cloneSchema(
  schema: AttributeSchemaDraft,
): AttributeSchemaDraft {
  return {
    title: schema.title,
    specifications: cloneSpecs(schema.specifications),
  };
}

/** After a type change only the constraint of the new type stays. */
export function normalizeSpecForType(spec: AttributeSpecification): void {
  if (usesMaxLength(spec.type)) {
    spec.values = [];
    if (spec.maxLength === undefined) spec.maxLength = null;
  } else if (usesValues(spec.type)) {
    spec.maxLength = null;
    if (!spec.values) spec.values = [];
  } else {
    spec.maxLength = null;
    spec.values = [];
  }
  // Descriptor is meaningless for BbCode rows.
  if (isBbCode(spec.type)) spec.isDescriptor = false;
}
