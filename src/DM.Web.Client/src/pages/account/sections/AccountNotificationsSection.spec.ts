/**
 * @vitest-environment jsdom
 */

/**
 * The notifications block of the account page: one grey panel, as "Сессии"
 * has, and in it a framed row a channel.
 *
 * There used to be two blocks about the same two bots. The upper one wrote the
 * state as a pill, "Подключен" or "Не подключен", and offered "Подключить" on a
 * button that only raised an event nobody listened to: the page mounted the
 * block bare, so the press did nothing at all. The lower one, "Привязка ботов",
 * did the linking, and offered "Подключить Telegram" next to a Telegram the
 * block above called connected. Now the row of a channel links it itself:
 * "Подключить" asks the server for a code and lays the steps out under the row.
 *
 * The hierarchy reads the way the sessions above it read: the section, in it
 * a framed row a channel, the name of the channel with its state under it in
 * grey on the left ("Подключен", "Не подключен"), the one press of the row on
 * the right, and under them what the channel sends, a column of boxes under
 * the bold caption "Что присылать". The state is a grey line, not a pill and
 * not green.
 *
 * The block also has no doors a parent can knock on. It reads itself once on
 * mount and redraws itself after its own presses, and no other block changes
 * what it shows; asserted through what a parent would see, the compiled
 * `emits` list and the public instance a template ref gets hold of.
 *
 * Nothing in the steps is copied by a button: the reader selects what he
 * needs himself. Telegram takes the code in the start parameter of a t.me
 * link, so its steps give that link, a plain one whose words are the address;
 * Discord is sent the command, words of the step, whole and selectable. The
 * code runs out on a visible countdown, which takes the steps away and puts
 * "Подключить" back, with no plate saying the same thing.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick, ref } from "vue";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { NotificationPreferences } from "@/shared/api/models/account";
import AccountNotificationsSection from "./AccountNotificationsSection.vue";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { compileSass, sassBlocks, ruleMap } from "@/shared/lib/testing/styles";

enableAutoUnmount(afterEach);

type Block = InstanceType<typeof AccountNotificationsSection>;

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** Telegram linked, with its boxes; Discord not linked. */
const TELEGRAM_ONLY: NotificationPreferences = {
  telegram: {
    connected: true,
    enabled: true,
    enabledCategories: ["Messages"],
  },
  discord: undefined,
};

const NOTHING_LINKED: NotificationPreferences = {
  telegram: undefined,
  discord: undefined,
};

/** A parent holding the block the way AccountPage holds the blocks it re-reads. */
const Host = defineComponent({
  setup(_, { expose }) {
    const block = ref<Block | null>(null);
    expose({ block });
    return () => h(AccountNotificationsSection, { ref: block });
  },
});

/** Mounts the block over what the server holds of the two channels. */
async function render(held: NotificationPreferences = TELEGRAM_ONLY) {
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
    // The two it declared were the dead "Подключить": a press that raised an
    // event the page never listened to.
    it("declares no event a parent would have to answer", () => {
      const emits = (AccountNotificationsSection as { emits?: unknown }).emits;
      expect(emits ?? []).toEqual([]);
    });

    it("hands a parent no handle to re-read it through", async () => {
      vi.spyOn(accountApi, "getNotificationPreferences").mockResolvedValue({
        data: NOTHING_LINKED,
        error: null,
      } as never);
      const host = mount(Host, { global: { plugins: [createPinia()] } });
      await flushPromises();

      const block = (host.vm as unknown as { block: Block | null }).block;
      expect(block).not.toBeNull();
      expect("loadPreferences" in (block as object)).toBe(false);
    });

    it("still reads itself once on mount", async () => {
      await render();

      expect(accountApi.getNotificationPreferences).toHaveBeenCalledTimes(1);
    });

    // The page of notifications leads here with a link to the section, so the
    // section answers to the address of that link.
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

    // The second block offered to link a channel the first one called linked.
    it("is the one block about the bots, and never offers to link a linked channel", async () => {
      const wrapper = await render();

      expect(wrapper.findAll(".channel-item")).toHaveLength(2);
      expect(wrapper.find(".channel-card").exists()).toBe(false);
      expect(card(wrapper, "telegram").text()).not.toContain("Подключить");
      expect(wrapper.text()).not.toContain("Привязка ботов");
    });

    // What the channel sends stands under its row: a caption of its own, the
    // boxes in a column under it, and the column named by the caption.
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
      // The other block is left as it was.
      expect(card(wrapper, "telegram").find(".link-process").exists()).toBe(
        false,
      );
      expect(button(wrapper, "telegram", "Подключить").exists()).toBe(true);
    });

    // The bar of time said what the digits beside it already say, and no
    // other place of the site draws one.
    it("counts the time down in digits, with no bar under them", async () => {
      const wrapper = await render(NOTHING_LINKED);
      await startLinking(wrapper, "discord", "482913");

      const discord = card(wrapper, "discord");
      expect(discord.find(".timer-bar").exists()).toBe(false);
      expect(discord.find(".timer-progress").exists()).toBe(false);
      expect(discord.find("[style]").exists()).toBe(false);
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

    // Each press here takes away the control it was made on. Left alone, focus
    // falls to the page and the next Tab starts from the top of it.
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

    // The command is sent from the bot, away from the page: coming back to the
    // page is where the block learns the channel is linked now.
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
    // No copy button: the command is the words of the step, and the reader
    // selects it himself.
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
      expect(discord.find(".copy-btn").exists()).toBe(false);
      expect(discord.text()).not.toContain("Скопировать");
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

  // Telegram takes the code in the start parameter of a t.me link and hands
  // it to the bot as "/start <code>" when Start is pressed in the chat the
  // link opens. So its steps give that link in place of the command.
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

    // A plain link: its words are the address it leads to, so a reader who
    // has Telegram on another device copies them as any other words.
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
      expect(telegram.text()).not.toContain("/connect");
      expect(telegram.text()).not.toContain("Скопировать");
      expect(telegram.findAll("button").map((one) => one.text())).toEqual([
        "Отмена",
      ]);
    });
  });

  describe("the code running out", () => {
    it("puts the row back to its offer, and says nothing over it", async () => {
      // Only the countdown and the clock it is measured against: setTimeout
      // stays real, because flushPromises above is scheduled on it.
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

// The notifications page links to "/account#notifications". The router
// scrolls to the section as soon as the page is drawn, while the blocks above
// it still say "Загрузка...": the section is the last of the page, the scroll
// stops at the foot of a page that is still short, and the blocks above then
// grow and push the section down past the window (at 1366 the link landed on
// "Смена имени пользователя"). jsdom lays nothing out, so the page growing is
// a resize observer told by hand, and the scroll is a spy.
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
          // A disconnected observer reports nothing, as a real one does.
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
          // A resize observer reports the size it starts from.
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
    // The page the section stands in: what grows above it is in there.
    expect(observers[0].watched).toEqual([wrapper.element.parentElement]);
    expect(scrolled).toHaveBeenCalledWith({ block: "start" });
    const before = ownScrolls(wrapper);

    observers[0].grow();
    observers[0].grow();

    expect(ownScrolls(wrapper)).toBe(before + 2);
  });

  // The reader has the page from the first turn of the wheel, the first touch
  // and the first key: the section never pulls the page back from under him.
  it.each(["wheel", "touchstart", "keydown", "pointerdown"])(
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

// jsdom lays nothing out and applies no scoped style, so the look is read off
// the stylesheet the build compiles.
describe("the look of the notifications block", () => {
  const file = join(
    dirname(fileURLToPath(import.meta.url)),
    "AccountNotificationsSection.vue",
  );
  const css = () => compileSass(sassBlocks(file), file);

  // The panel and the rows of "Сессии" above: a grey panel, and in it a row
  // a channel framed by the border of the site, 8 to the next.
  it("draws the channels as framed rows of a grey panel, as the sessions are", () => {
    const rules = ruleMap(css());
    expect(rules.get(".notifications-content")?.["background-color"]).toBe(
      "var(--bg-element)",
    );
    const row = rules.get(".channel-item");
    expect(row?.["border"]).toBe("1px solid var(--border)");
    expect(row?.["padding"]).toBe("8px 16px");
    expect(rules.get(".channels")?.["gap"]).toBe("8px");
  });

  // The name and its state on the left, the press on the right, as the
  // device of a session and its "Завершить" stand.
  it("heads a row with the name over its state, and the press beside them", () => {
    const rules = ruleMap(css());
    expect(rules.get(".channel-head")?.["grid-template-columns"]).toBe(
      "minmax(0, 1fr) auto",
    );
    const name = rules.get(".channel-name");
    expect(name?.["font-size"]).toBe("16px");
    expect(name?.["font-weight"]).toBe("500");
    const state = rules.get(".channel-state");
    expect(state?.["font-size"]).toBe("14px");
    expect(state?.["color"]).toBe("var(--text-muted)");
  });

  // Green is for what is good (the owner, 12 October 2026), and a linked bot
  // is a fact, not a good thing.
  it("paints no word green", () => {
    expect(css()).not.toContain("var(--accent-green)");
  });

  it("captions what the channel sends in bold 14, as the blacklist captions its boxes", () => {
    const caption = ruleMap(css()).get(".categories-caption");
    expect(caption?.["font-size"]).toBe("14px");
    expect(caption?.["font-weight"]).toBe("700");
  });

  // Words of the step: no frame, no fill, never broken in two, and a tap
  // selects all of it.
  it("sets the command as words of the line, never broken in two", () => {
    const command = ruleMap(css()).get(".command");
    expect(command?.["white-space"]).toBe("nowrap");
    expect(command?.["font-family"]).toContain("Courier New");
    expect(command?.["user-select"]).toBe("all");
    expect(command?.["border"]).toBeUndefined();
    expect(command?.["background-color"]).toBeUndefined();
  });

  // The link of the Telegram step is an address, and an address broken over
  // two lines reads as two.
  it("keeps the start link in one piece, as the command is", () => {
    expect(ruleMap(css()).get(".start-link")?.["white-space"]).toBe("nowrap");
  });

  // A phone shows the row as the full version does.
  it("has no form of its own for a narrow screen", () => {
    const compiled = css();
    expect(compiled).not.toContain("@container");
    expect(compiled).not.toContain("container-type");
    expect(compiled).not.toContain("@media");
  });
});
