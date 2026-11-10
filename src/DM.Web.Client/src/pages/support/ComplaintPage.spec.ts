/**
 * @vitest-environment jsdom
 */

/**
 * The complaint page over the form: what is a violation, and what is not.
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount } from "@vue/test-utils";
import ComplaintPage from "./ComplaintPage.vue";
import {
  linksOn,
  openOn,
  stubResizeObserver,
} from "./SupportTicketForm.fixtures";

// Left mounted, the tiptap editor's timers fire after the environment is torn down.
enableAutoUnmount(afterEach);
stubResizeObserver();

describe("the complaint page", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("says what is a violation, with the table of them one link away", async () => {
    const wrapper = await openOn(ComplaintPage);

    expect(wrapper.find(".lead-text").text()).toContain(
      "Жалобу разбирают модераторы",
    );
    expect(linksOn(wrapper)).toContainEqual({
      href: "/rules#penalties",
      text: "правила сайта",
    });
  });

  it("says a master's decision in his game is not one", async () => {
    expect((await openOn(ComplaintPage)).find(".complaint-not").text()).toBe(
      "Решения мастера в его игре нарушением не считаются: подаваясь в игру, игроки принимают его условия",
    );
  });
});
