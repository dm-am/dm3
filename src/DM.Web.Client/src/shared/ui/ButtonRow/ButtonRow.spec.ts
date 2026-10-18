/**
 * @vitest-environment jsdom
 */

/**
 * The row of buttons on the page: one line where it has the room, nothing
 * hidden, nothing turned into a column (a phone shows the row the full version
 * shows, and the reader zooms in); where it has not, a button goes down a line.
 *
 * A document of tests lays nothing out, so the readings of the page are stood
 * in for: every item is as wide as the number in its data-width, the row as
 * wide as `room`, and the observer of sizes and the frame are called by hand,
 * so a row that turned itself into a column where its buttons do not fit
 * would do it here.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import { h } from "vue";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import ButtonRow from "./ButtonRow.vue";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const SFC = join(dirname(fileURLToPath(import.meta.url)), "ButtonRow.vue");

enableAutoUnmount(afterEach);

/** The width of the row's content, as the test sets it. */
let room = 0;

/** The observers of sizes the rows made, with the boxes they watch. */
const observers: FakeObserver[] = [];

class FakeObserver {
  readonly boxes = new Set<Element>();
  constructor(readonly callback: ResizeObserverCallback) {
    observers.push(this);
  }
  observe(box: Element) {
    this.boxes.add(box);
  }
  unobserve(box: Element) {
    this.boxes.delete(box);
  }
  disconnect() {
    this.boxes.clear();
  }
}

/** The frames asked for and not drawn yet. */
let frames: FrameRequestCallback[] = [];

/** Changes the room, reports it as the browser would, and draws the frame. */
function resize(width: number) {
  room = width;
  for (const observer of observers) {
    observer.callback([], observer as unknown as ResizeObserver);
  }
  const due = frames;
  frames = [];
  due.forEach((frame) => frame(0));
}

const box = (width: number) =>
  ({ width, height: 38, top: 0, left: 0, right: width, bottom: 38 }) as DOMRect;

beforeEach(() => {
  room = 0;
  observers.length = 0;
  frames = [];
  vi.stubGlobal("ResizeObserver", FakeObserver);
  vi.stubGlobal("requestAnimationFrame", (frame: FrameRequestCallback) => {
    frames.push(frame);
    return frames.length;
  });
  vi.stubGlobal("cancelAnimationFrame", () => {});
  vi.spyOn(Element.prototype, "getClientRects").mockImplementation(function (
    this: Element,
  ) {
    return (this.isConnected ? [box(0)] : []) as unknown as DOMRectList;
  });
  vi.spyOn(Element.prototype, "getBoundingClientRect").mockImplementation(
    function (this: Element) {
      if (this.classList.contains("button-row")) return box(room);
      return box(Number((this as HTMLElement).dataset.width ?? 0));
    },
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  document.body.innerHTML = "";
});

/** Three buttons of 100, 120 and 80, in the order they were given. */
function render() {
  return mount(ButtonRow, {
    attachTo: document.body,
    slots: {
      default: () => [
        h("button", { type: "button", "data-width": 100 }, "Первая"),
        h("button", { type: "button", "data-width": 120 }, "Вторая"),
        h("button", { type: "button", "data-width": 80 }, "Третья"),
      ],
    },
  });
}

const isColumn = (wrapper: ReturnType<typeof render>) =>
  wrapper.classes().includes("button-row--column");

const hidden = (wrapper: ReturnType<typeof render>) =>
  wrapper.findAll("button").filter((b) => {
    const el = b.element as HTMLElement;
    return el.hidden || el.style.display === "none";
  });

describe("ButtonRow", () => {
  it("turns into no column whatever its room, every button in sight and in order", async () => {
    room = 400;
    const wrapper = render();
    await wrapper.vm.$nextTick();
    expect(isColumn(wrapper)).toBe(false);

    resize(250);
    await wrapper.vm.$nextTick();
    expect(isColumn(wrapper)).toBe(false);
    expect(hidden(wrapper)).toEqual([]);
    expect(wrapper.findAll("button").map((b) => b.text())).toEqual([
      "Первая",
      "Вторая",
      "Третья",
    ]);
  });

  it("marks no button to break its words, however narrow the row", async () => {
    room = 90;
    const wrapper = mount(ButtonRow, {
      attachTo: document.body,
      slots: {
        default: () => [
          h(
            "button",
            {
              type: "button",
              "data-width": 120,
              style: "white-space: nowrap",
            },
            "Отключить двухфакторную аутентификацию",
          ),
          h(
            "button",
            {
              type: "button",
              "data-width": 80,
              style: "white-space: nowrap",
            },
            "Удалить",
          ),
        ],
      },
    });
    resize(90);
    await wrapper.vm.$nextTick();

    expect(isColumn(wrapper)).toBe(false);
    expect(wrapper.find("[data-button-row-wraps]").exists()).toBe(false);
  });

  // jsdom lays nothing out, so the stylesheet is what is read: a row that
  // kept every button on one line whatever its room stood over the next
  // column of the page at 1024 and 1280.
  it("sends a button with no room in the line down to the next one", () => {
    const row = ruleMap(componentCss(SFC)).get(".button-row");
    expect(row?.["flex-wrap"]).toBe("wrap");
    expect(componentCss(SFC)).not.toMatch(/@media|@container/);
  });

  it("lets nothing in it be wider than the row", () => {
    const rules = ruleMap(componentCss(SFC));
    const item = [...rules].find(([selector]) =>
      /^.button-row > /.test(selector),
    )?.[1];
    expect(item?.["max-width"]).toBe("100%");
    const button = [...rules].find(([selector]) =>
      /^.button-row .*button/.test(selector),
    )?.[1];
    expect(button).toMatchObject({
      "max-width": "100%",
      "white-space": "normal",
    });
  });

  it("keeps the step it is given between its buttons", () => {
    const wrapper = mount(ButtonRow, { props: { gap: "medium", tag: "span" } });
    expect(wrapper.element.tagName).toBe("SPAN");
    expect(wrapper.classes()).toContain("button-row--gap-medium");
  });
});
