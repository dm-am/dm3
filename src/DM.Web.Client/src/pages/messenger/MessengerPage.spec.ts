/**
 * @vitest-environment jsdom
 */

// The correspondence and the list beside it, the list always whole at the
// width of the right column: its edge is no control, and a width the former
// drag left in this browser changes nothing.
import { describe, expect, it } from "vitest";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import MessengerPage from "./MessengerPage.vue";

const PAGE = join(dirname(fileURLToPath(import.meta.url)), "MessengerPage.vue");

/** The key the former drag stored its width under. */
const FORMER_WIDTH_KEY = "dm3-messenger-list-width";

function open() {
  setActivePinia(createPinia());
  return mount(MessengerPage, {
    global: {
      stubs: {
        RouterView: { template: "<div class='view-stub' />" },
        PageTitle: { template: "<h1><slot /></h1>" },
        ChatsList: { template: "<div class='list-stub' />" },
      },
    },
  });
}

describe("MessengerPage", () => {
  it("lays the correspondence and the list side by side, the line between", () => {
    const wrapper = open();

    const parts = [...wrapper.get(".messenger").element.children].map(
      (el) => el.className,
    );
    expect(parts).toEqual([
      "messenger__chat",
      "messenger__edge",
      "messenger__list",
    ]);
  });

  it("gives the line between them nothing to drag or to focus", () => {
    const edge = open().get(".messenger__edge");

    expect(edge.attributes("role")).toBeUndefined();
    expect(edge.attributes("tabindex")).toBeUndefined();
    expect(edge.attributes("aria-hidden")).toBe("true");
  });

  it("stands the list at the width of the right column, whatever was stored", () => {
    localStorage.setItem(FORMER_WIDTH_KEY, "64");
    const wrapper = open();

    expect(wrapper.get(".messenger__list").attributes("style")).toBeUndefined();
    expect(ruleMap(componentCss(PAGE)).get(".messenger__list")).toMatchObject({
      width: "298px",
    });
    localStorage.clear();
  });
});
