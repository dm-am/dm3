/**
 * @vitest-environment jsdom
 */

// The support page over the form: where it sends the reader, and the windows of the note.
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount } from "@vue/test-utils";
import SupportPage from "./SupportPage.vue";
import {
  linksOn,
  openOn,
  stubResizeObserver,
} from "./SupportTicketForm.fixtures";

// Left mounted, the tiptap editor's timers fire after the environment is torn down.
enableAutoUnmount(afterEach);
stubResizeObserver();

describe("the support page", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  // The note's links change the query only, so a closed window leaves the reader on the form.
  it("opens the ways back to an account over the page, not away from it", async () => {
    const wrapper = await openOn(SupportPage, "AccessRecovery");

    expect(wrapper.text()).not.toMatch(/Перейти/);
    const windows = linksOn(wrapper).filter((entry) =>
      entry.href?.includes("action="),
    );
    expect(windows).not.toHaveLength(0);
    for (const link of windows) {
      expect(link.href).toMatch(/^\/support\?subtype=AccessRecovery&action=/);
    }
  });

  it("sends suggestions and discussions to the forum", async () => {
    expect(linksOn(await openOn(SupportPage))).toContainEqual({
      href: "/forum/improvements",
      text: "Улучшение сайта",
    });
  });

  it("sends every other error to the forum's errors section", async () => {
    // Only security errors and access trouble come here; the rest is a forum topic.
    const wrapper = await openOn(SupportPage);

    expect(wrapper.find(".lead-text").text()).toContain(
      "Это обращение отправится только администрации сайта",
    );
    expect(linksOn(wrapper)).toContainEqual({
      href: "/forum/bugs",
      text: "Ошибки",
    });
  });

  it("sends a question about how the site works to the mentors and the newcomers", async () => {
    const links = linksOn(await openOn(SupportPage));

    expect(links).toContainEqual({
      href: "/community?role=Mentor",
      text: "наставникам",
    });
    expect(links).toContainEqual({
      href: "/forum/newbies",
      text: "Для новичков",
    });
  });
});
