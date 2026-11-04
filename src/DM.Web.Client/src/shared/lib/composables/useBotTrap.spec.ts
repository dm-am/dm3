/**
 * @vitest-environment jsdom
 */

/**
 * The least time a person takes over a guest form, sat out on the submit, a
 * parameter of each form; and the honeypot field, sent as `website`.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import HoneypotField from "@/shared/ui/Form/HoneypotField.vue";
import { useBotTrap } from "./useBotTrap";

enableAutoUnmount(afterEach);

/** The trap of a form that went on screen just now, with its hidden field. */
function trapOf(minFillMs: number) {
  let trap: ReturnType<typeof useBotTrap> | undefined;
  const form = mount(
    defineComponent({
      setup() {
        const own = useBotTrap({ minFillMs });
        trap = own;
        return () =>
          h("form", [
            h(HoneypotField, {
              modelValue: own.honeypot.value,
              "onUpdate:modelValue": (value: string) => {
                own.honeypot.value = value;
              },
            }),
          ]);
      },
    }),
  );
  return { ...trap!, form };
}

/** Whether the wait of a submit is over yet, read without awaiting it. */
function watchWait(wait: Promise<void>) {
  const state = { over: false };
  void wait.then(() => {
    state.over = true;
  });
  return state;
}

describe("useBotTrap", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("sits out the rest of the minimum fill time when the form is sent early", async () => {
    const trap = trapOf(3000);
    vi.advanceTimersByTime(1000);

    const wait = watchWait(trap.sitOut());

    await vi.advanceTimersByTimeAsync(1999);
    expect(wait.over).toBe(false);

    await vi.advanceTimersByTimeAsync(1);
    expect(wait.over).toBe(true);
  });

  it("does not wait once the minimum fill time has passed", async () => {
    const trap = trapOf(2000);
    vi.advanceTimersByTime(2000);

    const wait = watchWait(trap.sitOut());

    await vi.advanceTimersByTimeAsync(0);
    expect(wait.over).toBe(true);
  });

  it("takes what a bot types into the hidden field, which no person reaches", async () => {
    const { form, honeypot } = trapOf(0);
    const field = form.get('input[name="website"]');

    expect(field.attributes("tabindex")).toBe("-1");
    expect(field.attributes("aria-hidden")).toBe("true");
    expect(field.classes()).toContain("honeypot-field");

    await field.setValue("https://spam.example");

    expect(honeypot.value).toBe("https://spam.example");
  });
});
