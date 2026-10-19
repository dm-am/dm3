/**
 * The states of a list read once when it mounts (UI_STANDARDS "Состояния
 * данных"): a placeholder and no words until the answer, a failed first read
 * said in the list's place with "Повторить", and the empty line only once an
 * answer came. The spec hands a mount whose first read answers as told, and
 * whose next one serves an empty list.
 */
import { expect } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";

export interface FirstReadCopy {
  /** The selector of the placeholder that stands for the list. */
  placeholder: string;
  /** What the list's place says when the first read fails. */
  failed: string;
  /** The line of an empty list. */
  empty: string;
}

export async function expectFirstReadStates(
  mountWith: (first: Promise<unknown>) => Promise<VueWrapper>,
  refused: unknown,
  copy: FirstReadCopy,
): Promise<void> {
  const waiting = await mountWith(new Promise(() => {}));
  expect(waiting.find(copy.placeholder).exists()).toBe(true);
  expect(waiting.text()).not.toContain(copy.empty);
  waiting.unmount();

  const failing = await mountWith(Promise.resolve(refused));
  expect(failing.get(".error-state").text()).toContain(copy.failed);
  expect(failing.find(copy.placeholder).exists()).toBe(false);
  expect(failing.text()).not.toContain(copy.empty);

  await failing.get(".error-retry").trigger("click");
  await flushPromises();
  expect(failing.find(".error-state").exists()).toBe(false);
  expect(failing.text()).toContain(copy.empty);
  failing.unmount();
}
