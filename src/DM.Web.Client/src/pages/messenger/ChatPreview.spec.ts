/**
 * @vitest-environment jsdom
 */

/**
 * The conversation list shows the last message on one line, with the markup
 * taken out. A private message draws its line breaks as <br />, and taking
 * that tag out without a space in its place ran the last word of one line
 * into the first word of the next: "Привет<br />Как дела" read as
 * "ПриветКак дела".
 */

// The device is in Moscow for the file (see "the time of the last message").
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import { afterAll, afterEach, describe, expect, it, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import type { Chat } from "@/entities/message";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";
import ChatPreview from "./ChatPreview.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ push: vi.fn() }),
}));

function mountPreview(text: string, createdUtc = "2026-09-01T10:00:00Z") {
  const chat = {
    id: "c-1",
    type: "Direct",
    participants: [],
    unreadMessagesCount: 0,
    lastMessage: { id: "m-1", text, createdUtc },
  } as unknown as Chat;

  return mount(ChatPreview, {
    props: { chat },
    global: {
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  });
}

const preview = (text: string) =>
  mountPreview(text).find(".message-preview").text();

describe("ChatPreview", () => {
  it("keeps the words of two lines apart", () => {
    expect(preview("Привет<br />Как дела")).toBe("Привет Как дела");
  });

  it("keeps them apart whichever way the break is spelled", () => {
    expect(preview("раз<br>два<BR/>три")).toBe("раз два три");
  });

  it("takes the rest of the markup out without adding anything", () => {
    expect(preview("<strong>жирный</strong> текст")).toBe("жирный текст");
  });
});

// The list prints when the last message came on the site's clock: the zone of
// the account for a signed-in reader, whatever the device says. The time for a
// message of today, the date for an older one, as a messenger's list does.
describe("the time of the last message", () => {
  afterEach(() => {
    setAccountTimeZone(null);
    vi.useRealTimers();
  });

  afterAll(() => {
    if (MACHINE_ZONE === undefined) delete process.env.TZ;
    else process.env.TZ = MACHINE_ZONE;
  });

  it("is on the account's clock for a reader in Tokyo on a device in Moscow", () => {
    setAccountTimeZone("Asia/Tokyo");

    const wrapper = mountPreview("Привет", "2026-09-26T20:09:00Z");

    expect(wrapper.get(".date").text()).toBe("27.09.2026");
  });

  it("is on the device's clock for a guest", () => {
    const wrapper = mountPreview("Привет", "2026-09-26T20:09:00Z");

    expect(wrapper.get(".date").text()).toBe("26.09.2026");
  });

  it("says the time alone for a message of today, on the site's clock", () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-09-26T20:30:00Z"));

    // 23:09 of the 26th in Moscow, where it is 23:30 of the same day.
    // In Tokyo the message is 05:09 of the 27th, and so is "now".
    expect(
      mountPreview("Привет", "2026-09-26T20:09:00Z").get(".date").text(),
    ).toBe("23:09");
    setAccountTimeZone("Asia/Tokyo");
    expect(
      mountPreview("Привет", "2026-09-26T20:09:00Z").get(".date").text(),
    ).toBe("05:09");
  });
});

/**
 * The unread messages of a conversation are counted the way the header counts
 * its own, "SolohinLex (2)": they used to stand in a grey pill at the right
 * edge of a row tinted blue, a plate and a fill that wrote one number nowhere
 * else on the site. Every row is one fill now, and the number follows the
 * name.
 */
describe("the unread of a conversation", () => {
  function mountRow(username: string, unread: number) {
    const chat = {
      id: "c-1",
      type: "Direct",
      participants: [],
      unreadMessagesCount: unread,
      lastMessage: {
        id: "m-1",
        text: "Привет",
        createdUtc: "2026-10-05T04:38:00Z",
      },
    } as unknown as Chat;

    return mount(ChatPreview, {
      props: { chat, interlocutor: { username } as never },
      attachTo: document.body,
      global: {
        stubs: {
          RouterLink: { template: "<a><slot /></a>" },
          // The words of a tooltip ride on its box, for the assertions to read.
          Tooltip: {
            props: ["text"],
            template: `<span class="tip" :data-tip="text"><slot /></span>`,
          },
        },
      },
    });
  }

  /** The line of the name as it copies: one line, the spaces as plain ones. */
  const nameLine = (wrapper: ReturnType<typeof mountRow>) =>
    wrapper.get(".name-line").text().replace(/\s+/g, " ");

  it("follows the name in brackets, as the header writes it", () => {
    const wrapper = mountRow("SolohinLex", 2);

    expect(nameLine(wrapper)).toBe("SolohinLex (2)");
    expect(wrapper.get(".unread-number").text()).toBe("2");
    expect(wrapper.findAll(".bracket").map((b) => b.text())).toEqual([
      "(",
      ")",
    ]);
    // No pill of its own, and no tint of the row that would tell it apart.
    expect(wrapper.find(".unread-count").exists()).toBe(false);
    expect(wrapper.get(".chat-preview").classes()).not.toContain("has-unread");
    wrapper.unmount();
  });

  it("is not written while nothing is unread", () => {
    const wrapper = mountRow("Ян", 0);

    expect(nameLine(wrapper)).toBe("Ян");
    expect(wrapper.find(".unread").exists()).toBe(false);
    wrapper.unmount();
  });

  it("reads the row in the order of its lines: name, date, last message", () => {
    const wrapper = mountRow("SolohinLex", 2);

    const parts = wrapper.get(".content").element.children;
    expect([...parts].map((part) => part.className)).toEqual([
      "name-line",
      "date",
      "message-preview",
    ]);
    wrapper.unmount();
  });
});

/**
 * The time of the last message stands to the right of the name, at the far
 * end of the first line. Where a long name leaves no room beside it, it goes
 * down whole, right under the name, and never breaks inside itself: the line
 * decides by what is on it, not by a width of the row.
 */
describe("the date of a conversation", () => {
  const css = componentCss(
    resolve(dirname(fileURLToPath(import.meta.url)), "ChatPreview.vue"),
  );
  const rules = styleRules(css);
  const on = (selector: string) =>
    Object.assign(
      {},
      ...rules
        .filter((rule) => rule.selector === selector)
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  it("stands beside the name on a line that wraps, at the line's far end", () => {
    expect(on(".content")).toMatchObject({
      display: "flex",
      "flex-wrap": "wrap",
      "justify-content": "space-between",
    });
    // The last message takes a line of its own under the two.
    expect(on(".message-preview")["flex-basis"]).toBe("100%");
  });

  it("goes down whole, and only by what is on the line", () => {
    expect(on(".date")["white-space"]).toBe("nowrap");
    // No width of the row decides it: the threshold of the longest login is
    // gone, and so is the box it was measured by.
    expect(css).not.toContain("@container");
    expect(on(".chat-preview")["container-type"]).toBeUndefined();
  });

  it("lets a name longer than the line wrap within the row", () => {
    // A flex item is as wide as its longest word at the least; a login of
    // twenty letters would push the row wider than a phone without this.
    expect(on(".name-line")["min-width"]).toBe("0");
    expect(on(".message-preview")["min-width"]).toBe("0");
  });
});

/**
 * The row says the whole name of the other one and the whole excerpt of the
 * last message. Where the row has no room, the name and the excerpt wrap; the
 * number stays right after the name with its brackets.
 */
describe("the row of a conversation, whole", () => {
  const rules = styleRules(
    componentCss(
      resolve(dirname(fileURLToPath(import.meta.url)), "ChatPreview.vue"),
    ),
  );
  /** The declarations of the rules on one selector, merged. */
  const on = (selector: string) =>
    Object.assign(
      {},
      ...rules
        .filter((rule) => rule.selector === selector)
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  it("cuts neither the name nor the excerpt", () => {
    for (const part of [".name-line", ".name", ".message-preview"]) {
      const declared = on(part);
      expect(declared["text-overflow"], part).toBeUndefined();
      expect(declared.overflow, part).toBeUndefined();
      expect(declared["white-space"], part).toBeUndefined();
      expect(declared["max-width"], part).toBeUndefined();
    }
    expect(on(".unread")["white-space"]).toBe("nowrap");
  });

  it("says a long name whole, with no tooltip and no room kept for it", () => {
    const chat = {
      id: "c-1",
      type: "Direct",
      participants: [],
      unreadMessagesCount: 15,
      lastMessage: { id: "m-1", text: "Привет", createdUtc: "2026-10-05" },
    } as unknown as Chat;
    const wrapper = mount(ChatPreview, {
      props: {
        chat,
        interlocutor: { username: "ДлинноеИмяИгрокаВсех" } as never,
      },
      global: { stubs: { RouterLink: { template: "<a><slot /></a>" } } },
    });

    expect(wrapper.get(".name").text()).toBe("ДлинноеИмяИгрокаВсех");
    expect(wrapper.get(".name").attributes("title")).toBeUndefined();
    expect(wrapper.find(".tooltip-trigger").exists()).toBe(false);
    expect(wrapper.get(".name-line").attributes("style")).toBeUndefined();
    wrapper.unmount();
  });
});

/** A conversation of a group is named by its title. */
describe("a group in the list", () => {
  it("names the group by its title", () => {
    const chat = {
      id: "g-1",
      type: "Group",
      title: "Партия Кайдана",
      participants: [{ username: "Bob" }, { username: "Eve" }],
      unreadMessagesCount: 0,
    } as unknown as Chat;
    const wrapper = mount(ChatPreview, {
      props: { chat, interlocutor: { username: "Bob" } as never },
      global: { stubs: { RouterLink: { template: "<a><slot /></a>" } } },
    });

    expect(wrapper.get(".name").text()).toBe("Партия Кайдана");
    expect(wrapper.get(".message-preview").text()).toBe("Нет сообщений");
    wrapper.unmount();
  });
});

/**
 * The narrow list keeps the picture and the count of unread under it. The
 * name is the link's own name for a reader of the screen and the picture's
 * tooltip.
 */
describe("the narrow row", () => {
  function mountNarrow(unread: number) {
    const chat = {
      id: "c-1",
      type: "Direct",
      participants: [],
      unreadMessagesCount: unread,
      lastMessage: { id: "m-1", text: "Привет", createdUtc: "2026-10-05" },
    } as unknown as Chat;
    return mount(ChatPreview, {
      props: {
        chat,
        interlocutor: { username: "SolohinLex" } as never,
        narrow: true,
      },
      global: {
        stubs: {
          RouterLink: { template: "<a><slot /></a>" },
          Tooltip: {
            props: ["text"],
            template: `<span class="tip" :data-tip="text"><slot /></span>`,
          },
        },
      },
    });
  }

  it("draws the picture and the count under it, and no words of the row", () => {
    const wrapper = mountNarrow(4);

    expect(wrapper.find(".avatar").exists()).toBe(true);
    expect(wrapper.get(".unread").text()).toBe("(4)");
    expect(wrapper.find(".name").exists()).toBe(false);
    expect(wrapper.find(".message-preview").exists()).toBe(false);
    expect(wrapper.find(".date").exists()).toBe(false);
    wrapper.unmount();
  });

  it("names the conversation and its count for a reader of the screen", () => {
    const wrapper = mountNarrow(4);

    expect(wrapper.get("a").attributes("aria-label")).toBe("SolohinLex (4)");
    expect(wrapper.get(".tip").attributes("data-tip")).toBe("SolohinLex");
    wrapper.unmount();
  });

  it("writes no count while nothing is unread", () => {
    const wrapper = mountNarrow(0);

    expect(wrapper.find(".unread").exists()).toBe(false);
    expect(wrapper.get("a").attributes("aria-label")).toBe("SolohinLex");
    wrapper.unmount();
  });
});
