/**
 * @vitest-environment jsdom
 */

/**
 * ChatView, the menu of the correspondence: its two questions, the clearing of
 * the history and the leaving of a group, and where the menu hangs.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { api, nav } from "./ChatView.doubles";
import {
  closeChat,
  feed,
  messagePage,
  openChat,
  resetChatView,
  shownIds,
} from "./ChatView.fixtures";
import { problem } from "@/shared/lib/testing/problem";

vi.mock("@/entities/message/api/messagingApi", async () =>
  (await import("./ChatView.doubles")).messagingApi(),
);
vi.mock("@/shared/lib/composables/useSignalR", async () =>
  (await import("./ChatView.doubles")).signalR(),
);
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeComposer(
    (await import("./ChatView.doubles")).clearSpy,
  ),
);
vi.mock("vue-router", async () =>
  (await import("./ChatView.doubles")).vueRouter(),
);

beforeEach(resetChatView);
afterEach(closeChat);

// Each line stands where the server allows it; the clearing and the leaving ask first.
describe("ChatView, the menu of the correspondence", () => {
  const trigger = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find('.chat-strip button[aria-label="Действия с перепиской"]');

  async function openMenu(wrapper: ReturnType<typeof mount>) {
    await trigger(wrapper).trigger("click");
    await nextTick();
  }

  const items = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findAll('[role="menuitem"]').map((item) => item.text());

  const direct = (viewer: Record<string, boolean>) => ({
    data: {
      id: "c1",
      type: "Direct",
      participants: [{ username: "Alice" }, { username: "Bob" }],
      viewer: { canWrite: true, ...viewer },
    },
    error: null,
  });

  it("leads to the other one's profile and clears the history of a conversation of two", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    const wrapper = await openChat();

    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
    await openMenu(wrapper);

    expect(trigger(wrapper).attributes("aria-expanded")).toBe("true");
    expect(items(wrapper)).toEqual(["Перейти в профиль", "Очистить историю"]);
    expect(
      JSON.parse(wrapper.get('a[role="menuitem"]').attributes("data-to")!),
    ).toEqual({ name: "profile", params: { username: "Bob" } });
  });

  it("offers only what the server allows", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: false }));
    const wrapper = await openChat();
    await openMenu(wrapper);

    expect(items(wrapper)).toEqual(["Перейти в профиль"]);
  });

  it("offers the leaving of a group, and no profile in it", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [{ username: "Alice" }, { username: "Bob" }],
        viewer: { canWrite: true, canLeave: true, canClearHistory: true },
      },
      error: null,
    });
    const wrapper = await openChat();
    await openMenu(wrapper);

    expect(items(wrapper)).toEqual(["Очистить историю", "Покинуть группу"]);
  });

  it('has no "…" where there is nothing to do', async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [{ username: "Alice" }],
        viewer: { canWrite: true },
      },
      error: null,
    });
    const wrapper = await openChat();

    expect(trigger(wrapper).exists()).toBe(false);
  });

  it('closes on Escape and gives the focus back to "…"', async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    const wrapper = await openChat({ attached: true });
    await openMenu(wrapper);

    await wrapper.get(".menu-anchor").trigger("keydown", { key: "Escape" });

    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
    expect(document.activeElement).toBe(trigger(wrapper).element);
  });

  // Drawn at the end of the page, the menu goes when the focus leaves "…" and the
  // menu both; a press on what takes no focus is the click's to answer.
  it('closes when the focus leaves "…" and the menu', async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    const wrapper = await openChat({ attached: true });
    await openMenu(wrapper);
    const leave = (from: Element, to: Element | null) =>
      from.dispatchEvent(
        new FocusEvent("focusout", { bubbles: true, relatedTarget: to }),
      );
    const item = wrapper.get('button[role="menuitem"]').element;

    leave(trigger(wrapper).element, item);
    leave(item, null);
    await nextTick();
    expect(wrapper.find('[role="menu"]').exists()).toBe(true);

    leave(item, feed(wrapper));
    await nextTick();
    expect(wrapper.find('[role="menu"]').exists()).toBe(false);

    await openMenu(wrapper);
    leave(trigger(wrapper).element, feed(wrapper));
    await nextTick();
    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
  });

  // Drawn over the page, since the feed's frame clips it; jsdom lays nothing
  // out, so "…", the menu and the window are given their sizes.
  describe("where the menu hangs", () => {
    let restore: (() => void) | null = null;

    afterEach(() => {
      restore?.();
      restore = null;
    });

    /** "…" at 300..310 under a line ending at 40, a menu 170 wide. */
    function lay(wrapper: ReturnType<typeof mount>, windowWidth: number) {
      const button = trigger(wrapper).element as HTMLElement;
      button.getBoundingClientRect = () =>
        ({ left: 300, right: 310, top: 24, bottom: 40 }) as DOMRect;
      const width = Object.getOwnPropertyDescriptor(
        HTMLElement.prototype,
        "offsetWidth",
      )!;
      Object.defineProperty(HTMLElement.prototype, "offsetWidth", {
        configurable: true,
        get() {
          return (this as HTMLElement).classList.contains("conversation-menu")
            ? 170
            : 0;
        },
      });
      Object.defineProperty(document.documentElement, "clientWidth", {
        configurable: true,
        value: windowWidth,
      });
      restore = () => {
        Object.defineProperty(HTMLElement.prototype, "offsetWidth", width);
        delete (document.documentElement as { clientWidth?: number })
          .clientWidth;
      };
    }

    const placed = (wrapper: ReturnType<typeof mount>) =>
      (wrapper.get('[role="menu"]').element as HTMLElement).style;

    it('stands under "…" at its left edge where the window has room', async () => {
      api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
      const wrapper = await openChat({ attached: true });
      lay(wrapper, 1000);
      await openMenu(wrapper);
      await flushPromises();

      expect(placed(wrapper).left).toBe("300px");
      expect(placed(wrapper).top).toBe("44px");
      expect(placed(wrapper).visibility).toBe("");
    });

    it('stands at the right edge of "…" where the window has no room to its right', async () => {
      api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
      const wrapper = await openChat({ attached: true });
      lay(wrapper, 420);
      await openMenu(wrapper);
      await flushPromises();

      expect(placed(wrapper).left).toBe(`${310 - 170}px`);
    });

    it("goes away when the page scrolls under it", async () => {
      api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
      const wrapper = await openChat({ attached: true });
      await openMenu(wrapper);
      await flushPromises();
      expect(wrapper.find('[role="menu"]').exists()).toBe(true);

      document.body.dispatchEvent(new Event("scroll"));
      await nextTick();

      expect(wrapper.find('[role="menu"]').exists()).toBe(false);
    });
  });

  it("asks before it clears, then empties the feed for the reader", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    api.getMessages.mockResolvedValue(messagePage(["m1", "m2"]));
    api.clearHistory.mockResolvedValue({ data: null, error: null });
    const wrapper = await openChat();
    await openMenu(wrapper);

    const clear = wrapper
      .findAll('[role="menuitem"]')
      .find((item) => item.text() === "Очистить историю")!;
    await clear.trigger("click");
    await nextTick();

    const dialog = wrapper.get('[role="alertdialog"]');
    expect(dialog.get(".dialog-title").text()).toBe("Очистить историю?");
    expect(dialog.get(".dialog-message").text()).toBe(
      "Сообщения, написанные до этой минуты, перестанут показываться вам, а у собеседника останутся",
    );
    expect(api.clearHistory).not.toHaveBeenCalled();

    const confirm = dialog
      .findAll("button")
      .find((button) => button.text() === "Очистить")!;
    await confirm.trigger("click");
    await flushPromises();

    expect(api.clearHistory).toHaveBeenCalledWith("c1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(shownIds()).toEqual([]);
    expect(wrapper.text()).toContain("Сообщений пока нет");
    expect(wrapper.find('[role="alertdialog"]').exists()).toBe(false);
  });

  it("keeps the window open with the reason when the clearing is refused", async () => {
    api.getChat.mockResolvedValue(direct({ canClearHistory: true }));
    api.getMessages.mockResolvedValue(messagePage(["m1"]));
    api.clearHistory.mockResolvedValue({ data: null, error: problem(500) });
    const wrapper = await openChat();
    await openMenu(wrapper);
    await wrapper
      .findAll('[role="menuitem"]')
      .find((item) => item.text() === "Очистить историю")!
      .trigger("click");
    await nextTick();

    await wrapper
      .get('[role="alertdialog"]')
      .findAll("button")
      .find((button) => button.text() === "Очистить")!
      .trigger("click");
    await flushPromises();

    expect(wrapper.get(".dialog-refusal").text()).toContain(
      "Не удалось очистить историю",
    );
    expect(shownIds()).toEqual(["m1"]);
  });

  it("asks before it leaves a group, then goes back to the list", async () => {
    api.getChat.mockResolvedValue({
      data: {
        id: "c1",
        type: "Group",
        title: "Партия",
        participants: [{ username: "Alice" }, { username: "Bob" }],
        viewer: { canWrite: true, canLeave: true },
      },
      error: null,
    });
    api.leaveChat.mockResolvedValue({ data: null, error: null });
    const wrapper = await openChat();
    await openMenu(wrapper);
    await wrapper
      .findAll('[role="menuitem"]')
      .find((item) => item.text() === "Покинуть группу")!
      .trigger("click");
    await nextTick();

    const dialog = wrapper.get('[role="alertdialog"]');
    expect(dialog.get(".dialog-title").text()).toBe("Покинуть группу?");
    await dialog
      .findAll("button")
      .find((button) => button.text() === "Покинуть")!
      .trigger("click");
    await flushPromises();

    expect(api.leaveChat).toHaveBeenCalledWith("c1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(nav.push).toHaveBeenCalledWith({ name: "messenger" });
  });
});
