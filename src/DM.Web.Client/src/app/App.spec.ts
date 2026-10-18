/**
 * @vitest-environment jsdom
 */

/**
 * The shell owns both header badges, and neither has a reading of its own: a
 * push arrives or the number stands still. Two of its rules about that are
 * branches nothing on the screen shows.
 *
 * A tab left in the background loses pushes for good - the browser may close
 * the socket and the hub keeps no backlog - so returning to the tab has to
 * re-read the counts, or a reader who comes back after lunch is looking at the
 * numbers from before it.
 *
 * The other way round for the global chat: its three events reach every open
 * connection, guests included, and store no notification for anybody. Sending
 * them to the bell turned one busy chat into a request per message from every
 * tab on the site, and re-read a number that had not moved.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { useUiStore } from "@/shared/stores/ui";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";

const stubs = vi.hoisted(() => ({
  blank: { render: () => null },
  // The furniture that takes the inert of an open column: an element of its
  // own is what the attribute lands on.
  header: { template: "<header class='header-stub' />" },
  footer: { template: "<footer class='footer-stub' />" },
  settings: { template: "<div class='settings-stub' />" },
  // The menu of the header, which leads the left view.
  menu: { template: "<nav class='main-menu-stub' />" },
  // The right column of the site, which the right view holds.
  right: { template: "<nav class='right-sidebar-stub' />" },
}));

/** Every mount of the statistics, wherever the shell puts them. */
const statistics = vi.hoisted(() => ({
  mounted: 0,
  stub: {
    template: "<div class='stats-stub' />",
    mounted() {
      statistics.mounted += 1;
    },
  },
}));

/** The width the shell is told it has, as useShellMode would read it. */
const width = vi.hoisted(() => ({
  mode: null as unknown as { value: "desktop" | "tablet" },
}));

vi.mock("@/shared/lib/composables/useShellMode", async () => {
  const { ref, computed } = await import("vue");
  const mode = ref<"desktop" | "tablet">("desktop");
  width.mode = mode;
  return {
    useShellMode: () => ({
      narrow: computed(() => mode.value !== "desktop"),
    }),
  };
});

/**
 * The route the shell reads: its address, whether a letter led to it, and the
 * outlets of its records, the right one among them or not.
 */
const where = vi.hoisted(() => ({
  route: null as unknown as {
    path: string;
    fullPath: string;
    meta: { fromLetter?: boolean };
    matched: { components: Record<string, object> }[];
  },
}));

/** The records of a page of the shell, which has the right column. */
const withRight = () => [{ components: { left: {}, right: {}, page: {} } }];
/** The records of the messenger or of a room of posts: no right outlet. */
const withoutRight = () => [{ components: { left: {}, page: {} } }];

const badges = vi.hoisted(() => ({
  messaging: { fetchUnreadCount: vi.fn() },
  notification: { fetchUnreadCount: vi.fn() },
}));

/**
 * The socket the shell subscribes to. Built inside the factory because the
 * connected flag has to be a real ref for the watcher over it.
 */
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
// The shell only calls it; what it offers and when is its own spec.
vi.mock("@/features/account-time-zone", () => ({ useTimeZoneCheck: vi.fn() }));

// meta is always there on a real route, and the shell reads it to decide
// whether a page from a letter keeps its sidebars.
vi.mock("vue-router", async () => {
  const { reactive } = await import("vue");
  where.route = reactive({
    path: "/",
    fullPath: "/",
    meta: {},
    matched: [{ components: { left: {}, right: {}, page: {} } }],
  });
  return { useRoute: () => where.route };
});

// The shell's own furniture: nothing here reads a badge, and rendering it
// would drag half the application into a test about two counters.
vi.mock("vue-final-modal", () => ({ ModalsContainer: stubs.blank }));
vi.mock("@/widgets/header", () => ({
  Header: stubs.header,
  MainMenu: stubs.menu,
  SiteStatistics: statistics.stub,
}));
vi.mock("@/widgets/sidebar", () => ({ RightSidebar: stubs.right }));
vi.mock("@/widgets/footer", () => ({ Footer: stubs.footer }));
vi.mock("@/shared/ui/Toast", () => ({ ToastContainer: stubs.blank }));
vi.mock("@/shared/ui/ScrollNav", () => ({ ScrollNav: stubs.blank }));
// The column of a narrow screen, as the shell hands it over: out or in, under
// which name, what its bar holds and what it holds, in the one element that
// scrolls, handed out as the column hands it out.
vi.mock("@/shared/ui/Drawer", async () => {
  const { defineComponent, ref } = await import("vue");
  return {
    MobileDrawer: defineComponent({
      props: ["open", "label"],
      setup(_, { expose }) {
        const content = ref<HTMLElement | null>(null);
        expose({ content });
        return { content };
      },
      template:
        "<aside class='drawer-stub' :data-open='open' :data-label='label'><div class='bar-stub'><slot name='bar' /></div><div ref='content' class='content-stub'><slot /></div></aside>",
    }),
  };
});
vi.mock("@/shared/ui/SiteSettings", () => ({
  SiteSettingsPanel: stubs.settings,
}));

import App from "./App.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

let shell: VueWrapper | null = null;
let pinia = createPinia();

beforeEach(() => {
  pinia = createPinia();
  setActivePinia(pinia);
  width.mode.value = "desktop";
  where.route.path = "/";
  where.route.fullPath = "/";
  where.route.meta = {};
  where.route.matched = withRight();
  statistics.mounted = 0;
});

/** Mounts the shell and drops the counts it reads on start. */
async function mountShell() {
  shell = mount(App, { global: { plugins: [pinia] } });
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

    // The listener sits on document, which outlives the component: left behind,
    // it would keep calling into the stores of a shell that is no longer there
    // on every tab switch, once per mount that ever happened.
    setHidden(false);
    expect(badges.messaging.fetchUnreadCount).not.toHaveBeenCalled();
    expect(badges.notification.fetchUnreadCount).not.toHaveBeenCalled();
  });

  // The start of the page settles who the viewer is through the one answer
  // the route guard shares, so a guest with nothing stored costs one request
  // and not one per asker. Calling the refresh directly skipped the question
  // whenever the browser held nobody.
  it("settles who the viewer is once when it starts", async () => {
    await mountShell();

    expect(session.restoreSession).toHaveBeenCalledTimes(1);
    expect(session.fetchUser).not.toHaveBeenCalled();
  });

  // The handler is not tied to the outcome of the first connect: a socket that
  // failed to come up on the first attempt can come up on a later one, and the
  // two badges have no polling of their own to fall back on.
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

/**
 * Where the two side columns stand at each width, and what the column out
 * over the page does to the rest of it. Below the shell breakpoint, a phone
 * included, both columns are the two views of one column that slides in from
 * the left: the left view led by the menu of the header, the right one led by
 * its statistics, and tabs in its bar switch them (owner, 13 October 2026:
 * one button, the left column, and a way to the right one and back). The four
 * blocks that end the right column stay in it (RightSidebar), and nothing of
 * it stands under the page; the settings of the site are in neither view:
 * they are the gear's.
 *
 * A route mounts the right column or not (router.ts): the messenger lays its
 * list in its place and the room of posts is read across its width. Such a
 * page has no right column on a desktop, an empty one included, and the right
 * view of a narrow screen still holds the right column of the site.
 */
describe("the side columns of the shell", () => {
  afterEach(() => {
    shell?.unmount();
    shell = null;
  });

  const at = async (
    mode: "desktop" | "tablet",
    { letter = false, right = true } = {},
  ) => {
    width.mode.value = mode;
    where.route.meta = letter ? { fromLetter: true } : {};
    where.route.matched = right ? withRight() : withoutRight();
    return mountShell();
  };

  const drawer = (wrapper: VueWrapper) => wrapper.find(".drawer-stub");
  const view = (wrapper: VueWrapper, side: "left" | "right") =>
    drawer(wrapper).get(`.sidebar-${side}[role="tabpanel"]`);
  /** Whether a view is the one on screen: the other is kept, hidden. */
  const shown = (wrapper: VueWrapper, side: "left" | "right") =>
    (view(wrapper, side).element as HTMLElement).style.display !== "none";
  const tabs = (wrapper: VueWrapper) =>
    drawer(wrapper).findAll(".bar-stub [role='tab']");
  const tab = (wrapper: VueWrapper, label: string) =>
    tabs(wrapper).find((one) => one.text() === label)!;

  it("stands both columns on the page of a desktop", async () => {
    const wrapper = await at("desktop");

    expect(drawer(wrapper).exists()).toBe(false);
    expect(wrapper.find("aside.sidebar-left").exists()).toBe(true);
    expect(wrapper.find("aside.sidebar-right").exists()).toBe(true);
    // The menu and the statistics are the header's there.
    expect(wrapper.find(".main-menu-stub").exists()).toBe(false);
    expect(statistics.mounted).toBe(0);
  });

  it("stands no right column on a desktop where the route mounts none", async () => {
    const wrapper = await at("desktop", { right: false });

    expect(wrapper.find("aside.sidebar-left").exists()).toBe(true);
    expect(wrapper.find("aside.sidebar-right").exists()).toBe(false);
    // The page is what stands right of the left column.
    const parts = [...wrapper.get(".content-body").element.children];
    expect(parts.map((part) => part.tagName.toLowerCase())).toEqual([
      "aside",
      "main",
    ]);
  });

  it("has one column on a narrow screen, named after its button", async () => {
    const wrapper = await at("tablet");

    expect(wrapper.findAll(".drawer-stub")).toHaveLength(1);
    expect(drawer(wrapper).attributes("data-label")).toBe("Меню");
    expect(wrapper.find("aside.sidebar-left").exists()).toBe(false);
    expect(wrapper.find("aside.sidebar-right").exists()).toBe(false);
  });

  it("switches its two views by the tabs of the site in its bar", async () => {
    const wrapper = await at("tablet");

    expect(tabs(wrapper).map((one) => one.text())).toEqual(["Меню", "О сайте"]);
    expect(drawer(wrapper).find(".bar-stub .tabs--headings").exists()).toBe(
      true,
    );
    // Each tab names the view it shows, and the view is labelled by it.
    for (const side of ["left", "right"] as const) {
      const panel = view(wrapper, side);
      const owner = tabs(wrapper).find(
        (one) => one.attributes("aria-controls") === panel.attributes("id"),
      );
      expect(owner, side).toBeDefined();
      expect(panel.attributes("aria-labelledby")).toBe(owner!.attributes("id"));
    }

    useUiStore().togglePanel("left");
    await nextTick();
    expect(shown(wrapper, "left")).toBe(true);
    expect(shown(wrapper, "right")).toBe(false);

    await tab(wrapper, "О сайте").trigger("click");
    expect(useUiStore().openedPanel).toBe("right");
    expect(shown(wrapper, "left")).toBe(false);
    expect(shown(wrapper, "right")).toBe(true);
    expect(drawer(wrapper).attributes("data-open")).toBe("true");

    await tab(wrapper, "Меню").trigger("click");
    expect(useUiStore().openedPanel).toBe("left");
    expect(shown(wrapper, "left")).toBe(true);
  });

  it("leads the left view with the menu of the header, and the column after it", async () => {
    const wrapper = await at("tablet");
    const left = view(wrapper, "left");

    expect(left.element.firstElementChild?.classList).toContain(
      "main-menu-stub",
    );
    expect(left.find('router-view[name="left"]').exists()).toBe(true);
    expect(left.find(".stats-stub").exists()).toBe(false);
  });

  it("leads the right view with the statistics of the header, and the column after them", async () => {
    const wrapper = await at("tablet");
    const right = view(wrapper, "right");

    expect(right.element.firstElementChild?.classList).toContain("stats-stub");
    expect(right.find(".right-sidebar-stub").exists()).toBe(true);
    expect(right.find(".main-menu-stub").exists()).toBe(false);
    // Once: two copies of the statistics would stop each other's polling.
    expect(statistics.mounted).toBe(1);
    expect(wrapper.findAll(".stats-stub")).toHaveLength(1);
  });

  it("holds the right column of the site in the right view where the route mounts none", async () => {
    const wrapper = await at("tablet", { right: false });
    const right = view(wrapper, "right");

    expect(right.element.firstElementChild?.classList).toContain("stats-stub");
    expect(right.find(".right-sidebar-stub").exists()).toBe(true);
    expect(drawer(wrapper).findAll(".right-sidebar-stub")).toHaveLength(1);
  });

  it("opens on the left view however it was closed", async () => {
    const wrapper = await at("tablet");

    useUiStore().openPanel("right");
    await nextTick();
    useUiStore().closePanel();
    await nextTick();
    // On its way out the column keeps the view it had.
    expect(shown(wrapper, "right")).toBe(true);

    useUiStore().togglePanel("left");
    await nextTick();
    expect(shown(wrapper, "left")).toBe(true);
    expect(shown(wrapper, "right")).toBe(false);
  });

  // The two views scroll in the one element of the column. A view taken over
  // at the depth the other was left at opened the statistics above the top of
  // the screen, in the middle of the polls.
  it("keeps the scroll of each view apart", async () => {
    const wrapper = await at("tablet");
    const content = drawer(wrapper).get(".content-stub").element as HTMLElement;
    const switchTo = async (label: string) => {
      await tab(wrapper, label).trigger("click");
      await flushPromises();
    };

    useUiStore().togglePanel("left");
    await nextTick();
    content.scrollTop = 568;

    // A view not seen yet opens at its top.
    await switchTo("О сайте");
    expect(content.scrollTop).toBe(0);
    content.scrollTop = 120;

    // Each comes back where it was left.
    await switchTo("Меню");
    expect(content.scrollTop).toBe(568);
    await switchTo("О сайте");
    expect(content.scrollTop).toBe(120);

    // Closed on the right view, the column opens on the left one at its own
    // place.
    useUiStore().closePanel();
    await nextTick();
    useUiStore().togglePanel("left");
    await flushPromises();
    expect(shown(wrapper, "left")).toBe(true);
    expect(content.scrollTop).toBe(568);
  });

  it("has the settings of the site in neither view", async () => {
    const wrapper = await at("tablet");

    expect(wrapper.find(".settings-stub").exists()).toBe(false);
  });

  it("stands nothing of the right column under the page", async () => {
    const wrapper = await at("tablet");

    // The header, the page and its columns, and then the footer.
    expect(wrapper.find(".sidebar-tail").exists()).toBe(false);
    const parts = [...wrapper.get(".content-wrapper").element.children];
    expect(parts.map((part) => part.className)).toEqual([
      "header-stub",
      "content-body",
    ]);
  });

  it("is laid out no narrower than a phone lays the page out", () => {
    // The root of the page holds the narrowest layout (index.html): a window
    // narrower than that scrolls it sideways instead of laying it out anew.
    const source = readFileSync(resolve(HERE, "App.vue"), "utf8");
    const narrowest = /\$narrowest-layout:\s*(\d+)px/.exec(source)?.[1];
    expect(narrowest).toBe("706");
    expect(source).toMatch(
      /\.content-container\n(?:[ \t]+.*\n)*?[ \t]+min-width: \$narrowest-layout\n/,
    );
  });

  describe("on a page a letter leads to", () => {
    it("drops both columns on a desktop", async () => {
      const wrapper = await at("desktop", { letter: true });

      expect(wrapper.find("aside").exists()).toBe(false);
    });

    // The page holds its card alone, as on a desktop: the column holds the
    // menu and the statistics, which the header of a desktop has, and none of
    // the columns' blocks.
    it("keeps the menu and the statistics alone in the column of a narrow screen", async () => {
      const wrapper = await at("tablet", { letter: true });

      expect(view(wrapper, "left").find(".main-menu-stub").exists()).toBe(true);
      expect(view(wrapper, "right").find(".stats-stub").exists()).toBe(true);
      expect(drawer(wrapper).find("router-view").exists()).toBe(false);
      expect(drawer(wrapper).find(".right-sidebar-stub").exists()).toBe(false);
    });
  });

  describe("the column out over the page", () => {
    const furniture = (wrapper: VueWrapper) => [
      wrapper.get(".header-stub"),
      wrapper.get("main"),
      wrapper.get(".footer-stub"),
      wrapper.get(".skip-link"),
    ];

    /** jsdom has no `inert` property, so Vue writes "false" out as well. */
    const isInert = (attribute: string | undefined) =>
      attribute !== undefined && attribute !== "false";

    it("takes focus and presses from the rest of the shell, on either view", async () => {
      for (const side of ["left", "right"] as const) {
        const wrapper = await at("tablet");

        for (const part of furniture(wrapper))
          expect(isInert(part.attributes("inert")), side).toBe(false);

        useUiStore().openPanel(side);
        await nextTick();
        expect(drawer(wrapper).attributes("data-open"), side).toBe("true");
        for (const part of furniture(wrapper))
          expect(isInert(part.attributes("inert")), side).toBe(true);
        wrapper.unmount();
        shell = null;
        useUiStore().closePanel();
      }
    });

    it("goes back on a change of address", async () => {
      await at("tablet");
      useUiStore().openPanel("left");

      where.route.path = "/forum";
      where.route.fullPath = "/forum";
      await flushPromises();

      expect(useUiStore().openedPanel).toBeNull();
    });

    it("goes back when the screen widens past the shell breakpoint", async () => {
      const wrapper = await at("tablet");
      useUiStore().openPanel("right");
      await nextTick();

      width.mode.value = "desktop";
      await flushPromises();

      expect(useUiStore().openedPanel).toBeNull();
      expect(isInert(wrapper.get("main").attributes("inert"))).toBe(false);
    });

    it("never stands out on a desktop", async () => {
      await at("desktop");

      useUiStore().togglePanel("left");
      await nextTick();

      expect(useUiStore().openedPanel).toBeNull();
    });
  });
});
