/**
 * @vitest-environment jsdom
 */

// The support page over the form: where it sends the reader, and the windows of the note.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import SupportPage from "./SupportPage.vue";
import { route } from "./SupportTicketForm.doubles";
import {
  linksOn,
  openOn,
  options,
  stubResizeObserver,
} from "./SupportTicketForm.fixtures";

vi.mock("vue-router", async () =>
  (await import("./SupportTicketForm.doubles")).vueRouter(),
);

// Left mounted, the tiptap editor's timers fire after the environment is torn down.
enableAutoUnmount(afterEach);
stubResizeObserver();

describe("the support page", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  // The note's links change the query only, so a closed window leaves the reader on the form.
  it("opens the ways back to an account over the page, not away from it", () => {
    const wrapper = openOn(SupportPage, "AccessRecovery");

    expect(wrapper.text()).not.toMatch(/Перейти/);
    for (const link of linksOn(wrapper).filter(
      (entry) =>
        typeof entry.to === "object" &&
        (entry.to as { query?: Record<string, string> }).query?.action,
    )) {
      expect(Object.keys(link.to as object)).toEqual(["query"]);
    }
  });

  it("sends suggestions and discussions to the forum", () => {
    expect(linksOn(mount(SupportPage, options()))).toContainEqual({
      to: { name: "forum", params: { alias: "improvements" } },
      text: "Улучшение сайта",
    });
  });

  it("sends every other error to the forum's errors section", () => {
    // Only security errors and access trouble come here; the rest is a forum topic.
    const wrapper = mount(SupportPage, options());

    expect(wrapper.find(".lead-text").text()).toContain(
      "Это обращение отправится только администрации сайта",
    );
    expect(linksOn(wrapper)).toContainEqual({
      to: { name: "forum", params: { alias: "bugs" } },
      text: "Ошибки",
    });
  });

  it("sends a question about how the site works to the mentors and the newcomers", () => {
    const links = linksOn(mount(SupportPage, options()));

    expect(links).toContainEqual({
      to: { name: "community", query: { role: "Mentor" } },
      text: "наставникам",
    });
    expect(links).toContainEqual({
      to: { name: "forum", params: { alias: "newbies" } },
      text: "Для новичков",
    });
  });
});
