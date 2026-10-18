/**
 * @vitest-environment jsdom
 */

/**
 * The answer a settings form gives instead of a toast.
 *
 * Three things have to hold or the button lies: a form nobody has saved yet
 * must not claim to be saved, a saved form must say so, and the claim must
 * fall away the instant the reader changes a field. The third is the one a
 * refactor breaks silently - a snapshot taken by reference rather than by
 * value keeps agreeing with a form that has since been edited, and the button
 * then reads "Сохранено" over unsaved work.
 */
import { describe, expect, it } from "vitest";
import { ref } from "vue";
import { useSavedState } from "./useSavedState";

describe("the saved state of a form", () => {
  it("is not saved before the first save", () => {
    const form = ref({ theme: "Light", perPage: 10 });
    const { saved, label } = useSavedState(() => form.value);

    expect(saved.value).toBe(false);
    expect(label.value).toBe("Сохранить");
  });

  it("is saved once the server has confirmed", () => {
    const form = ref({ theme: "Light", perPage: 10 });
    const { saved, label, remember } = useSavedState(() => form.value);

    remember();

    expect(saved.value).toBe(true);
    expect(label.value).toBe("Сохранено");
  });

  // The one that a by-reference snapshot would keep passing while the button
  // says "Сохранено" over changed fields.
  it("stops being saved the moment a field changes", () => {
    const form = ref({ theme: "Light", perPage: 10 });
    const { saved, label, remember } = useSavedState(() => form.value);
    remember();

    form.value.perPage = 25;

    expect(saved.value).toBe(false);
    expect(label.value).toBe("Сохранить");
  });

  it("is saved again when the reader undoes the change", () => {
    const form = ref({ theme: "Light", perPage: 10 });
    const { saved, remember } = useSavedState(() => form.value);
    remember();
    form.value.perPage = 25;

    form.value.perPage = 10;

    expect(saved.value).toBe(true);
  });

  it("does not mind which field was touched first", () => {
    const form = ref<Record<string, unknown>>({ a: 1 });
    const { saved, remember } = useSavedState(() => form.value);
    form.value.b = 2;
    remember();

    expect(saved.value).toBe(true);
  });
});
