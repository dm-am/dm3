/**
 * The height pin of a flip is let go on a timer when no transition event
 * arrives (reduced motion, jsdom), and the timer can outlive the component:
 * the page is left before it fires.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { defineComponent, h, ref } from "vue";
import { mount } from "@vue/test-utils";
import { useAnimatedHeightToggle } from "./useAnimatedHeightToggle";

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

/** A zone on the page whose content the toggle flips in and out. */
function mountZone() {
  let zone!: ReturnType<typeof useAnimatedHeightToggle>;
  const Zone = defineComponent({
    setup() {
      const el = ref<HTMLElement | null>(null);
      const open = ref(false);
      zone = useAnimatedHeightToggle(el, (next) => (open.value = next));
      return () => h("div", { ref: el }, open.value ? [h("p")] : []);
    },
  });
  const wrapper = mount(Zone, { attachTo: document.body });
  return { wrapper, zone };
}

describe("useAnimatedHeightToggle", () => {
  it("lets the pin go when the transition would have ended", async () => {
    vi.useFakeTimers();
    const { zone } = mountZone();

    await zone.toggle(true);
    expect(zone.pinnedHeight.value).not.toBeNull();

    vi.advanceTimersByTime(150);
    expect(zone.pinnedHeight.value).toBeNull();
  });

  it("leaves no timer behind once the component is gone", async () => {
    vi.useFakeTimers();
    const { wrapper, zone } = mountZone();

    await zone.toggle(true);
    expect(vi.getTimerCount()).toBe(1);
    wrapper.unmount();

    expect(vi.getTimerCount()).toBe(0);
  });

  it("starts no timer when the component goes before the flip is drawn", async () => {
    vi.useFakeTimers();
    const { wrapper, zone } = mountZone();

    const flip = zone.toggle(true);
    wrapper.unmount();
    await flip;

    expect(vi.getTimerCount()).toBe(0);
  });
});
