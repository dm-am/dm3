/**
 * @vitest-environment jsdom
 */

/**
 * "Последние баны", the ban list written as DM2's ban log.
 *
 * The server answers with entries, an issue or a lift each, newest first, and
 * the page keeps that order: a lift made today stands on top, not at the date
 * of the ban it ended. How an entry reads is the card's own spec
 * (BanLogCard.spec.ts); the card is mounted here for real, because what this
 * page decides about it is whether the lift is offered, and that has to be the
 * server's flag on the row and not the role of the reader. A senior moderator
 * used to be offered the lift of his own ban and of a permanent one, and the
 * server refused it after the window was filled in.
 *
 * The list is narrowed by name in the search line of the warning list next
 * door, through the address, so the bans of one person are a link: the
 * profile's "Баны: N" leads here with the name in it. The checks follow the
 * name both ways, from the address to the request and the field, and from the
 * field to the address without the page number of the list being left.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { Mock } from "vitest";
import type { Ref } from "vue";
import * as vueRouter from "vue-router";
import ModerationBans from "./ModerationBans.vue";

const getBanHistory = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  moderationApi: { getBanHistory },
}));

vi.mock("@/features/moderation-actions", () => ({
  LiftBanDialog: { template: "<div class='dialog-stub' />" },
}));

/**
 * The lift window is opened by hand rather than mounted, so the seam this
 * page owns is what it hands over: which ban, whose, and what happens after.
 */
const modalOpen = vi.hoisted(() => vi.fn());
const modalClose = vi.hoisted(() => vi.fn());
const modal = vi.hoisted(() => ({
  attrs: null as Record<string, unknown> | null,
}));

vi.mock("vue-final-modal", () => ({
  useModal: (options: { attrs: Record<string, unknown> }) => {
    modal.attrs = options.attrs;
    return { open: modalOpen, close: modalClose };
  },
}));

/**
 * The address bar, moved by the push as a navigation would move it, and left
 * where it is when it is asked for the address already open (the warning
 * list's spec says why). The replace moves it the same way, anchor and all.
 */
vi.mock("vue-router", async () => {
  const { shallowRef } = await import("vue");
  const query = shallowRef<Record<string, string>>({});
  const hash = shallowRef("");
  const isCurrent = (next: Record<string, string>) => {
    const current = query.value;
    const keys = Object.keys(next);
    return (
      keys.length === Object.keys(current).length &&
      keys.every((key) => current[key] === next[key])
    );
  };
  const push = vi.fn((to: { query?: Record<string, string> }) => {
    const next = { ...(to.query ?? {}) };
    if (isCurrent(next)) return;
    query.value = next;
  });
  const replace = vi.fn(
    (to: { query?: Record<string, string>; hash?: string }) => {
      query.value = { ...(to.query ?? {}) };
      hash.value = to.hash ?? "";
    },
  );
  return {
    useRoute: () => ({
      get query() {
        return query.value;
      },
      get hash() {
        return hash.value;
      },
    }),
    useRouter: () => ({ push, replace }),
    routeQuery: query,
    routeHash: hash,
    routerPush: push,
    routerReplace: replace,
  };
});

const { routeQuery, routeHash, routerPush, routerReplace } =
  vueRouter as unknown as {
    routeQuery: Ref<Record<string, string>>;
    routeHash: Ref<string>;
    routerPush: Mock;
    routerReplace: Mock;
  };

/**
 * The reader is a senior moderator: the one role the old page offered the
 * lift to, so a page that still reads the role offers it here.
 */
vi.mock("./lib/useRoleGate", () => ({
  useRoleGate: () => ({
    hasAccess: true,
    deniedText: "",
    isSeniorModerator: true,
  }),
}));

const PAGE_SIZE = 20;

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
  comment: "Оскорбление участников в общем чате",
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
) => ({
  data: {
    resources,
    paging: {
      skip,
      take: PAGE_SIZE,
      total,
      current: skip / PAGE_SIZE + 1,
      pages,
    },
  },
  error: null,
});

enableAutoUnmount(afterEach);

function render({ realPaging = false }: { realPaging?: boolean } = {}) {
  return mount(ModerationBans, {
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template:
            "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
        },
        "page-title": { template: "<h1><slot /></h1>" },
        Button: { template: "<button><slot /></button>" },
        ...(realPaging
          ? {}
          : {
              Paging: {
                props: ["paging"],
                template: "<nav class='paging-stub' />",
              },
            }),
        ErrorState: { template: "<div class='error-stub' />" },
      },
    },
  });
}

async function rendered(options?: { realPaging?: boolean }) {
  const wrapper = render(options);
  await flushPromises();
  return wrapper;
}

/** The page in the document, where an entry can be found by its anchor. */
function mountAttached() {
  return mount(ModerationBans, {
    attachTo: document.body,
    global: {
      stubs: {
        "page-title": { template: "<h1><slot /></h1>" },
        Button: { template: "<button><slot /></button>" },
        Paging: { template: "<nav />" },
        ErrorState: { template: "<div />" },
      },
    },
  });
}

const nameField = (wrapper: ReturnType<typeof render>) =>
  wrapper.find<HTMLInputElement>("form input");

/** The sentences of the entries, in the order they stand. */
const sentences = (wrapper: ReturnType<typeof render>) =>
  wrapper
    .findAll(".ban-log-card__phrase")
    .map((phrase) => phrase.element.textContent ?? "");

describe("the ban list", () => {
  beforeEach(() => {
    getBanHistory.mockReset();
    modalOpen.mockReset();
    modalClose.mockReset();
    routerPush.mockClear();
    routerReplace.mockClear();
    routeQuery.value = {};
    routeHash.value = "";
  });

  it("asks for the page named in the address, at the size of the list", async () => {
    routeQuery.value = { number: "3" };
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();

    expect(getBanHistory).toHaveBeenCalledWith({
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
    expect(wrapper.find(".paging-stub").exists()).toBe(true);
  });

  /**
   * The lift of a ban stands on top as an entry of its own, over the issue
   * of the ban it ended, in the order the server put them.
   */
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

  /**
   * The server says on each ban whether this reader may lift it. A senior
   * moderator may not lift his own ban nor a permanent one, and the page
   * offered him both by his role; the server refused after the reason was
   * written.
   */
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

    expect(modalOpen).toHaveBeenCalled();
    expect(modal.attrs?.banId).toBe("b-7");
    expect(modal.attrs?.username).toBe("Нарушитель");
  });

  /** The lift comes back as an entry on top of the page the moderator is on. */
  it("re-reads the page the moderator is on after a lift", async () => {
    routeQuery.value = { number: "2" };
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();
    await wrapper.get(".caption-action").trigger("click");
    (modal.attrs?.onSuccess as () => void)();
    await flushPromises();

    expect(modalClose).toHaveBeenCalled();
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
    (modal.attrs?.onCancel as () => void)();
    await flushPromises();

    expect(modalClose).toHaveBeenCalled();
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  it("says the list is empty only when it really is", async () => {
    getBanHistory.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Банов пока нет");
  });

  it("offers the first page when this one is past the end of a filled list", async () => {
    routeQuery.value = { username: "SolohinLex", number: "4" };
    getBanHistory.mockResolvedValue(page([], 3));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("На этой странице банов нет.");
    const back = wrapper.get(".bans-empty a");
    expect(back.text()).toBe("Вернуться на первую страницу");
    expect(JSON.parse(back.attributes("data-to") ?? "null")).toEqual({
      name: "moderation-bans",
      query: { username: "SolohinLex" },
    });
  });

  /**
   * Two pages on the wire at once: what the moderator asked for last stays,
   * however late the answer to the page he left arrives.
   */
  it("keeps the page the address names when a later answer belongs to an older one", async () => {
    const answers = new Map<number, (value: unknown) => void>();
    getBanHistory.mockImplementation(
      ({ skip }: { skip: number }) =>
        new Promise((resolve) => {
          answers.set(skip, resolve);
        }),
    );

    const wrapper = render();
    await flushPromises();
    routeQuery.value = { number: "2" };
    await flushPromises();

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
  beforeEach(() => {
    getBanHistory.mockReset();
    routerPush.mockClear();
    routerReplace.mockClear();
    routeQuery.value = {};
    routeHash.value = "";
  });

  /** The search line of "Последние предупреждения", the neighbouring item. */
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
    routeQuery.value = { username: "SolohinLex" };
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();

    expect(getBanHistory).toHaveBeenCalledWith({
      username: "SolohinLex",
      skip: 0,
      take: PAGE_SIZE,
    });
    expect(nameField(wrapper).element.value).toBe("SolohinLex");
  });

  it("puts the name in the address without the page number of the previous list", async () => {
    routeQuery.value = { number: "3" };
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();
    await nameField(wrapper).setValue("  SolohinLex  ");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-bans",
      query: { username: "SolohinLex" },
    });
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

    expect(routerPush).not.toHaveBeenCalled();
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  /** The cross of the field gives the whole list back at once. */
  it("drops the name at once when the field is emptied", async () => {
    routeQuery.value = { username: "SolohinLex", number: "2" };
    getBanHistory.mockResolvedValue(page([issued()]));

    const wrapper = await rendered();
    await wrapper.get("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-bans",
      query: {},
    });
    expect(getBanHistory).toHaveBeenLastCalledWith({
      username: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("says nothing was found when the name matches nobody", async () => {
    routeQuery.value = { username: "Никто" };
    getBanHistory.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Банов по заданным фильтрам не найдено");
    expect(wrapper.text()).not.toContain("Банов пока нет");
  });

  it("keeps the name in the address the paging control links to", async () => {
    routeQuery.value = { username: "SolohinLex" };
    getBanHistory.mockResolvedValue(page([issued()], 25, 2));

    const wrapper = await rendered({ realPaging: true });

    const second = wrapper
      .findAll(".paging a")
      .find((link) => link.text() === "2");
    expect(JSON.parse(second?.attributes("data-to") ?? "null")).toEqual({
      name: "moderation-bans",
      query: { username: "SolohinLex", number: "2" },
    });
  });
});

/**
 * The number of an entry is a link to it, as the number of a comment is. The
 * list is newest first, so a page number goes stale as entries are written
 * over it: the link names the entry by its number, the server answers with the
 * page of the list that holds it, and the address then says that page, so the
 * paging goes on from it.
 */
describe("the links to the entries of the ban list", () => {
  beforeEach(() => {
    getBanHistory.mockReset();
    routerPush.mockClear();
    routerReplace.mockClear();
    routeQuery.value = {};
    routeHash.value = "";
  });

  it("links the number of an entry to it, in the list of the name, whatever its page", async () => {
    routeQuery.value = { username: "Experienced", number: "2" };
    getBanHistory.mockResolvedValue(page([issued({}, 16)], 25, 2, 20));

    const wrapper = await rendered();
    const number = wrapper.get(".ban-log-card__number");

    expect(number.text()).toBe("16");
    expect(JSON.parse(number.attributes("data-to") ?? "null")).toEqual({
      name: "moderation-bans",
      query: { username: "Experienced", entry: "16" },
      hash: "#ban-16",
    });
    expect(wrapper.get(".ban-log-card .record-block").attributes("id")).toBe(
      "ban-16",
    );
  });

  it("asks for the page of the entry the address names and then names that page", async () => {
    routeQuery.value = { username: "Experienced", entry: "16" };
    routeHash.value = "#ban-16";
    getBanHistory.mockResolvedValue(page([issued({}, 16)], 45, 3, 40));

    await rendered();
    await flushPromises();

    expect(getBanHistory).toHaveBeenCalledWith({
      username: "Experienced",
      entry: 16,
      skip: 0,
      take: PAGE_SIZE,
    });
    expect(routerReplace).toHaveBeenCalledWith({
      name: "moderation-bans",
      query: { username: "Experienced", number: "3" },
      hash: "#ban-16",
    });
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  /** An entry on the first page, or one the list does not have: no number. */
  it("leaves the page number out of the address for the first page", async () => {
    routeQuery.value = { entry: "999" };
    routeHash.value = "#ban-999";
    getBanHistory.mockResolvedValue(page([issued({}, 16)]));

    await rendered();
    await flushPromises();

    expect(routerReplace).toHaveBeenCalledWith({
      name: "moderation-bans",
      query: {},
      hash: "#ban-999",
    });
    expect(getBanHistory).toHaveBeenCalledTimes(1);
  });

  it("brings the entry the address names into view and marks it", async () => {
    routeHash.value = "#ban-15";
    // jsdom lays nothing out and has no scrollIntoView of its own.
    const scrolled: Element[] = [];
    Element.prototype.scrollIntoView = function (this: Element) {
      scrolled.push(this);
    };
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-2" }, 16), issued({ id: "b-1" }, 15)]),
    );

    const wrapper = mountAttached();
    await flushPromises();

    await vi.waitFor(() =>
      expect(scrolled.map((element) => element.id)).toEqual(["ban-15"]),
    );
    expect(scrolled[0].classList.contains("highlight-unread")).toBe(true);
    expect(scrolled[0].classList.contains("record-block")).toBe(true);
    wrapper.unmount();
    delete (Element.prototype as Partial<Element>).scrollIntoView;
  });

  /**
   * The router brings an anchor to the top of the window once the address
   * changes, so an entry brought to the middle before the address names its
   * page would be pulled back to the top edge. The entry comes into view after
   * the address is settled, in the middle of the window, as a comment does.
   */
  it("brings the entry into the middle only after the address names its page", async () => {
    routeQuery.value = { entry: "15" };
    routeHash.value = "#ban-15";
    const scrolled: { id: string; options: unknown }[] = [];
    Element.prototype.scrollIntoView = function (
      this: Element,
      options?: unknown,
    ) {
      scrolled.push({ id: this.id, options });
    };
    const moveAddress = routerReplace.getMockImplementation()!;
    let settle = () => {};
    routerReplace.mockImplementationOnce((to) => {
      moveAddress(to);
      return new Promise<void>((resolve) => (settle = resolve));
    });
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-2" }, 16), issued({ id: "b-1" }, 15)]),
    );

    const wrapper = mountAttached();
    await flushPromises();
    await new Promise((resolve) => setTimeout(resolve, 300));

    expect(routerReplace).toHaveBeenCalledTimes(1);
    expect(scrolled).toEqual([]);

    settle();
    await vi.waitFor(() =>
      expect(scrolled).toEqual([
        { id: "ban-15", options: { behavior: "smooth", block: "center" } },
      ]),
    );
    wrapper.unmount();
    delete (Element.prototype as Partial<Element>).scrollIntoView;
  });

  it("leaves a reader who went elsewhere before the entry came into view", async () => {
    routeHash.value = "#ban-15";
    const scrolled: string[] = [];
    Element.prototype.scrollIntoView = function (this: Element) {
      scrolled.push(this.id);
    };
    getBanHistory.mockResolvedValue(
      page([issued({ id: "b-2" }, 16), issued({ id: "b-1" }, 15)]),
    );

    const wrapper = mountAttached();
    await flushPromises();
    routeHash.value = "";
    await new Promise((resolve) => setTimeout(resolve, 300));

    expect(scrolled).toEqual([]);
    wrapper.unmount();
    delete (Element.prototype as Partial<Element>).scrollIntoView;
  });

  /** A read that failed is read again when its address is opened again. */
  it("reads a page again after a read of it failed", async () => {
    getBanHistory.mockResolvedValueOnce(page([issued()]));
    const wrapper = await rendered();

    getBanHistory.mockResolvedValueOnce({ data: null, error: "Ошибка" });
    routeQuery.value = { number: "2" };
    await flushPromises();
    getBanHistory.mockResolvedValueOnce(page([issued()]));
    routeQuery.value = {};
    await flushPromises();

    expect(getBanHistory).toHaveBeenCalledTimes(3);
    expect(wrapper.find(".ban-log-card").exists()).toBe(true);
  });
});
