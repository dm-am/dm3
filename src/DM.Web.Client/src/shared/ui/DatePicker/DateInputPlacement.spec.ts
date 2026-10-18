/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import DateInput from "./DateInput.vue";

/**
 * Where the calendar of a date field stands, in numbers. jsdom lays out
 * nothing: the field's rectangle and the window are given by hand, and the
 * calendar, which measures 0 here, is taken at the size the field assumes
 * before it can measure it (266 by 290) unless a test gives it one.
 */
const html = document.documentElement;
const restores: Array<() => void> = [];

function windowOf(width: number, height: number) {
  Object.defineProperty(html, "clientWidth", {
    configurable: true,
    get: () => width,
  });
  Object.defineProperty(html, "clientHeight", {
    configurable: true,
    get: () => height,
  });
  restores.push(() => {
    delete (html as unknown as Record<string, unknown>).clientWidth;
    delete (html as unknown as Record<string, unknown>).clientHeight;
  });
}

/** The calendar measured at a size of its own once it is on the page. */
function calendarMeasures(width: number, height: number) {
  const proto = HTMLElement.prototype;
  const sizes = { offsetWidth: width, offsetHeight: height };
  for (const [name, value] of Object.entries(sizes)) {
    const own = Object.getOwnPropertyDescriptor(proto, name)!;
    Object.defineProperty(proto, name, {
      configurable: true,
      get(this: HTMLElement) {
        return this.classList.contains("di-popover")
          ? value
          : own.get!.call(this);
      },
    });
    restores.push(() => Object.defineProperty(proto, name, own));
  }
}

afterEach(() => {
  while (restores.length) restores.pop()!();
  document.body.innerHTML = "";
});
// Registered after the cleanup above, so it runs first: a field comes off the
// page, and its listeners on the window with it, before the window is restored.
enableAutoUnmount(afterEach);

/** A date field at a place on the screen, 38 high and 200 wide. */
function placedField(left: number, top: number) {
  const wrapper = mount(DateInput, {
    attachTo: document.body,
    props: { modelValue: "2026-07-14" },
    global: { stubs: { SvgIcon: true } },
  });
  const box = { left, top };
  Object.defineProperty(
    wrapper.get(".di-field").element,
    "getBoundingClientRect",
    {
      configurable: true,
      value: () => ({
        left: box.left,
        top: box.top,
        right: box.left + 200,
        bottom: box.top + 38,
        width: 200,
        height: 38,
      }),
    },
  );
  return { wrapper, box };
}

async function openedAt(left: number, top: number) {
  const placed = placedField(left, top);
  await placed.wrapper.get(".di-toggle").trigger("click");
  await nextTick();
  return placed;
}

const where = (wrapper: ReturnType<typeof placedField>["wrapper"]) => {
  const { top, left } = (wrapper.get(".di-popover").element as HTMLElement)
    .style;
  return { top, left };
};

describe("DateInput: where its calendar stands", () => {
  // The gap is POPOVER_GAP (2), the edge VIEWPORT_EDGE (8).
  it.each<[string, [number, number], [number, number], object]>([
    // 50 + 38 + 2
    [
      "under the field, left edges together",
      [1366, 800],
      [100, 50],
      { top: "90px", left: "100px" },
    ],
    // 400 - 2 - 290
    [
      "above the field where the window has no room below",
      [1366, 500],
      [100, 400],
      { top: "108px", left: "100px" },
    ],
    // 400 - 8 - 290
    [
      "against the bottom edge where there is room neither below nor above",
      [1366, 400],
      [100, 150],
      { top: "102px", left: "100px" },
    ],
    [
      "pushed back inside the window at its left edge",
      [1366, 800],
      [-30, 50],
      { top: "90px", left: "8px" },
    ],
    // 375 - 8 - 266
    [
      "pushed back inside the window at its right edge",
      [375, 800],
      [300, 50],
      { top: "90px", left: "101px" },
    ],
  ])("stands %s", async (_case, [w, h], [x, y], at) => {
    windowOf(w, h);
    const { wrapper } = await openedAt(x, y);

    expect(where(wrapper)).toEqual(at);
  });

  it("takes the calendar's own size once it can measure it", async () => {
    windowOf(375, 500);
    calendarMeasures(300, 320);
    const { wrapper } = await openedAt(300, 400);

    // 375 - 8 - 300 across, 400 - 2 - 320 up.
    expect(where(wrapper)).toEqual({ top: "78px", left: "67px" });
  });

  it("follows the field on a scroll and on a resize while open", async () => {
    windowOf(1366, 800);
    const { wrapper, box } = await openedAt(100, 50);

    box.left = 40;
    document.dispatchEvent(new Event("scroll"));
    await nextTick();
    expect(where(wrapper).left).toBe("40px");

    box.top = 10;
    window.dispatchEvent(new Event("resize"));
    await nextTick();
    expect(where(wrapper).top).toBe("50px");
  });
});
