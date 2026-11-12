/**
 * @vitest-environment jsdom
 */

/**
 * The shell owns both header badges: they change only on a push, so a tab back
 * on screen re-reads them, and the broadcast events of the global chat do not.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { type VueWrapper } from "@vue/test-utils";
import { createPinia } from "pinia";
import { mountPage, type AddressRecord } from "@/shared/lib/testing/mountPage";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";

const stubs = vi.hoisted(() => ({
  blank: { render: () => null },
  header: { template: "<header class='header-stub' />" },
  footer: { template: "<footer class='footer-stub' />" },
}));

/** The views of a page of the shell, which has the right column. */
const withRight = () => ({
  left: stubs.blank,
  right: stubs.blank,
  page: stubs.blank,
});
/** The views of the messenger or of a room of posts: no right outlet. */
const withoutRight = () => ({ left: stubs.blank, page: stubs.blank });

const badges = vi.hoisted(() => ({
  messaging: { fetchUnreadCount: vi.fn() },
  notification: { fetchUnreadCount: vi.fn() },
}));

// The connected flag is built inside the factory: the watcher needs a real ref.
const socket = vi.hoisted(() => ({
  handlers: [] as ((notification: SignalRNotification) => void)[],
  connect: vi.fn(async () => true),
}));

vi.mock("@/shared/lib/composables/useSignalR", async () => {
  const { ref } = await import("vue");
  const isConnected = ref(false);
  return {
    useGlobalSignalR: () => ({
      connect: socket.connect,
      disconnect: async () => undefined,
      onNotification: (
        handler: (notification: SignalRNotification) => void,
      ) => {
        socket.handlers.push(handler);
        return () => undefined;
      },
      isConnected,
    }),
  };
});

vi.mock("@/entities/message", () => ({
  useMessagingStore: () => badges.messaging,
}));

vi.mock("@/entities/notification", () => ({
  useNotificationStore: () => badges.notification,
}));

/** Who the viewer is: asked by the shell once, through the shared answer. */
const session = vi.hoisted(() => ({
  restoreSession: vi.fn(),
  fetchUser: vi.fn(),
  viewer: { user: null, isAuthenticated: false },
}));

vi.mock("@/entities/user", () => ({
  useAuthStore: () => session.viewer,
  fetchUser: session.fetchUser,
  restoreSession: session.restoreSession,
}));

vi.mock("@/shared/lib/scroll", () => ({ setScrollContainer: vi.fn() }));
vi.mock("@/features/account-time-zone", () => ({ useTimeZoneCheck: vi.fn() }));

// The furniture of the shell: rendered, it would drag half the application in.
vi.mock("@/widgets/header", () => ({ Header: stubs.header }));
vi.mock("@/widgets/footer", () => ({ Footer: stubs.footer }));
vi.mock("@/shared/ui/Toast", () => ({ ToastContainer: stubs.blank }));
vi.mock("@/shared/ui/ScrollNav", () => ({ ScrollNav: stubs.blank }));

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import App from "./App.vue";

let shell: VueWrapper | null = null;
/** Mounts the shell at a page with the given record and drops the counts it reads on start. */
async function mountShell(page: AddressRecord = { components: withRight() }) {
  ({ wrapper: shell } = await mountPage(App, "/page", {
    routes: [["page", "/page", page]],
    pinia: createPinia(),
  }));
  await Promise.resolve();
  badges.messaging.fetchUnreadCount.mockClear();
  badges.notification.fetchUnreadCount.mockClear();
  return shell;
}

/** Puts the tab on screen or off it, the way the browser reports it. */
function setHidden(hidden: boolean) {
  Object.defineProperty(document, "hidden", {
    value: hidden,
    configurable: true,
  });
  document.dispatchEvent(new Event("visibilitychange"));
}

const push = (eventType: NotificationType) =>
  socket.handlers.forEach((handler) =>
    handler({ id: "n1", eventType, payload: {} }),
  );

describe("the application shell", () => {
  beforeEach(() => {
    socket.handlers.length = 0;
    vi.clearAllMocks();
  });

  afterEach(() => {
    shell?.unmount();
    shell = null;
    setHidden(false);
  });

  it("re-reads both badges when the tab returns to the screen", async () => {
    await mountShell();

    setHidden(true);
    expect(badges.messaging.fetchUnreadCount).not.toHaveBeenCalled();
    expect(badges.notification.fetchUnreadCount).not.toHaveBeenCalled();

    setHidden(false);
    expect(badges.messaging.fetchUnreadCount).toHaveBeenCalledTimes(1);
    expect(badges.notification.fetchUnreadCount).toHaveBeenCalledTimes(1);
  });

  it("stops listening for the tab once the shell is gone", async () => {
    await mountShell();

    setHidden(true);
    shell?.unmount();
    shell = null;

    // The listener sits on document, which outlives the component.
    setHidden(false);
    expect(badges.messaging.fetchUnreadCount).not.toHaveBeenCalled();
    expect(badges.notification.fetchUnreadCount).not.toHaveBeenCalled();
  });

  // Through the answer the route guard shares: one request, not one per asker.
  it("settles who the viewer is once when it starts", async () => {
    await mountShell();

    expect(session.restoreSession).toHaveBeenCalledTimes(1);
    expect(session.fetchUser).not.toHaveBeenCalled();
  });

  it("hears the pushes of a signed-in viewer whose first connect failed", async () => {
    session.viewer.isAuthenticated = true;
    socket.connect.mockResolvedValueOnce(false);

    try {
      await mountShell();
      expect(socket.connect).toHaveBeenCalledOnce();

      push(NotificationType.NewTopicComment);
      push(NotificationType.NewMessage);
    } finally {
      session.viewer.isAuthenticated = false;
    }

    expect(badges.notification.fetchUnreadCount).toHaveBeenCalledTimes(1);
    expect(badges.messaging.fetchUnreadCount).toHaveBeenCalledTimes(1);
  });

  it("leaves the bell alone on the broadcast events", async () => {
    await mountShell();

    push(NotificationType.NewGlobalChatMessage);
    push(NotificationType.GlobalChatEventStarted);
    push(NotificationType.GlobalChatEventEnded);

    expect(badges.notification.fetchUnreadCount).not.toHaveBeenCalled();

    // An event that does leave a notification behind still re-reads it.
    push(NotificationType.NewTopicComment);
    expect(badges.notification.fetchUnreadCount).toHaveBeenCalledTimes(1);
  });
});

describe("the side columns of the shell", () => {
  afterEach(() => {
    shell?.unmount();
    shell = null;
  });

  const at = async ({ letter = false, right = true } = {}) =>
    mountShell({
      meta: letter ? { fromLetter: true } : {},
      components: right ? withRight() : withoutRight(),
    });

  it("stands both columns on the page", async () => {
    const wrapper = await at();

    expect(wrapper.find("aside.sidebar-left").exists()).toBe(true);
    expect(wrapper.find("aside.sidebar-right").exists()).toBe(true);
  });

  it("stands no right column where the route mounts none", async () => {
    const wrapper = await at({ right: false });

    expect(wrapper.find("aside.sidebar-left").exists()).toBe(true);
    expect(wrapper.find("aside.sidebar-right").exists()).toBe(false);
    // The page is what stands right of the left column.
    const parts = [...wrapper.get(".content-body").element.children];
    expect(parts.map((part) => part.tagName.toLowerCase())).toEqual([
      "aside",
      "main",
    ]);
  });

  it("drops both columns on a page a letter leads to", async () => {
    const wrapper = await at({ letter: true });

    expect(wrapper.find("aside").exists()).toBe(false);
  });

  // A page with columns grows past the window as its blocks come; a footer standing at the window's foot meanwhile is pushed off.
  it("holds the footer below the window from the first frame where the columns stand", async () => {
    const wrapper = await at({ right: false });
    expect(wrapper.get(".content-wrapper").classes()).toContain("with-columns");
    const css = ruleMap(
      componentCss(resolve(dirname(fileURLToPath(import.meta.url)), "App.vue")),
    );
    expect(css.get(".content-wrapper.with-columns")?.["min-height"]).toBe(
      "100vh",
    );
  });

  it("leaves the footer at the window's foot on a page without columns", async () => {
    const wrapper = await at({ letter: true });

    expect(wrapper.get(".content-wrapper").classes()).not.toContain(
      "with-columns",
    );
  });
});
