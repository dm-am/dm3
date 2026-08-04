/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { MESSAGE_LAYOUTS, useUiStore } from "./ui";

const MESSAGE_LAYOUT_KEY = "dm_message_layout";

describe("useUiStore message layout", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
  });

  // Driven by the array rather than by a written-out list: a layout the store
  // offers but refuses to read back drops the choice the user already made,
  // and it fails silently, so only a test that enumerates the set catches it.
  for (const layout of MESSAGE_LAYOUTS) {
    it(`restores "${layout}" saved by a previous visit`, () => {
      localStorage.setItem(MESSAGE_LAYOUT_KEY, layout);

      expect(useUiStore().messageLayout).toBe(layout);
    });
  }

  it("falls back to full when the stored value is not a layout", () => {
    localStorage.setItem(MESSAGE_LAYOUT_KEY, "cozy");

    expect(useUiStore().messageLayout).toBe("full");
  });
});
