/**
 * @vitest-environment jsdom
 */

// The last message on one line with the markup out: a <br /> becomes a space.

// The device is in Moscow for the file (see "the time of the last message").
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import { afterAll, afterEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import type { Chat } from "@/entities/message";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";
import ChatPreview from "./ChatPreview.vue";

enableAutoUnmount(afterEach);

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

// The time of the last message on the site's clock: the account's zone for a
// reader; the time for today, the date for older.
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

    // 23:09 of the 26th in Moscow; in Tokyo it is 05:09 of the 27th, and so is "now".
    expect(
      mountPreview("Привет", "2026-09-26T20:09:00Z").get(".date").text(),
    ).toBe("23:09");
    setAccountTimeZone("Asia/Tokyo");
    expect(
      mountPreview("Привет", "2026-09-26T20:09:00Z").get(".date").text(),
    ).toBe("05:09");
  });
});

// The unread follow the name as the header writes them, "SolohinLex (2)"; every row is one fill.
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
          // The words of a tooltip ride on its box.
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
    // No pill and no tint of the row.
    expect(wrapper.find(".unread-count").exists()).toBe(false);
    expect(wrapper.get(".chat-preview").classes()).not.toContain("has-unread");
  });

  it("is not written while nothing is unread", () => {
    const wrapper = mountRow("Ян", 0);

    expect(nameLine(wrapper)).toBe("Ян");
    expect(wrapper.find(".unread").exists()).toBe(false);
  });

  it("reads the row in the order of its lines: name, date, last message", () => {
    const wrapper = mountRow("SolohinLex", 2);

    const parts = wrapper.get(".content").element.children;
    expect([...parts].map((part) => part.className)).toEqual([
      "name-line",
      "date",
      "message-preview",
    ]);
  });
});

// The whole name and the whole excerpt; the number stays right after the name.
describe("the row of a conversation, whole", () => {
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
  });
});
