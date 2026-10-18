/**
 * @vitest-environment jsdom
 */

/**
 * The corner of the bubble is where its edit and delete icons stand, and only
 * for a caller that has any: the read-only surfaces (the home page gallery, the
 * profile listings) pass no `actions` slot and must get neither the corner nor
 * the share of the first line it holds.
 *
 * The icons come after the text in the DOM, so a screen reader reads what is
 * being acted on before the controls. Where they land on screen is the
 * stylesheet's business and is measured on a live stand, not here.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { Tooltip } from "@/shared/ui/Tooltip";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import SpeechBubble from "./SpeechBubble.vue";
import BubbleAction from "./BubbleAction.vue";

describe("SpeechBubble actions", () => {
  it("holds no corner for a caller without actions", () => {
    const wrapper = mount(SpeechBubble, { slots: { default: "Текст" } });

    expect(wrapper.find(".bubble-actions").exists()).toBe(false);
    expect(wrapper.find(".bubble-actions-space").exists()).toBe(false);
  });

  it("puts the actions in the corner after the text", () => {
    const wrapper = mount(SpeechBubble, {
      slots: {
        default: "Текст",
        actions: '<button type="button" class="probe">x</button>',
      },
    });

    const bubble = wrapper.get(".bubble").element;
    const children = Array.from(bubble.children).map((el) => el.className);
    expect(children.indexOf("bubble-actions")).toBeGreaterThan(
      children.indexOf("bubble-content"),
    );
    expect(wrapper.find(".bubble-actions .probe").exists()).toBe(true);

    // The first line's share of the corner: silent, and ahead of the text so
    // the line it shortens is the first one.
    const space = wrapper.get(".bubble-content").element.firstElementChild;
    expect(space?.className).toBe("bubble-actions-space");
    expect(space?.getAttribute("aria-hidden")).toBe("true");
  });
});

describe("BubbleAction", () => {
  const render = () =>
    mount(BubbleAction, { props: { icon: "trash", label: "Удалить" } });

  it("is a button named for a screen reader and described on hover", () => {
    const wrapper = render();
    const button = wrapper.get("button");

    expect(button.attributes("type")).toBe("button");
    expect(button.attributes("aria-label")).toBe("Удалить");
    expect(wrapper.findComponent(Tooltip).props("text")).toBe("Удалить");
    expect(wrapper.findComponent(SvgIcon).props("name")).toBe("trash");
    // The name is the label alone: the glyph is hidden from the reader.
    expect(button.text()).toBe("");
  });

  it("says a click to the caller", async () => {
    const wrapper = render();
    await wrapper.get("button").trigger("click");

    expect(wrapper.emitted("click")).toHaveLength(1);
  });
});
