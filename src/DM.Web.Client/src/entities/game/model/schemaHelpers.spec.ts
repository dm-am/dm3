// The server refuses a null set of values, so every draft holds an array, through every helper.
import { describe, it, expect } from "vitest";
import {
  createEmptySpec,
  cloneSpecs,
  cloneSpecsWithNewIds,
  normalizeSpecForType,
} from "./schemaHelpers";
import { AttributeSpecificationType } from "@/shared/api/models/game/attributeSchemas";

/** Every type the editor offers, so a new one cannot slip in with a null. */
const EVERY_TYPE = Object.values(AttributeSpecificationType);

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
