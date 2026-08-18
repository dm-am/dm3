/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { defineComponent, h, type Ref } from "vue";
import { mount } from "@vue/test-utils";
import { useNowTimestamp } from "./useNowTimestamp";

/** Host component: the composable releases its subscription in onUnmounted. */
function mountSubscriber() {
  let now!: Ref<number>;
  const wrapper = mount(
    defineComponent({
      setup() {
        now = useNowTimestamp();
        return () => h("div");
      },
    }),
  );
  return { wrapper, now };
}

describe("useNowTimestamp", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("refreshes the timestamp once per minute while a subscriber is mounted", () => {
    const { wrapper, now } = mountSubscriber();

    vi.advanceTimersByTime(60_000);
    expect(now.value).toBe(Date.now());

    // A partial minute must not tick — one Date per minute is the contract
    const afterFirstTick = now.value;
    vi.advanceTimersByTime(59_999);
    expect(now.value).toBe(afterFirstTick);

    vi.advanceTimersByTime(1);
    expect(now.value).toBe(Date.now());

    wrapper.unmount();
  });

  it("shares one singleton ref and keeps the timer alive until the last subscriber unmounts", () => {
    const first = mountSubscriber();
    const second = mountSubscriber();

    expect(second.now).toBe(first.now);

    // One subscriber leaving must not stop the shared timer
    first.wrapper.unmount();
    vi.advanceTimersByTime(60_000);
    expect(second.now.value).toBe(Date.now());

    // The last one leaving must: a stopped timer leaves the value frozen
    second.wrapper.unmount();
    const frozen = second.now.value;
    vi.advanceTimersByTime(180_000);
    expect(second.now.value).toBe(frozen);
  });

  it("restarts the timer for a subscriber mounted after a full stop", () => {
    const first = mountSubscriber();
    first.wrapper.unmount();

    const { wrapper, now } = mountSubscriber();
    vi.advanceTimersByTime(60_000);
    expect(now.value).toBe(Date.now());

    wrapper.unmount();
  });
});
