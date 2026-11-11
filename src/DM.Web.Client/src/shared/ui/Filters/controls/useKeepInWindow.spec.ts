/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach } from "vitest";
import { defineComponent, h, nextTick, ref } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";
import { useKeepInWindow, WINDOW_EDGE } from "./useKeepInWindow";

describe("a panel of the bar", () => {
  const ROOM = 375;
  let wrapper: VueWrapper | null = null;

  afterEach(() => {
    wrapper?.unmount();
    wrapper = null;
  });

  /** A panel that the stylesheet has put at [left, left + width). */
  const openAt = async (left: number, width: number) => {
    Object.defineProperty(document.documentElement, "clientWidth", {
      configurable: true,
      get: () => ROOM,
    });
    const panel = ref<HTMLElement | null>(null);
    wrapper = mount(
      defineComponent({
        setup() {
          useKeepInWindow(panel);
          return () => h("div", { ref: panel });
        },
      }),
      { attachTo: document.body },
    );
    await nextTick();
    const el = panel.value!;
    el.getBoundingClientRect = () => {
      // The stylesheet's place, moved by the translate the script sets.
      const shift = Number.parseFloat(el.style.translate || "0") || 0;
      return {
        left: left + shift,
        right: left + width + shift,
        width,
      } as DOMRect;
    };
    window.dispatchEvent(new Event("resize"));
    return el;
  };

  // Read back as a browser serializes it: `Xpx 0` comes back without the zero.
  it("is moved back in from the left edge", async () => {
    // The games list on a phone: the panel hung from a button at the left.
    const el = await openAt(-114, 262);
    expect(el.style.translate).toBe(`${WINDOW_EDGE + 114}px`);
  });

  it("is moved back in from the right edge", async () => {
    const el = await openAt(275, 262);
    expect(el.style.translate).toBe(`${ROOM - WINDOW_EDGE - 275 - 262}px`);
  });

  it("stays where its stylesheet put it when it fits", async () => {
    const el = await openAt(35, 262);
    expect(el.style.translate).toBe("");
  });

  it("stands from the left edge when it is wider than the window", async () => {
    const el = await openAt(-50, 400);
    expect(el.style.translate).toBe(`${WINDOW_EDGE + 50}px`);
  });
});
