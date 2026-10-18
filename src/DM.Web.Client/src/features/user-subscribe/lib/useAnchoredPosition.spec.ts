/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { defineComponent, nextTick, ref } from "vue";
import { useAnchoredPosition } from "./useAnchoredPosition";

/**
 * Where a panel dropped from an anchor stands, in numbers. jsdom lays out
 * nothing: the anchor's rectangle, the panel's size and the window are given
 * by hand. The anchor is 38 high and 120 wide, the panel 240 by 180.
 */
const html = document.documentElement;
const restores: Array<() => void> = [];
const box = { left: 0, top: 0 };

function stub<T extends object>(target: T, name: string, value: object) {
  const own = Object.getOwnPropertyDescriptor(target, name);
  Object.defineProperty(target, name, { configurable: true, ...value });
  restores.push(() =>
    own
      ? Object.defineProperty(target, name, own)
      : delete (target as Record<string, unknown>)[name],
  );
}

function windowOf(width: number, height: number) {
  stub(html, "clientWidth", { get: () => width });
  stub(html, "clientHeight", { get: () => height });
}

function layOut() {
  const proto = HTMLElement.prototype;
  const rect = Object.getOwnPropertyDescriptor(
    Element.prototype,
    "getBoundingClientRect",
  )!;
  stub(proto, "getBoundingClientRect", {
    value(this: HTMLElement) {
      if (!this.classList.contains("anchor")) return rect.value.call(this);
      return {
        left: box.left,
        top: box.top,
        right: box.left + 120,
        bottom: box.top + 38,
        width: 120,
        height: 38,
      };
    },
  });
  const panelSize = { offsetWidth: 240, offsetHeight: 180 };
  for (const [name, size] of Object.entries(panelSize)) {
    const own = Object.getOwnPropertyDescriptor(proto, name)!;
    stub(proto, name, {
      get(this: HTMLElement) {
        return this.classList.contains("panel") ? size : own.get!.call(this);
      },
    });
  }
}

afterEach(() => {
  while (restores.length) restores.pop()!();
  document.body.innerHTML = "";
});

const Host = defineComponent({
  setup() {
    const anchor = ref<HTMLElement | null>(null);
    const panel = ref<HTMLElement | null>(null);
    const { style } = useAnchoredPosition(() => anchor.value, panel);
    return { anchor, panel, style };
  },
  template: `
    <div>
      <button ref="anchor" class="anchor">Подписка</button>
      <div ref="panel" class="panel" :style="style" />
    </div>`,
});

async function panelAt(window: [number, number], anchor: [number, number]) {
  windowOf(...window);
  layOut();
  [box.left, box.top] = anchor;
  const wrapper = mount(Host, { attachTo: document.body });
  await nextTick();
  return wrapper;
}

const where = (wrapper: Awaited<ReturnType<typeof panelAt>>) => {
  const { top, left } = (wrapper.get(".panel").element as HTMLElement).style;
  return { top, left };
};

describe("useAnchoredPosition", () => {
  // The gap is POPOVER_GAP (2), the edge VIEWPORT_EDGE (8).
  it.each<[string, [number, number], [number, number], object]>([
    // 50 + 38 + 2
    [
      "under the anchor, left edges together",
      [1366, 800],
      [100, 50],
      { top: "90px", left: "100px" },
    ],
    // 300 - 2 - 180
    [
      "above the anchor where the window has no room below",
      [1366, 400],
      [100, 300],
      { top: "118px", left: "100px" },
    ],
    // 300 - 8 - 180
    [
      "against the bottom edge where there is room neither below nor above",
      [1366, 300],
      [100, 120],
      { top: "112px", left: "100px" },
    ],
    [
      "pushed back inside the window at its left edge",
      [1366, 800],
      [-20, 50],
      { top: "90px", left: "8px" },
    ],
    // 375 - 8 - 240
    [
      "pushed back inside the window at its right edge",
      [375, 800],
      [300, 50],
      { top: "90px", left: "127px" },
    ],
  ])("stands %s", async (_case, window, anchor, at) => {
    const wrapper = await panelAt(window, anchor);

    expect(where(wrapper)).toEqual(at);
    wrapper.unmount();
  });

  it("follows the anchor on a scroll and on a resize", async () => {
    const wrapper = await panelAt([1366, 800], [100, 50]);

    box.left = 40;
    document.dispatchEvent(new Event("scroll"));
    await nextTick();
    expect(where(wrapper).left).toBe("40px");

    box.top = 10;
    window.dispatchEvent(new Event("resize"));
    await nextTick();
    expect(where(wrapper).top).toBe("50px");
    wrapper.unmount();
  });
});
