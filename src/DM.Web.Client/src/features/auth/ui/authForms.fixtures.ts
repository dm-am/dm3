import { expect, vi, type MockInstance } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { defineComponent, h, nextTick, type Component } from "vue";
import type { Router } from "vue-router";
import type { GeneralError } from "@/shared/api/models/common";
import { SERVER_ERROR } from "@/shared/lib/errors/generalRefusal";
import {
  mountPage,
  type Address,
  type MountOptions,
} from "@/shared/lib/testing/mountPage";

// The page a guest form opens over, and the support page its way out leads to.
const routes: Address[] = [
  ["home", "/"],
  ["support", "/support"],
];

let router: Router | undefined;

/** A guest form over the home page, on the real router. */
export async function mountAuthForm(
  form: Component,
  options: Pick<MountOptions, "props" | "stubs" | "attachTo"> = {},
) {
  const mounted = await mountPage(form, "/", { routes, ...options });
  router = mounted.router;
  return mounted.wrapper;
}

/** Where the form mounted last has taken the reader. */
export const address = () => router?.currentRoute.value.fullPath;

export const problem = (
  status: number,
  title = "",
  retryAfterSeconds?: number,
): GeneralError => ({
  type: "",
  title,
  status,
  traceId: "trace",
  ...(retryAfterSeconds === undefined ? {} : { retryAfterSeconds }),
});

// The dialog frame is a vue-final-modal wrapper; the card inside it is the subject.
// `busy` is declared so a spec can read whether the form locks its window.
export const AuthDialogStub = defineComponent({
  name: "AuthDialog",
  props: { busy: Boolean },
  setup:
    (_props, { slots }) =>
    () =>
      h("div", slots.default?.()),
});

// The spec has pressed into a 3-second limit with the interval faked.
export async function expectWaitRunsDown(
  wrapper: Pick<VueWrapper, "find">,
  stripSelector: string,
) {
  const strip = wrapper.find(stripSelector);
  const shut = () => wrapper.find("button.auth-button").attributes("disabled");

  expect(strip.text()).toBe(
    "Слишком много попыток, попробуйте через 3 секунды",
  );
  expect(strip.classes()).toContain("auth-message--error");
  expect(shut()).toBe("");

  vi.advanceTimersByTime(1000);
  await nextTick();
  expect(strip.text()).toBe(
    "Слишком много попыток, попробуйте через 2 секунды",
  );

  vi.advanceTimersByTime(2000);
  await nextTick();
  expect(strip.text()).toBe("");
  expect(shut()).toBeUndefined();
}

// The spec has pressed into a limit with the interval faked. The stub dialog
// keeps the form mounted after beforeClose, so only the form's own handler, not
// its unmount, can have stopped the interval.
export async function expectWaitDropsOnClose(
  wrapper: Pick<VueWrapper, "find" | "findComponent">,
  stripSelector: string,
) {
  expect(vi.getTimerCount()).toBe(1);

  wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
  await nextTick();

  expect(vi.getTimerCount()).toBe(0);
  expect(wrapper.find(stripSelector).text()).toBe("");
}

/**
 * An address form's button opens on anything typed, and the press on a broken
 * address names the format under the field and sends nothing.
 */
export async function expectFormatNamedOnPress(
  wrapper: Pick<VueWrapper, "find" | "text">,
  field: string,
  send: MockInstance,
) {
  const button = () => wrapper.find("button[type='submit']");

  expect(button().attributes("disabled")).toBeDefined();

  await wrapper.find(field).setValue("reader@");
  expect(button().attributes("disabled")).toBeUndefined();

  await wrapper.find("form").trigger("submit");
  await flushPromises();

  expect(send).not.toHaveBeenCalled();
  expect(wrapper.text()).not.toContain("Проверьте почту");
  const strip = wrapper.find(".auth-message");
  expect(strip.text()).toBe("Неверный формат почты");
  expect(strip.classes()).toContain("auth-message--error");
}

/**
 * The form's window is locked from the press until the server answers, then
 * open again: a close in between would lose the answer (UI_STANDARDS/dialogs.md).
 * `request` is the call the press makes; it is held here until the press is done.
 */
export async function expectWindowLockedWhile(
  request: MockInstance,
  busy: () => unknown,
  press: () => Promise<unknown>,
) {
  let answer = (_value: unknown) => {};
  request.mockReturnValue(new Promise((resolve) => (answer = resolve)));
  expect(busy()).toBe(false);

  await press();
  expect(busy()).toBe(true);

  answer({ data: null, error: problem(500, SERVER_ERROR) });
  await flushPromises();
  expect(busy()).toBe(false);
}
