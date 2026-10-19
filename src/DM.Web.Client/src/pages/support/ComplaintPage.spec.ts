/**
 * @vitest-environment jsdom
 */

/**
 * The complaint page over the form: what is a violation, and what is not.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import ComplaintPage from "./ComplaintPage.vue";
import { route } from "./SupportTicketForm.doubles";
import {
  linksOn,
  options,
  stubResizeObserver,
} from "./SupportTicketForm.fixtures";

vi.mock("vue-router", async () =>
  (await import("./SupportTicketForm.doubles")).vueRouter(),
);

// Left mounted, the tiptap editor's timers fire after the environment is torn down.
enableAutoUnmount(afterEach);
stubResizeObserver();

describe("the complaint page", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  it("says what is a violation, with the table of them one link away", () => {
    const wrapper = mount(ComplaintPage, options());

    expect(wrapper.find(".lead-text").text()).toContain(
      "Жалобу разбирают модераторы",
    );
    expect(linksOn(wrapper)).toContainEqual({
      to: { name: "rules", hash: "#penalties" },
      text: "правила сайта",
    });
  });

  it("says a master's decision in his game is not one", () => {
    expect(mount(ComplaintPage, options()).find(".complaint-not").text()).toBe(
      "Решения мастера в его игре нарушением не считаются: подаваясь в игру, игроки принимают его условия",
    );
  });
});
