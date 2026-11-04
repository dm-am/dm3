/**
 * What a FormField says under its control, found by the caption over it.
 */
import { expect } from "vitest";
import type { VueWrapper } from "@vue/test-utils";

/** The error under the field captioned `label`, or undefined while there is none. */
function fieldErrorOf(wrapper: VueWrapper, label: string): string | undefined {
  const field = wrapper
    .findAll(".form-field")
    .find((f) => f.find(".form-field-label").text() === label);
  const error = field?.find(".form-field-error");
  return error?.exists() ? error.text() : undefined;
}

/**
 * The server's refusal of one field stands under that field and not in the
 * form's line, and goes as soon as the field is typed in again.
 */
export async function expectRefusalUnderField(
  wrapper: VueWrapper,
  label: string,
  sentence: string,
  typeAgain: () => Promise<unknown>,
): Promise<void> {
  expect(fieldErrorOf(wrapper, label)).toBe(sentence);
  expect(wrapper.find(".action-result").exists()).toBe(false);
  await typeAgain();
  expect(fieldErrorOf(wrapper, label)).toBeUndefined();
}
