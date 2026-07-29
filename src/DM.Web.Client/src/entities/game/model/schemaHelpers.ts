// Pure helpers and constants for the attribute-schema editor.
// Kept framework-free so they can be unit-tested and reused across the
// three embed sites (create-game, game schema page, game settings page).

import {
  AttributeSchemaType,
  AttributeSpecificationType,
  type AttributeSchema,
  type AttributeSpecification,
  type AttributeValueSpecification,
} from "@/entities/game";

/** Human-readable labels for the 6 specification types. */
export const SPEC_TYPE_LABELS: Record<AttributeSpecificationType, string> = {
  [AttributeSpecificationType.Text]: "Текст",
  [AttributeSpecificationType.Number]: "Число",
  [AttributeSpecificationType.TextList]: "Текстовый список",
  [AttributeSpecificationType.NumberList]: "Числовой список",
  [AttributeSpecificationType.TextNumberList]: "Список с модификаторами",
  [AttributeSpecificationType.BbCode]: "BBCode",
};

/** Options array for the type <Select>. */
export const SPEC_TYPE_OPTIONS = (
  Object.values(AttributeSpecificationType) as AttributeSpecificationType[]
).map((value) => ({ value, label: SPEC_TYPE_LABELS[value] }));

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

/** Fresh client-side identifier for a draft spec (backend re-assigns on save). */
export function newSpecId(): string {
  return crypto.randomUUID();
}

export function createEmptySchema(): AttributeSchema {
  return {
    id: null,
    title: "",
    author: null,
    type: AttributeSchemaType.Private,
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
    values: null,
  };
}

function cloneValues(
  values: AttributeValueSpecification[] | null | undefined,
): AttributeValueSpecification[] | null {
  if (!values) return null;
  return values.map((v) => ({ value: v.value, modifier: v.modifier }));
}

/**
 * Copy specs from another schema into a fresh draft. Each spec gets a NEW
 * client id and re-sequenced order so saving never re-uses the source
 * schema's spec identifiers (they become a distinct schema).
 */
export function cloneSpecsWithNewIds(
  specs: AttributeSpecification[],
): AttributeSpecification[] {
  return specs.map((s, index) => ({
    ...s,
    id: newSpecId(),
    order: index,
    values: cloneValues(s.values),
  }));
}

/** Deep clone of a whole schema (JSON round-trip is enough for this shape). */
export function cloneSchema(schema: AttributeSchema): AttributeSchema {
  return {
    id: schema.id,
    title: schema.title,
    author: schema.author,
    type: schema.type,
    specifications: schema.specifications.map((s) => ({
      ...s,
      values: cloneValues(s.values),
    })),
  };
}

/**
 * Normalize a spec's per-type fields after a type change: keep only the
 * constraint relevant to the new type, drop the other.
 */
export function normalizeSpecForType(spec: AttributeSpecification): void {
  if (usesMaxLength(spec.type)) {
    spec.values = null;
    if (spec.maxLength === undefined) spec.maxLength = null;
  } else if (usesValues(spec.type)) {
    spec.maxLength = null;
    if (!spec.values) spec.values = [];
  } else {
    spec.maxLength = null;
    spec.values = null;
  }
  // Descriptor is meaningless for BbCode rows.
  if (isBbCode(spec.type)) spec.isDescriptor = false;
}
