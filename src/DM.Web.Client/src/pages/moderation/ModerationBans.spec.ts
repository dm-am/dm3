/**
 * @vitest-environment jsdom
 */

// The server's entries in its order; the lift is offered by the server's flag
// on the row, not by the reader's role. The name filter goes both ways through
// the address. How an entry reads is BanLogCard.spec.ts's.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import ModerationBans from "./ModerationBans.vue";
import {
  mountPage,
  windows,
  type MountOptions,
} from "@/shared/lib/testing/mountPage";

const { getBanHistory, liftBan } = vi.hoisted(() => ({
  getBanHistory: vi.fn(),
  liftBan: vi.fn(),
}));

// The network only: the router, the lift window and the list are the real ones.
// The original as a plain object: its type would name a file inside another
// slice, which the layer rule refuses even as a type.
vi.mock("@/entities/moderation/api/banApi", async (importOriginal) => ({
  ...(await importOriginal<object>()),
  default: { getBanHistory, liftBan },
}));

/** The page of a reader who chose no size. */
const PAGE_SIZE = DEFAULT_PAGE_SIZE;

const person = (username: string) => ({
  id: `u-${username}`,
  username,
  lastActivityUtc: null,
  role: "RegularUser",
  isNewbie: false,
});

const ban = (over: Record<string, unknown> = {}) => ({
  id: "b-1",
  user: person("Experienced"),
  moderator: person("TestSeniorMod"),
  type: "Temporary",
  accessPolicy: "DemocraticBan",
  startedUtc: "2026-10-12T12:00:00Z",
  expiresUtc: "2026-10-19T12:00:00Z",
  comment: "Оскорбление участников в глобальном чате",
  isActive: true,
  canLift: true,
  ...over,
});

const issued = (over: Record<string, unknown> = {}, number = 16) => ({
  number,
  kind: "Issued",
  occurredUtc: "2026-10-12T12:00:00Z",
  ban: ban(over),
});

const page = (
  resources: unknown[],
  total = resources.length,
  pages = 1,
  skip = 0,
  take: number = PAGE_SIZE,
) => ({
  data: {
    resources,
    paging: {
      skip,
      take,
      total,
      current: skip / take + 1,
      pages,
    },
  },
  error: null,
});

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.useRealTimers();
  delete (Element.prototype as Partial<Element>).scrollIntoView;
});

let router: Router;

/** The options of a mount besides the addresses, which are the page's own. */
type Extra = Omit<MountOptions, "routes">;

async function rendered(address = "/moderation/bans", options: Extra = {}) {
  const mounted = await mountPage(ModerationBans, address, {
    routes: [
      ["moderation-bans", "/moderation/bans"],
      ["profile", "/users/:username"],
    ],
    ...options,
  });
  router = mounted.router;
  await flushPromises();
  return mounted.wrapper;
}

/** The page in the document, where an entry can be found by its anchor. */
const renderedAttached = (address: string, options: Extra = {}) =>
  rendered(address, { ...options, attachTo: document.body });

/** Where the reader is now. */
const address = () => router.currentRoute.value.fullPath;

const go = async (to: string) => {
  await router.push(to);
  await flushPromises();
};

const nameField = (wrapper: VueWrapper) =>
  wrapper.find<HTMLInputElement>("form input");

const sentences = (wrapper: VueWrapper) =>
  wrapper
    .findAll(".ban-log-card__phrase")
    .map((phrase) => phrase.element.textContent ?? "");

/** The lift window over the page, if it is open. */
const liftWindow = () => windows().find(".dialog-frame .dialog");

/** jsdom lays nothing out and has no scrollIntoView of its own. */
function recordScrolls() {
  const scrolled: { id: string; element: Element; options: unknown }[] = [];
  Element.prototype.scrollIntoView = function (
    this: Element,
    options?: unknown,
  ) {
    scrolled.push({ id: this.id, element: this, options });
  };
  return scrolled;
}

beforeEach(() => {
  getBanHistory.mockReset();
  liftBan.mockReset();
  liftBan.mockResolvedValue({ data: null, error: null });
});

describe("the ban list", () => {
  it("asks for the page named in the address, at the size of the list", async () => {
    getBanHistory.mockResolvedValue(page([issued()], 45, 3, 40));

    const wrapper = await rendered("/moderation/bans?number=3");

    expect(getBanHistory).toHaveBeenCalledWith({
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
    expect(wrapper.get(".paging a.active").text()).toBe("3");
  });

  it("asks for pages as long as the reader's lists", async () => {
    getBanHistory.mockResolvedValue(page([issued()], 45, 7, 14, 7));
    const pinia = createPinia();
    useAuthStore(pinia).user = {
      settings: { pageSize: 7 },
    } as never;

    await rendered("/moderation/bans?number=3", { pinia });

    expect(getBanHistory).toHaveBeenCalledWith({ skip: 14, take: 7 });
  });

  // The page an entry is on is counted in the pages the reader chose.
  it("names the page of an entry in pages as long as the reader's lists", async () => {
    getBanHistory.mockResolvedValue(page([issued({}, 16)], 45, 7, 14, 7));
    const pinia = createPinia();
    useAuthStore(pinia).user = {
      settings: { pageSize: 7 },
    } as never;

    await rendered("/moderation/bans?entry=16#ban-16", { pinia });

    expect(address()).toBe("/moderation/bans?number=3#ban-16");
  });

  // A lift stands on top as an entry of its own, in the server's order.
  it("draws every entry in the order the server sent", async () => {
    getBanHistory.mockResolvedValue(
      page([
        {
          number: 17,
          kind: "Lifted",
          occurredUtc: "2026-10-13T11:12:00Z",
          ban: ban({
            isActive: false,
            canLift: false,
            liftedUtc: "2026-10-13T11:12:00Z",
            liftedBy: person("SolohinLex"),
            liftReason: "Извинился",
          }),
        },
        issued({
          isActive: false,
          canLift: false,
          liftedBy: person("SolohinLex"),
        }),
      ]),
    );

    const wrapper = await rendered();

    expect(sentences(wrapper).map((text) => text.split(" ")[1])).toEqual([
      "снял(а)",
      "выдал(а)",
    ]);
  });

  // A senior moderator may not lift his own ban nor a permanent one.
  it("offers the lift by the server's flag, not by the reader's role", async () => {
    getBanHistory.mockResolvedValue(
      page([
        issued({ id: "b-1", canLift: false }),
        issued({ id: "b-2", canLift: true }),
      ]),
    );

    const wrapper = await rendered();
    const cards = wrapper.findAll(".ban-log-card");

    expect(cards[0].find(".caption-action").exists()).toBe(false);
    expect(cards[1].get(".caption-action").text()).toBe("Снять бан");
  });

  it("opens the lift window on the ban the action stands under", async () => {
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-7", user: person("Нарушитель") })]),
    );

    const wrapper = await rendered();
    await wrapper.get(".caption-action").trigger("click");
    await flushPromises();

    expect(liftWindow().get("h2").text()).toBe("Снятие бана");
    expect(liftWindow().get(".lift-context strong").text()).toBe("Нарушитель");
    await windows().get("#lift-reason").setValue("Извинился");
    await liftWindow().get("form").trigger("submit");
    await flushPromises();
    expect(liftBan).toHaveBeenCalledWith("b-7", "Извинился", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
  });

  /** The lift comes back as an entry on top of the page the moderator is on. */
  it("re-reads the page the moderator is on after a lift", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered("/moderation/bans?number=2");
    await wrapper.get(".caption-action").trigger("click");
    await flushPromises();
    await windows().get("#lift-reason").setValue("Извинился");
    await liftWindow().get("form").trigger("submit");
    await flushPromises();

    expect(liftWindow().exists()).toBe(false);
    expect(getBanHistory).toHaveBeenCalledTimes(2);
    expect(getBanHistory).toHaveBeenLastCalledWith({
      skip: PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  it("re-reads nothing when the lift window is closed", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();
    await wrapper.get(".caption-action").trigger("click");
    await flushPromises();
    await liftWindow().get(".dialog-close").trigger("click");
    await flushPromises();

    expect(liftWindow().exists()).toBe(false);
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  it("says the list is empty only when it really is", async () => {
    getBanHistory.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Банов пока нет");
  });

  it("offers the first page when this one is past the end of a filled list", async () => {
    getBanHistory.mockResolvedValue(page([], 3));

    const wrapper = await rendered(
      "/moderation/bans?username=SolohinLex&number=4",
    );

    expect(wrapper.text()).toContain("На этой странице банов нет.");
    const back = wrapper.get(".bans-empty a");
    expect(back.text()).toBe("Вернуться на первую страницу");
    expect(back.attributes("href")).toBe(
      "/moderation/bans?username=SolohinLex",
    );
  });

  // What the moderator asked for last stays, however late the older answer arrives.
  it("keeps the page the address names when a later answer belongs to an older one", async () => {
    const answers = new Map<number, (value: unknown) => void>();
    getBanHistory.mockImplementation(
      ({ skip }: { skip: number }) =>
        new Promise((resolve) => {
          answers.set(skip, resolve);
        }),
    );

    const wrapper = await rendered();
    await go("/moderation/bans?number=2");

    answers.get(PAGE_SIZE)?.(
      page([issued({ user: person("Новая"), canLift: false })]),
    );
    await flushPromises();
    answers.get(0)?.(
      page([issued({ user: person("Старая"), canLift: false })]),
    );
    await flushPromises();

    expect(sentences(wrapper)).toHaveLength(1);
    expect(sentences(wrapper)[0]).toContain("Новая");
  });
});

describe("the name filter of the ban list", () => {
  it("draws the search line the warning list draws", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();

    expect(wrapper.find("label").exists()).toBe(false);
    expect(wrapper.find("form .search-container").exists()).toBe(true);
    expect(nameField(wrapper).attributes("placeholder")).toBe(
      "Точное имя пользователя",
    );
    expect(wrapper.get("form button[type=submit]").text()).toBe("Найти");
  });

  /** The profile's "Баны: N" leads here with the name in the address. */
  it("asks for the bans of the name the address names, and shows the name", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered("/moderation/bans?username=SolohinLex");

    expect(getBanHistory).toHaveBeenCalledWith({
      username: "SolohinLex",
      skip: 0,
      take: PAGE_SIZE,
    });
    expect(nameField(wrapper).element.value).toBe("SolohinLex");
  });

  it("puts the name in the address without the page number of the previous list", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered("/moderation/bans?number=3");
    await nameField(wrapper).setValue("  SolohinLex  ");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(address()).toBe("/moderation/bans?username=SolohinLex");
    expect(getBanHistory).toHaveBeenLastCalledWith({
      username: "SolohinLex",
      skip: 0,
      take: PAGE_SIZE,
    });
    expect(nameField(wrapper).element.value).toBe("SolohinLex");
  });

  /** Typing a name is no search yet: the server looks up whole names. */
  it("asks for nothing while the name is being typed", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("Solo");
    await flushPromises();

    expect(address()).toBe("/moderation/bans");
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  /** The cross of the field gives the whole list back at once. */
  it("drops the name at once when the field is emptied", async () => {
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered(
      "/moderation/bans?username=SolohinLex&number=2",
    );
    await wrapper.get("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(address()).toBe("/moderation/bans");
    expect(getBanHistory).toHaveBeenLastCalledWith({
      username: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("says nothing was found when the name matches nobody", async () => {
    getBanHistory.mockResolvedValue(page([]));

    const wrapper = await rendered("/moderation/bans?username=Никто");

    expect(wrapper.text()).toContain("Банов по заданным фильтрам не найдено");
    expect(wrapper.text()).not.toContain("Банов пока нет");
  });

  it("keeps the name in the address the paging control links to", async () => {
    getBanHistory.mockResolvedValue(page([issued()], 25, 2));

    const wrapper = await rendered("/moderation/bans?username=SolohinLex");

    const second = wrapper
      .findAll(".paging a")
      .find((link) => link.text() === "2");
    expect(second?.attributes("href")).toBe(
      "/moderation/bans?username=SolohinLex&number=2",
    );
  });
});

// The number of an entry names it, and the server answers with the page that
// holds it: a page number goes stale as newer entries push it down.
describe("the links to the entries of the ban list", () => {
  it("links the number of an entry to it, in the list of the name, whatever its page", async () => {
    getBanHistory.mockResolvedValue(page([issued({}, 16)], 25, 2, 20));

    const wrapper = await rendered(
      "/moderation/bans?username=Experienced&number=2",
    );
    const number = wrapper.get(".ban-log-card__number");

    expect(number.text()).toBe("16");
    expect(number.attributes("href")).toBe(
      "/moderation/bans?username=Experienced&entry=16#ban-16",
    );
    expect(wrapper.get(".ban-log-card .record-block").attributes("id")).toBe(
      "ban-16",
    );
  });

  it("asks for the page of the entry the address names and then names that page", async () => {
    getBanHistory.mockResolvedValue(page([issued({}, 16)], 45, 3, 40));

    await rendered("/moderation/bans?username=Experienced&entry=16#ban-16");
    await flushPromises();

    expect(getBanHistory).toHaveBeenCalledWith({
      username: "Experienced",
      entry: 16,
      skip: 0,
      take: PAGE_SIZE,
    });
    expect(address()).toBe(
      "/moderation/bans?username=Experienced&number=3#ban-16",
    );
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  /** An entry on the first page, or one the list does not have: no number. */
  it("leaves the page number out of the address for the first page", async () => {
    getBanHistory.mockResolvedValue(page([issued({}, 16)]));

    await rendered("/moderation/bans?entry=999#ban-999");
    await flushPromises();

    expect(address()).toBe("/moderation/bans#ban-999");
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  it("brings the entry the address names into view and marks it", async () => {
    const scrolled = recordScrolls();
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-2" }, 16), issued({ id: "b-1" }, 15)]),
    );

    await renderedAttached("/moderation/bans#ban-15");

    await vi.waitFor(() =>
      expect(scrolled.map((entry) => entry.id)).toEqual(["ban-15"]),
    );
    const { element } = scrolled[0];
    expect(element.classList.contains("highlight-unread")).toBe(true);
    expect(element.classList.contains("record-block")).toBe(true);
  });

  /** The one scroll an arrival at the entry asks for. */
  const TO_THE_MIDDLE = {
    id: "ban-15",
    options: { behavior: "smooth", block: "center" },
  };

  /**
   * An arrival by the number of the entry, held at the page's own change of
   * address until it is let through, then let through: the scrolls it made.
   */
  async function arriveThroughHeldAddress() {
    const scrolled = recordScrolls();
    // The page's own change of address waits at a guard until it is let through.
    let reached = false;
    let settle = () => {};
    const hold = (next: Router) =>
      next.beforeEach(
        () =>
          new Promise<boolean>((resolve) => {
            reached = true;
            settle = () => resolve(true);
          }),
      );
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-2" }, 16), issued({ id: "b-1" }, 15)]),
    );

    vi.useFakeTimers({ toFake: ["setTimeout"] });
    await renderedAttached("/moderation/bans?entry=15#ban-15", {
      beforeMount: hold,
    });
    await vi.advanceTimersByTimeAsync(300);

    expect(reached).toBe(true);
    expect(address()).toBe("/moderation/bans?entry=15#ban-15");
    expect(scrolled).toEqual([]);

    settle();
    await vi.waitFor(() => expect(scrolled.length).toBeGreaterThan(0));
    // Past the pause of every scroll the arrival woke.
    await vi.advanceTimersByTimeAsync(300);
    return scrolled.map(({ id, options }) => ({ id, options }));
  }

  // The router pulls an anchor to the top once the address changes, so the
  // entry comes to the middle only after the address names its page.
  it("brings the entry into the middle only after the address names its page", async () => {
    const scrolled = await arriveThroughHeldAddress();

    for (const one of scrolled) expect(one).toEqual(TO_THE_MIDDLE);
    expect(address()).toBe("/moderation/bans#ban-15");
  });

  // The page's own change of address, which drops the page number from the
  // anchor, must not scroll and mark the entry a second time.
  it("brings the entry into the middle once", async () => {
    const scrolled = await arriveThroughHeldAddress();

    expect(scrolled).toEqual([TO_THE_MIDDLE]);
  });

  it("leaves a reader who went elsewhere before the entry came into view", async () => {
    const scrolled = recordScrolls();
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-2" }, 16), issued({ id: "b-1" }, 15)]),
    );

    vi.useFakeTimers({ toFake: ["setTimeout"] });
    await renderedAttached("/moderation/bans#ban-15");
    await go("/moderation/bans");
    await vi.advanceTimersByTimeAsync(300);

    expect(scrolled).toEqual([]);
  });

  /** A read that failed is read again when its address is opened again. */
  it("reads a page again after a read of it failed", async () => {
    getBanHistory.mockResolvedValueOnce(page([issued()]));
    const wrapper = await rendered();

    getBanHistory.mockResolvedValueOnce({ data: null, error: "Ошибка" });
    await go("/moderation/bans?number=2");
    getBanHistory.mockResolvedValueOnce(page([issued()]));
    await go("/moderation/bans");

    expect(getBanHistory).toHaveBeenCalledTimes(3);
    expect(wrapper.find(".ban-log-card").exists()).toBe(true);
  });
});
