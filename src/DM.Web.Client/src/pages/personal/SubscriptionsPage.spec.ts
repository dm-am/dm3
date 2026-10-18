/**
 * @vitest-environment jsdom
 */

/**
 * "Подписки": what the reader follows, in sections of the site's lists.
 *
 * The page used to be one mixed column behind a filter, a badge of the type on
 * every row and a button on every row, and "Отписаться" took the row away with
 * no way back but the game's own page. What is held here:
 *
 * - the sections, each counted, a section with nothing in it not drawn, and
 *   the rows by name;
 * - every name a named route (the hand-written paths led nowhere: /games/<id>
 *   is not a route, a topic by id goes through the resolver, and a profile is
 *   addressed by name), and a target that is gone named in words, not "n/a",
 *   a removed account by the link every surface names it with;
 * - "Отписаться" leaves the row and turns into "Подписаться", which puts the
 *   subscription back with the settings it had, and the count follows;
 * - a person's row says what it sends and opens the panel of the profile;
 * - a game and a blog named in the colour of their status, as everywhere on
 *   the site: green while new, grey once closed (grey first);
 * - a failed load said as one, not as "no subscriptions".
 *
 * The rows come off the wire as the server writes them (enum names), so the
 * transport is the only thing faked: the page once showed every row as
 * "Неизвестно" to a test that handed it numbers.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { Api } from "@/shared/api";
import { removedAccountRoute } from "@/entities/user";
import SubscriptionsPage from "./SubscriptionsPage.vue";

const GUID = "11111111-1111-1111-1111-111111111111";
const GAME_B = "22222222-2222-2222-2222-222222222222";
const GONE = "33333333-3333-3333-3333-333333333333";
const USER = "44444444-4444-4444-4444-444444444444";

const wire = (over: Record<string, unknown>) => ({
  id: "s-" + String(over.targetId),
  settings: "None",
  createdUtc: "2026-09-16T00:09:26.612346+00:00",
  ...over,
});

/** A list the way the stand answers it, in no particular order. */
const LIST = [
  wire({
    id: "s-game-b",
    targetType: "Game",
    targetId: GAME_B,
    targetTitle: "Последний Рубеж",
    settings: "GamePlayerDefault",
  }),
  wire({
    id: "s-game-a",
    targetType: "Game",
    targetId: GUID,
    targetTitle: "Врата Бездны",
    settings: "GameReaderDefault",
  }),
  wire({ id: "s-gone", targetType: "Game", targetId: GONE }),
  wire({
    id: "s-blog",
    targetType: "Blog",
    targetId: GUID,
    targetTitle: "Новости ролевок",
    settings: "BlogReaderDefault",
  }),
  wire({
    id: "s-user",
    targetType: "User",
    targetId: USER,
    targetTitle: "SolohinLex",
    targetUsername: "SolohinLex",
    settings: "InApp, AuthorGameEvents, AuthorTopicEvents",
  }),
];

enableAutoUnmount(afterEach);

/** The stand's list, and every write the page makes answered by hand. */
async function render(list: unknown[] = LIST) {
  const pinia = createPinia();
  setActivePinia(pinia);
  let current = [...list];
  const get = vi.spyOn(Api, "get").mockImplementation(async () => ({
    data: { resources: current },
    error: null,
  }));
  const del = vi.spyOn(Api, "delete").mockImplementation(async (url) => {
    const id = String(url).split("/").pop();
    current = current.filter((s) => (s as { id: string }).id !== id);
    return { data: null, error: null };
  });
  const post = vi.spyOn(Api, "post").mockImplementation(async (_url, body) => {
    const request = body as { targetType: number; targetId: string };
    const back = list.find(
      (s) => (s as { targetId: string }).targetId === request.targetId,
    );
    current = [...current, { ...(back as object), ...request, id: "s-new" }];
    return { data: current.at(-1), error: null };
  });

  const wrapper = mount(SubscriptionsPage, {
    attachTo: document.body,
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return { wrapper, get, del, post };
}

const headings = (wrapper: VueWrapper) =>
  wrapper.findAll("h2").map((h) => h.text());

/** Each row of a section, as it reads with the spacing condensed. */
const rowsOf = (wrapper: VueWrapper, index: number) =>
  wrapper
    .findAll(".subscriptions-section")
    [index].findAll(".subscription-row")
    .map((row) => row.text().replace(/\s+/g, " ").trim());

const actionIn = (wrapper: VueWrapper, name: string) =>
  wrapper
    .findAll(".subscription-row")
    .find((row) => row.text().includes(name))!
    .find("button.caption-action");

describe("SubscriptionsPage", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("lists every kind in a section of its own, counted, the empty ones left out", async () => {
    const { wrapper } = await render();

    expect(headings(wrapper)).toEqual([
      "Игры (3)",
      "Блоги (1)",
      "Пользователи (1)",
    ]);
    expect(wrapper.find(".segmented-control").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Неизвестно");
  });

  it("reads each row as a line of the site: the name, then the action after a bar", async () => {
    const { wrapper } = await render();

    // By name, and the target that is gone at the end, named in words.
    expect(rowsOf(wrapper, 0)).toEqual([
      "- Врата Бездны | Отписаться",
      "- Последний Рубеж | Отписаться",
      "- Игра удалена | Отписаться",
    ]);
    // A blog is followed by reading it, as its own panel says.
    expect(rowsOf(wrapper, 1)).toEqual([
      "- Новости ролевок | Убрать из читаемого",
    ]);
    // A person's row says what it sends, and opens the panel instead.
    expect(rowsOf(wrapper, 2)).toEqual([
      "- SolohinLex | игры, топики | Настроить",
    ]);
  });

  it("links every name to its target, and a target that is gone to nothing", async () => {
    const { wrapper } = await render();

    expect(
      wrapper.findAllComponents(RouterLinkStub).map((link) => link.props("to")),
    ).toEqual([
      { name: "game", params: { id: GUID } },
      { name: "game", params: { id: GAME_B } },
      { name: "blog", params: { id: GUID } },
      { name: "profile", params: { username: "SolohinLex" } },
    ]);
    expect(wrapper.text()).not.toContain("n/a");
    expect(wrapper.text()).not.toContain(GONE);
  });

  it("names a game and a blog in the colour of their status", async () => {
    const day = 24 * 3600 * 1000;
    const opened = (days: number) =>
      new Date(Date.now() - days * day).toISOString();
    const { wrapper } = await render([
      wire({
        targetType: "Game",
        targetId: "g-new",
        targetTitle: "А новая",
        targetStatus: "Active",
        targetActivatedUtc: opened(2),
      }),
      wire({
        targetType: "Game",
        targetId: "g-old",
        targetTitle: "Б давняя",
        targetStatus: "Active",
        targetActivatedUtc: opened(90),
      }),
      // Closed within the week it opened: grey, not green.
      wire({
        targetType: "Game",
        targetId: "g-closed",
        targetTitle: "В закрытая",
        targetStatus: "Closed",
        targetActivatedUtc: opened(3),
      }),
      wire({
        targetType: "Blog",
        targetId: "b-new",
        targetTitle: "Г новый блог",
        targetStatus: "Active",
        targetActivatedUtc: opened(1),
      }),
      wire({
        targetType: "Blog",
        targetId: "b-closed",
        targetTitle: "Д закрытый блог",
        targetStatus: "Closed",
        targetActivatedUtc: opened(300),
      }),
      wire({ targetType: "Topic", targetId: "t-1", targetTitle: "Е топик" }),
    ]);

    const tones = wrapper.findAll(".subscription-title").map((link) => [
      link.text(),
      link
        .classes()
        .filter((c) => c.startsWith("subscription-title--"))
        .join(" "),
    ]);
    expect(tones).toEqual([
      ["А новая", "subscription-title--new"],
      ["Б давняя", ""],
      ["В закрытая", "subscription-title--closed"],
      ["Г новый блог", "subscription-title--new"],
      ["Д закрытый блог", "subscription-title--closed"],
      ["Е топик", ""],
    ]);
  });

  it("points a topic at the resolver route", async () => {
    const { wrapper } = await render([
      wire({ targetType: "Topic", targetId: GUID, targetTitle: "Правила" }),
    ]);

    expect(headings(wrapper)).toEqual(["Топики (1)"]);
    expect(wrapper.getComponent(RouterLinkStub).props("to")).toEqual({
      name: "forum-topic-redirect",
      params: { topicId: GUID },
    });
  });

  /**
   * A person whose account was removed is named "Аккаунт удален" as everywhere
   * on the site: a link of the ordinary colour to the profile that gathers what
   * removed accounts left, not a grey word with nowhere to go (owner, 12 October
   * 2026). A game, a blog or a topic that is gone stays a grey word: those have
   * no page left to open.
   */
  it("names a removed account the way the site does, as a link", async () => {
    const { wrapper } = await render([
      wire({
        id: "s-removed",
        targetType: "User",
        targetId: GONE,
        settings: "InApp, AuthorGameEvents",
      }),
    ]);

    expect(rowsOf(wrapper, 0)).toEqual(["- Аккаунт удален | Отписаться"]);
    const link = wrapper.getComponent(RouterLinkStub);
    expect(link.text()).toBe("Аккаунт удален");
    expect(link.props("to")).toEqual(removedAccountRoute);
    expect(link.classes()).toContain("subscription-title");
    expect(link.classes()).not.toContain("subscription-title--gone");
    expect(link.classes()).not.toContain("muted");
  });

  /**
   * A press on the wrong row is put right in the same place. The row stays,
   * its word turns into the way back, and the count in the heading follows;
   * the way back restores the subscription with the settings it had, not a
   * default the reader never chose.
   */
  it("keeps the row it unsubscribes, and puts the subscription back as it was", async () => {
    const { wrapper, del, post } = await render();

    await actionIn(wrapper, "Последний Рубеж").trigger("click");
    await flushPromises();

    expect(del).toHaveBeenCalledWith("users/me/subscriptions/s-game-b");
    expect(headings(wrapper)[0]).toBe("Игры (2)");
    expect(rowsOf(wrapper, 0)[1]).toBe("- Последний Рубеж | Подписаться");

    await actionIn(wrapper, "Последний Рубеж").trigger("click");
    await flushPromises();

    expect(post).toHaveBeenCalledWith("users/me/subscriptions", {
      targetType: 1,
      targetId: GAME_B,
      settings: 1 | 16 | 32 | 128 | 256,
    });
    expect(headings(wrapper)[0]).toBe("Игры (3)");
    expect(rowsOf(wrapper, 0)[1]).toBe("- Последний Рубеж | Отписаться");
  });

  it("offers nothing back for a target that is gone", async () => {
    const { wrapper } = await render();

    await actionIn(wrapper, "Игра удалена").trigger("click");
    await flushPromises();

    expect(rowsOf(wrapper, 0)[2]).toBe("- Игра удалена");
  });

  it("says a refused unsubscribe under its row and keeps the word", async () => {
    const { wrapper, del } = await render();
    del.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    } as never);

    await actionIn(wrapper, "Новости ролевок").trigger("click");
    await flushPromises();

    const row = wrapper
      .findAll(".subscription-row")
      .find((r) => r.text().includes("Новости ролевок"))!;
    expect(row.get(".action-result").text()).toBe(
      "Не удалось убрать блог из читаемого",
    );
    expect(row.text()).toContain("Убрать из читаемого");
  });

  it("opens the panel of a person's subscription under its row", async () => {
    const { wrapper } = await render();

    await actionIn(wrapper, "SolohinLex").trigger("click");

    const panel = wrapper.get(".subscription-row .subscription-panel");
    expect(
      panel
        .findAll("input")
        .map((box) => (box.element as HTMLInputElement).checked),
    ).toEqual([true, false, true]);

    await actionIn(wrapper, "SolohinLex").trigger("click");
    expect(wrapper.find(".subscription-panel").exists()).toBe(false);
  });

  it("says there is nothing to list, and where to subscribe", async () => {
    const { wrapper } = await render([]);

    expect(headings(wrapper)).toEqual([]);
    expect(wrapper.text()).toContain(
      "Подписок нет. Подписаться на игру или блог можно на их странице",
    );
  });

  it("says a failed load as one, not as an empty list", async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    vi.spyOn(Api, "get").mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 500, traceId: "t" },
    } as never);

    const wrapper = mount(SubscriptionsPage, {
      global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
    });
    await flushPromises();

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить подписки",
    );
    expect(wrapper.text()).not.toContain("Подписок нет");
  });
});
