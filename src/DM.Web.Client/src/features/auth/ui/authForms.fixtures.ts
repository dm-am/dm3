import { expect, vi } from "vitest";
import type { VueWrapper } from "@vue/test-utils";
import { defineComponent, h, nextTick, type Component } from "vue";
import type { Router } from "vue-router";
import type { GeneralError } from "@/shared/api/models/common";
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
export const AuthDialogStub = defineComponent({
  name: "AuthDialog",
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

// The spec has pressed into a limit with the interval faked. The modal keeps
// the component mounted, so a running interval would outlive it.
export async function expectWaitDropsOnClose(
  wrapper: Pick<VueWrapper, "find" | "findComponent">,
  stripSelector: string,
) {
  // A real frame of jsdom runs out before the close, the order a loaded runner met.
  await new Promise((resolve) => setTimeout(resolve, 50));
  expect(vi.getTimerCount()).toBe(1);

  wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
  await nextTick();

  expect(vi.getTimerCount()).toBe(0);
  expect(wrapper.find(stripSelector).text()).toBe("");
}
