/**
 * @vitest-environment jsdom
 */

/**
 * When a page's chunk is asked for, and when it must not be. Every refusal ends
 * by fetching something real, so a module that stopped prefetching fails it.
 * The app router is faked (a real one pulls every lazy page in);
 * loadRouteLocation is the real one.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";

const fake = vi.hoisted(() => {
  const beforeGuards: Array<() => void> = [];
  const afterHooks: Array<() => void> = [];
  const errorHandlers: Array<() => void> = [];
  const subscribe =
    (list: Array<() => void>) =>
    (handler: () => void): (() => void) => {
      list.push(handler);
      return () => {
        const at = list.indexOf(handler);
        if (at >= 0) list.splice(at, 1);
      };
    };
  return {
    resolve: vi.fn(),
    currentRoute: { value: { path: "/" } },
    beforeGuards,
    afterHooks,
    errorHandlers,
    beforeEach: subscribe(beforeGuards),
    afterEach: subscribe(afterHooks),
    onError: subscribe(errorHandlers),
  };
});

vi.mock("./router", () => ({
  default: {
    resolve: fake.resolve,
    currentRoute: fake.currentRoute,
    beforeEach: fake.beforeEach,
    afterEach: fake.afterEach,
    onError: fake.onError,
  },
}));

import { installRoutePrefetch } from "./routePrefetch";

/** Past the pointer's wait in routePrefetch.ts, in milliseconds. */
const AFTER_HOVER_DWELL = 100;
/** Past the keyboard's, which is the longer of the two. */
const AFTER_FOCUS_DWELL = 200;
/** Inside both of them: a step of a traversal, not an arrival. */
const MID_TRAVERSAL = 40;

/** A route record, shaped the way vue-router keeps one before it is loaded. */
interface FakeRecord {
  path: string;
  components: Record<string, unknown>;
  mods: Record<string, unknown>;
}

/** What the fake router answers `resolve` with. */
const routes = new Map<string, { path: string; matched: FakeRecord[] }>();

/** A record with a lazy component, which vue-router writes back once loaded. */
function lazyRecord(path: string): { record: FakeRecord; load: () => unknown } {
  const load = vi.fn(() => Promise.resolve({ default: { name: "Page" } }));
  return { record: { path, components: { default: load }, mods: {} }, load };
}

/** Teaches the fake router that this address is drawn by these records. */
function serves(address: string, ...records: FakeRecord[]): void {
  routes.set(address, { path: address, matched: records });
}

/** `navigator.connection`, which jsdom does not have until a test says so. */
function connectionReports(info: Record<string, unknown> | undefined): void {
  Object.defineProperty(navigator, "connection", {
    value: info,
    configurable: true,
  });
}

const Nav = defineComponent({
  render() {
    return h("nav", [
      // Crossing onto the icon is a mouseout off the anchor.
      h("a", { href: "/games", id: "games" }, [
        h("span", { id: "games-icon" }, "*"),
        "Игры",
      ]),
      h("a", { href: "/blogs", id: "blogs" }, "Блоги"),
      h("a", { href: "/users/anna", id: "anna" }, "Анна"),
      h("a", { href: "/users/boris", id: "boris" }, "Борис"),
      h("a", { href: "https://example.com/elsewhere", id: "outside" }, "Прочь"),
      h("a", { href: "#top", id: "anchor" }, "Наверх"),
      h("a", { href: "/", id: "home" }, "Главная"),
      h("a", { href: "/blogs", target: "_blank", id: "new-tab" }, "В окне"),
      h("a", { href: "/blogs", download: "", id: "download" }, "Скачать"),
      h("a", { href: "/files/handbook.pdf", id: "file" }, "Файл"),
      // A run of top-level routes, as in the moderation panel.
      h("a", { href: "/moderation/bans", id: "p1" }, "Баны"),
      h("a", { href: "/moderation/warnings", id: "p2" }, "Предупреждения"),
      h("a", { href: "/moderation/tags", id: "p3" }, "Теги"),
      h("button", { id: "not-a-link" }, "Кнопка"),
    ]);
  },
});

let wrapper: VueWrapper;
let uninstall: () => void;
let games: () => unknown;
let blogs: () => unknown;
let profile: () => unknown;
let home: () => unknown;
let notFound: () => unknown;
let panel: Array<() => unknown>;

const startNavigation = (): void => fake.beforeGuards.forEach((g) => g());
const finishNavigation = (): void => fake.afterHooks.forEach((h) => h());
const failNavigation = (): void => fake.errorHandlers.forEach((h) => h());

beforeEach(() => {
  vi.useFakeTimers();
  routes.clear();
  connectionReports(undefined);
  fake.currentRoute.value = { path: "/" };

  const gamesPage = lazyRecord("/games");
  const blogsPage = lazyRecord("/blogs");
  const profilePage = lazyRecord("/users/:username");
  const homePage = lazyRecord("/");
  // router.ts ends with a catch-all, so an unknown address resolves to the 404.
  const notFoundPage = lazyRecord("/:pathMatch(.*)*");
  const panelPages = [
    lazyRecord("/moderation/bans"),
    lazyRecord("/moderation/warnings"),
    lazyRecord("/moderation/tags"),
  ];

  games = gamesPage.load;
  blogs = blogsPage.load;
  profile = profilePage.load;
  home = homePage.load;
  notFound = notFoundPage.load;
  panel = panelPages.map(({ load }) => load);

  serves("/games", gamesPage.record);
  serves("/blogs", blogsPage.record);
  // One record, two addresses.
  serves("/users/anna", profilePage.record);
  serves("/users/boris", profilePage.record);
  serves("/", homePage.record);
  serves("/moderation/bans", panelPages[0].record);
  serves("/moderation/warnings", panelPages[1].record);
  serves("/moderation/tags", panelPages[2].record);

  fake.resolve.mockImplementation(
    (to: string) =>
      routes.get(to) ?? { path: to, matched: [notFoundPage.record] },
  );

  wrapper = mount(Nav, { attachTo: document.body });
  uninstall = installRoutePrefetch();
});

afterEach(() => {
  uninstall();
  wrapper.unmount();
  vi.useRealTimers();
  vi.clearAllMocks();
});

async function settle(ms: number): Promise<void> {
  vi.advanceTimersByTime(ms);
  // Lets the loader's promise chain run.
  await vi.advanceTimersByTimeAsync(0);
}

/** Rests the pointer on a link long enough to mean it. */
async function hover(id: string): Promise<void> {
  await wrapper.get(`#${id}`).trigger("mouseover");
  await settle(AFTER_HOVER_DWELL);
}

/** Takes the pointer off a link, so the next hover is a fresh aim. */
async function unhover(id: string): Promise<void> {
  await wrapper.get(`#${id}`).trigger("mouseout");
}

/** Leaves the keyboard on a link long enough to mean it. */
async function focus(id: string): Promise<void> {
  await wrapper.get(`#${id}`).trigger("focusin");
  await settle(AFTER_FOCUS_DWELL);
}

/** The refusals all end here: proof the module still fetches anything at all. */
async function stillFetches(): Promise<void> {
  await hover("games");
  expect(games).toHaveBeenCalledTimes(1);
}

describe("route prefetch", () => {
  describe("what it fetches", () => {
    it("fetches the page a resting pointer is aimed at", async () => {
      expect(games).not.toHaveBeenCalled();

      await hover("games");

      expect(games).toHaveBeenCalledTimes(1);
    });

    it("waits before believing the pointer, and forgets a link it left", async () => {
      await wrapper.get("#games").trigger("mouseover");
      await unhover("games");
      await settle(AFTER_HOVER_DWELL);
      expect(games).not.toHaveBeenCalled();

      await stillFetches();
    });

    it("does not count an icon inside the link as leaving it", async () => {
      await wrapper.get("#games").trigger("mouseover");
      await wrapper.get("#games").trigger("mouseout", {
        relatedTarget: document.querySelector("#games-icon"),
      });
      await settle(AFTER_HOVER_DWELL);

      expect(games).toHaveBeenCalledTimes(1);
    });

    it("fetches on keyboard focus", async () => {
      await focus("blogs");

      expect(blogs).toHaveBeenCalledTimes(1);
    });

    it("makes focus wait too, so tabbing past a link is not aiming at it", async () => {
      await wrapper.get("#blogs").trigger("focusin");
      await settle(MID_TRAVERSAL);
      expect(blogs).not.toHaveBeenCalled();

      await settle(AFTER_FOCUS_DWELL);
      expect(blogs).toHaveBeenCalledTimes(1);
    });

    it("fetches one page for a pass down a panel, not one per link", async () => {
      for (const id of ["p1", "p2", "p3"]) {
        await wrapper.get(`#${id}`).trigger("focusin");
        await settle(MID_TRAVERSAL);
        await wrapper.get(`#${id}`).trigger("focusout");
      }
      for (const load of panel) expect(load).not.toHaveBeenCalled();

      await focus("p3");
      expect(panel[0]).not.toHaveBeenCalled();
      expect(panel[1]).not.toHaveBeenCalled();
      expect(panel[2]).toHaveBeenCalledTimes(1);
    });

    it("forgets a link the keyboard left for something that is not a link", async () => {
      await wrapper.get("#blogs").trigger("focusin");
      await settle(MID_TRAVERSAL);
      await wrapper.get("#blogs").trigger("focusout");
      await settle(AFTER_FOCUS_DWELL);
      expect(blogs).not.toHaveBeenCalled();

      await stillFetches();
    });

    it("asks for a chunk once, however many times it is aimed at", async () => {
      await hover("games");
      await unhover("games");
      await hover("games");
      await wrapper.get("#games").trigger("focusin");
      await settle(AFTER_FOCUS_DWELL);

      expect(games).toHaveBeenCalledTimes(1);
    });

    it("counts two addresses of one page as the one chunk", async () => {
      await hover("anna");
      await unhover("anna");
      await hover("boris");

      expect(profile).toHaveBeenCalledTimes(1);
    });
  });

  describe("what it refuses", () => {
    it.each([
      ["under Save-Data", { saveData: true }],
      ["on a 2g connection", { effectiveType: "2g" }],
      ["on a slow-2g connection", { effectiveType: "slow-2g" }],
    ])("spends nothing %s", async (_, connection) => {
      connectionReports(connection);

      await hover("blogs");
      expect(blogs).not.toHaveBeenCalled();

      connectionReports({ effectiveType: "4g" });
      await unhover("blogs");
      await stillFetches();
    });

    it("holds off while a navigation the reader started is in flight", async () => {
      startNavigation();

      await hover("blogs");
      expect(blogs).not.toHaveBeenCalled();

      finishNavigation();
      await unhover("blogs");
      await stillFetches();
    });

    it("does not stay switched off when a navigation ends in an error", async () => {
      // A guard that throws reaches no afterEach.
      startNavigation();
      failNavigation();

      await stillFetches();
    });

    it("leaves a link off this site alone", async () => {
      await hover("outside");

      expect(fake.resolve).not.toHaveBeenCalledWith(
        expect.stringContaining("example.com"),
      );

      await unhover("outside");
      await stillFetches();
    });

    it("leaves an in-page anchor alone", async () => {
      await hover("anchor");

      expect(home).not.toHaveBeenCalled();

      await unhover("anchor");
      await stillFetches();
    });

    it("leaves a link that opens its own tab alone", async () => {
      // That tab has a module registry of its own.
      await hover("new-tab");
      expect(blogs).not.toHaveBeenCalled();

      await unhover("new-tab");
      await stillFetches();
    });

    it("leaves a link that downloads instead of navigating alone", async () => {
      await hover("download");
      expect(blogs).not.toHaveBeenCalled();

      await unhover("download");
      await stillFetches();
    });

    it("leaves the page already open alone", async () => {
      await hover("home");
      expect(home).not.toHaveBeenCalled();

      await unhover("home");
      await stillFetches();
    });

    it("leaves an internal address that is not a route of ours alone", async () => {
      await hover("file");
      expect(notFound).not.toHaveBeenCalled();

      await unhover("file");
      await stillFetches();
    });
  });

  it("stops listening once it is torn down", async () => {
    uninstall();

    await hover("games");

    expect(games).not.toHaveBeenCalled();
    // afterEach tears down again; doing so twice must be harmless.
  });

  it("stops watching the router once it is torn down", () => {
    uninstall();

    // The fake router's lists outlive a case.
    expect(fake.beforeGuards).toHaveLength(0);
    expect(fake.afterHooks).toHaveLength(0);
    expect(fake.errorHandlers).toHaveLength(0);
  });

  it("drops a wait still running when it is torn down", async () => {
    await wrapper.get("#games").trigger("mouseover");
    uninstall();

    await settle(AFTER_HOVER_DWELL);

    expect(games).not.toHaveBeenCalled();
  });
});
