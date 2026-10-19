/**
 * @vitest-environment jsdom
 */

/**
 * The width of a bubble is the stylesheet's: as wide as its longest line up
 * to the cap. A script once measured the words on a canvas and wrote a width
 * of its own, so a browser that can measure would get an inline width.
 */
import { afterEach, describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import TooltipBubble from "./TooltipBubble.vue";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("TooltipBubble width", () => {
  it("is left to the stylesheet, as wide as the words up to the cap", async () => {
    const measuring = {
      font: "",
      measureText: (text: string) => ({ width: text.length * 7 }),
    };
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(
      measuring as never,
    );
    const wrapper = mount(TooltipBubble, {
      props: {
        text: "Непрочитанных уведомлений: 5",
        anchor: () => new DOMRect(400, 200, 20, 20),
      },
    });
    for (let i = 0; i < 3; i += 1) await nextTick();

    const style = wrapper.get(".tooltip").attributes("style") ?? "";
    expect(style).toContain("max-width: 340px");
    expect(style).not.toMatch(/(^|[^-])width:/);
    wrapper.unmount();
  });
});
