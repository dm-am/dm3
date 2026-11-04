import { expect, vi } from "vitest";
import type { VueWrapper } from "@vue/test-utils";
import { defineComponent, h, nextTick } from "vue";
import type { GeneralError } from "@/shared/api/models/common";

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
