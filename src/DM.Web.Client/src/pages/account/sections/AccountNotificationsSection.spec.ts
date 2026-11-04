/**
 * @vitest-environment jsdom
 */

/**
 * The notifications block of the account page: a grey panel with a framed row
 * a channel, the name over its state in grey and the one press beside them,
 * and under them a column of boxes or the steps that link it. "Подключить"
 * asks for a code itself; nothing in the steps is copied by a button, and the
 * code runs out on a countdown that puts the offer back with no plate.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { NotificationSettings } from "@/shared/api/models/account";
import AccountNotificationsSection from "./AccountNotificationsSection.vue";

enableAutoUnmount(afterEach);

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** Telegram linked, with its boxes; Discord not linked. */
const TELEGRAM_ONLY: NotificationSettings = {
  telegram: {
    connected: true,
    enabled: true,
    enabledCategories: ["Messages"],
  },
  discord: undefined,
};

const NOTHING_LINKED: NotificationSettings = {
  telegram: undefined,
  discord: undefined,
};

/** Mounts the block over what the server holds of the two channels. */
async function render(held: NotificationSettings = TELEGRAM_ONLY) {
  vi.spyOn(accountApi, "getNotificationPreferences").mockResolvedValue({
    data: held,
    error: null,
  } as never);
  const wrapper = mount(AccountNotificationsSection, {
    global: { plugins: [createPinia()] },
    attachTo: document.body,
  });
  await flushPromises();
  return wrapper;
}

/** The row of one channel. */
const card = (wrapper: VueWrapper, channel: "telegram" | "discord") =>
  wrapper.get(`.channel-item[data-channel="${channel}"]`);

/** The button of a row that its caption names. */
const button = (
  wrapper: VueWrapper,
  channel: "telegram" | "discord",
  caption: string,
) => {
  const found = card(wrapper, channel)
    .findAll("button")
    .find((one) => one.text() === caption);
  if (!found) throw new Error(`no "${caption}" in the ${channel} row`);
  return found;
};

/** Presses "Подключить" on a row and lets the code come back. */
async function startLinking(
  wrapper: VueWrapper,
  channel: "telegram" | "discord",
  code: string,
  secondsLeft = 900,
) {
  vi.spyOn(accountApi, "generateBotCode").mockResolvedValue({
    data: {
      code,
      expiresUtc: new Date(Date.now() + secondsLeft * 1000).toISOString(),
    },
    error: null,
  } as never);

  await button(wrapper, channel, "Подключить").trigger("click");
  await flushPromises();
}

describe("the notifications block of the account page", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    [...toast.toasts.value].forEach((one) => toast.dismiss(one.id));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe("its doors", () => {
    it("still reads itself once on mount", async () => {
      await render();

      expect(accountApi.getNotificationPreferences).toHaveBeenCalledTimes(1);
    });

    // The notifications page links to the section by its id.
    it("answers to #notifications, the address the notifications page links to", async () => {
      const wrapper = await render();

      expect(wrapper.get("section").attributes("id")).toBe("notifications");
    });
  });

  describe("the state of a channel", () => {
    it("names a linked channel, says it is linked, and offers Отключить beside it", async () => {
      const wrapper = await render();

      const telegram = card(wrapper, "telegram");
      const head = telegram.get(".channel-head");
      expect(head.get("h3").text()).toBe("Telegram");
      expect(head.get(".channel-state").text()).toBe("Подключен");
      expect(head.get("button").text()).toBe("Отключить");
      expect(telegram.text()).toContain("Включить уведомления");
    });

    it("says a channel that is not linked is not, and offers to link it", async () => {
      const wrapper = await render();

      const discord = card(wrapper, "discord");
      const head = discord.get(".channel-head");
      expect(head.get("h3").text()).toBe("Discord");
      expect(head.get(".channel-state").text()).toBe("Не подключен");
      expect(head.get("button").text()).toBe("Подключить");
      expect(discord.find(".channel-settings").exists()).toBe(false);
    });

    // One block about the bots, never offering to link a linked channel.
    it("is the one block about the bots, and never offers to link a linked channel", async () => {
      const wrapper = await render();

      expect(wrapper.findAll(".channel-item")).toHaveLength(2);
      expect(card(wrapper, "telegram").text()).not.toContain("Подключить");
    });

    // What the channel sends: a caption and a column of boxes named by it.
    it("lays what the channel sends out under the row, as a group named by its caption", async () => {
      const wrapper = await render();

      const telegram = card(wrapper, "telegram");
      const settings = telegram.get(".channel-settings");
      expect(
        telegram.element.children[0].classList.contains("channel-head"),
      ).toBe(true);
      const caption = settings.get(".categories-caption");
      expect(caption.text()).toBe("Что присылать");
      const group = settings.get(".categories-list");
      expect(group.attributes("role")).toBe("group");
      expect(group.attributes("aria-labelledby")).toBe(
        caption.attributes("id"),
      );
      expect(group.findAll('input[type="checkbox"]')).toHaveLength(7);
    });
  });

  describe("linking a channel", () => {
    it("asks for a code and lays the steps out under the row that was pressed", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      expect(accountApi.generateBotCode).toHaveBeenCalledWith("discord");
      const discord = card(wrapper, "discord");
      expect(discord.get("code").text()).toBe("/connect 482913");
      expect(discord.get(".bot-link").text()).toBe("Dungeon Master Bot");
      expect(discord.text()).toMatch(/Код действителен: 14:5[0-9]/);
      expect(discord.text()).not.toContain("Подключить");
      expect(discord.get(".channel-head button").text()).toBe("Отмена");
      expect(discord.get(".channel-state").text()).toBe("Не подключен");
      // The other row is left as it was.
      expect(card(wrapper, "telegram").find(".link-process").exists()).toBe(
        false,
      );
      expect(button(wrapper, "telegram", "Подключить").exists()).toBe(true);
    });

    it("says a refused code at the foot of its own row", async () => {
      const wrapper = await render(NOTHING_LINKED);
      vi.spyOn(accountApi, "generateBotCode").mockResolvedValue({
        data: null,
        error: { status: 409, title: "Код уже выдан" },
      } as never);

      await button(wrapper, "telegram", "Подключить").trigger("click");
      await flushPromises();

      expect(card(wrapper, "telegram").get(".action-result").text()).toBe(
        "Код уже выдан",
      );
      expect(card(wrapper, "discord").find(".action-result").exists()).toBe(
        false,
      );
      expect(card(wrapper, "telegram").find(".link-process").exists()).toBe(
        false,
      );
    });

    it("puts the row back to its offer on Отмена", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      await button(wrapper, "discord", "Отмена").trigger("click");

      const discord = card(wrapper, "discord");
      expect(discord.find(".link-process").exists()).toBe(false);
      expect(discord.text()).not.toContain("482913");
      expect(button(wrapper, "discord", "Подключить").exists()).toBe(true);
    });

    // Each press takes away its own control: focus stays in the row.
    it("keeps focus in the row when a press takes its control away", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      expect(document.activeElement).toBe(
        card(wrapper, "discord").get(".link-process").element,
      );

      await button(wrapper, "discord", "Отмена").trigger("click");
      await flushPromises();

      expect(document.activeElement).toBe(
        button(wrapper, "discord", "Подключить").element,
      );
    });

    it("hands focus to Подключить once a channel is disconnected", async () => {
      const wrapper = await render();
      vi.spyOn(accountApi, "disconnectBot").mockResolvedValue({
        data: null,
        error: null,
      } as never);

      await button(wrapper, "telegram", "Отключить").trigger("click");
      await flushPromises();

      expect(document.activeElement).toBe(
        button(wrapper, "telegram", "Подключить").element,
      );
    });

    // The command is sent from the bot: coming back to the page tells the block the channel is linked.
    it("trades the steps for the settings once the channel is linked and the page is back in view", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");
      vi.mocked(accountApi.getNotificationPreferences).mockResolvedValue({
        data: {
          telegram: undefined,
          discord: {
            connected: true,
            enabled: true,
            enabledCategories: ["Games"],
          },
        },
        error: null,
      } as never);

      window.dispatchEvent(new Event("focus"));
      await flushPromises();

      const discord = card(wrapper, "discord");
      expect(discord.find(".link-process").exists()).toBe(false);
      expect(discord.get("h3").text()).toBe("Discord");
      expect(discord.text()).toContain("Включить уведомления");
      expect(button(wrapper, "discord", "Отключить").exists()).toBe(true);
    });

    it("keeps the steps while the channel is still not linked", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      window.dispatchEvent(new Event("focus"));
      await flushPromises();

      expect(card(wrapper, "discord").get("code").text()).toBe(
        "/connect 482913",
      );
    });

    it("reads nothing again while no block is linking", async () => {
      await render(NOTHING_LINKED);

      window.dispatchEvent(new Event("focus"));
      await flushPromises();

      expect(accountApi.getNotificationPreferences).toHaveBeenCalledTimes(1);
    });
  });

  describe("the command of the bot", () => {
    // No copy button: the reader selects the command himself.
    it("is written in the step as words, with no button that copies it", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      const discord = card(wrapper, "discord");
      const step = discord
        .findAll(".link-step")
        .find((one) => one.find("code").exists());
      expect(step?.text().replace(/\s+/g, " ")).toBe(
        "2. Отправьте боту команду /connect 482913",
      );
      expect(discord.findAll("button").map((one) => one.text())).toEqual([
        "Отмена",
      ]);
    });

    it("leads to the bot by its address, as a plain link", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      const link = card(wrapper, "discord").get(".bot-link");
      expect(link.attributes("href")).toBe("https://discord.com/");
      expect(link.text()).toBe("Dungeon Master Bot");
    });
  });

  // Telegram takes the code in the start parameter of a t.me link, so its steps give that link.
  describe("the start link of the Telegram bot", () => {
    it("opens the bot with the code in its start parameter", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "telegram", "ABC123");

      const link = card(wrapper, "telegram").get(".bot-link");
      expect(link.attributes("href")).toBe(
        "https://t.me/DM3NotifyBot?start=ABC123",
      );
      expect(link.attributes("target")).toBe("_blank");
    });

    // Its words are the address, copied as any other words.
    it("is a plain link whose words are the address", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "telegram", "ABC123");

      const steps = card(wrapper, "telegram")
        .findAll(".link-step")
        .map((one) => one.text().replace(/\s+/g, " "));
      expect(steps).toEqual([
        "1. Откройте бота по ссылке t.me/DM3NotifyBot?start=ABC123",
        '2. Нажмите в нем "Запустить"',
      ]);
    });

    it("names no command and offers no button that copies anything", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "telegram", "ABC123");

      const telegram = card(wrapper, "telegram");
      expect(telegram.find("code").exists()).toBe(false);
      expect(telegram.findAll("button").map((one) => one.text())).toEqual([
        "Отмена",
      ]);
    });
  });

  describe("the code running out", () => {
    it("puts the row back to its offer, and says nothing over it", async () => {
      // Only the countdown and its clock: setTimeout stays real for flushPromises.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval", "Date"] });
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "telegram", "ABC123", 2);

      expect(card(wrapper, "telegram").find(".link-process").exists()).toBe(
        true,
      );

      await vi.advanceTimersByTimeAsync(2000);
      await nextTick();

      const telegram = card(wrapper, "telegram");
      expect(telegram.find(".link-process").exists()).toBe(false);
      expect(telegram.text()).not.toContain("ABC123");
      expect(button(wrapper, "telegram", "Подключить").exists()).toBe(true);
      expect(saidOutLoud()).toEqual([]);
    });
  });
});

// The router scrolls to the section while the blocks above still load, and
// their growth pushes it past the window. jsdom lays nothing out: the growth
// is an observer told by hand, the scroll a spy.
describe("arriving at #notifications", () => {
  /** The resize observers the block made, with what they watch. */
  let observers: {
    grow: () => void;
    watched: Element[];
    gone: boolean;
  }[];
  let scrolled: ReturnType<typeof vi.fn>;
  let page: HTMLElement;

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    observers = [];
    vi.stubGlobal(
      "ResizeObserver",
      class {
        record: (typeof observers)[number];
        constructor(callback: () => void) {
          // A disconnected observer reports nothing, as a real one.
          const record = {
            grow: () => {
              if (!record.gone) callback();
            },
            watched: [] as Element[],
            gone: false,
          };
          this.record = record;
          observers.push(this.record);
        }
        observe(element: Element) {
          this.record.watched.push(element);
          // An observer reports the size it starts from.
          this.record.grow();
        }
        disconnect() {
          this.record.gone = true;
        }
      },
    );
    scrolled = vi.fn();
    Object.defineProperty(HTMLElement.prototype, "scrollIntoView", {
      configurable: true,
      value: scrolled,
    });
    page = document.createElement("div");
    document.body.appendChild(page);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    delete (HTMLElement.prototype as Partial<HTMLElement>).scrollIntoView;
    window.history.replaceState(null, "", "/");
    page.remove();
  });

  /** Mounts the block as the last section of a page, at an address. */
  async function arrive(address: string) {
    window.history.replaceState(null, "", address);
    vi.spyOn(accountApi, "getNotificationPreferences").mockResolvedValue({
      data: TELEGRAM_ONLY,
      error: null,
    } as never);
    const wrapper = mount(AccountNotificationsSection, {
      global: { plugins: [createPinia()] },
      attachTo: page,
    });
    await flushPromises();
    return wrapper;
  }

  /** The scrolls that brought the section itself into view. */
  const ownScrolls = (wrapper: VueWrapper) =>
    scrolled.mock.contexts.filter((that) => that === wrapper.element).length;

  it("brings itself into view each time the page above it grows", async () => {
    const wrapper = await arrive("/account#notifications");

    expect(observers).toHaveLength(1);
    // The page the section stands in, where what grows is.
    expect(observers[0].watched).toEqual([wrapper.element.parentElement]);
    expect(scrolled).toHaveBeenCalledWith({ block: "start" });
    const before = ownScrolls(wrapper);

    observers[0].grow();
    observers[0].grow();

    expect(ownScrolls(wrapper)).toBe(before + 2);
  });

  // The reader has the page from the first wheel, key or press.
  it.each(["wheel", "keydown", "pointerdown"])(
    "lets the page go at the reader's first %s",
    async (input) => {
      const wrapper = await arrive("/account#notifications");

      window.dispatchEvent(new Event(input));
      const before = ownScrolls(wrapper);
      observers[0].grow();

      expect(observers[0].gone).toBe(true);
      expect(ownScrolls(wrapper)).toBe(before);
    },
  );

  it("lets the page go once the page has had time to settle", async () => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    try {
      window.history.replaceState(null, "", "/account#notifications");
      vi.spyOn(accountApi, "getNotificationPreferences").mockResolvedValue({
        data: TELEGRAM_ONLY,
        error: null,
      } as never);
      mount(AccountNotificationsSection, {
        global: { plugins: [createPinia()] },
        attachTo: page,
      });
      await vi.advanceTimersByTimeAsync(0);

      expect(observers[0].gone).toBe(false);
      await vi.advanceTimersByTimeAsync(5000);
      expect(observers[0].gone).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });

  it("lets the page go when it leaves the page", async () => {
    const wrapper = await arrive("/account#notifications");

    wrapper.unmount();

    expect(observers[0].gone).toBe(true);
  });

  it("leaves the scroll alone when the address names no section of it", async () => {
    await arrive("/account");

    expect(observers).toHaveLength(0);
    expect(scrolled).not.toHaveBeenCalled();
  });
});
