/**
 * The set of possible values is an array in every draft, never null.
 *
 * The server declares the field on a specification and refuses a null with a
 * validation error, so a draft that means "this type has no list" has to say
 * it with an empty array. The stand found this the hard way: a template with a
 * single text attribute could not be saved at all, because the editor put null
 * there and the answer came back as an English framework message about a
 * required field.
 *
 * Held here rather than in a page spec because every writing screen builds its
 * draft through these helpers: the template editor, the game creation form and
 * the sheet section of game settings alike.
 */
import { describe, it, expect } from "vitest";
import {
  createEmptySpec,
  cloneSpecs,
  cloneSpecsWithNewIds,
  normalizeSpecForType,
} from "./schemaHelpers";
import { AttributeSpecificationType } from "@/shared/api/models/game/attributeSchemas";

/** Every type the editor offers, so a new one cannot slip in with a null. */
const EVERY_TYPE = [
  AttributeSpecificationType.Text,
  AttributeSpecificationType.Number,
  AttributeSpecificationType.TextList,
  AttributeSpecificationType.NumberList,
  AttributeSpecificationType.TextNumberList,
  AttributeSpecificationType.BbCode,
];

describe("a draft specification", () => {
  it("carries an empty set of values when it is fresh", () => {
    expect(createEmptySpec(0).values).toEqual([]);
  });

  it("keeps the set of values an array through every change of type", () => {
    for (const type of EVERY_TYPE) {
      const spec = createEmptySpec(0);
      spec.type = type;
      normalizeSpecForType(spec);
      expect(Array.isArray(spec.values)).toBe(true);
    }
  });

  it("hands back an array when copying a stored specification that has none", () => {
    const stored = {
      ...createEmptySpec(0),
      // An older draft, or a hand-written fixture, can still carry a null here:
      // the copy has to take it off.
      values: null as unknown as [],
    };
    expect(cloneSpecs([stored])[0].values).toEqual([]);
    expect(cloneSpecsWithNewIds([stored])[0].values).toEqual([]);
  });
});
