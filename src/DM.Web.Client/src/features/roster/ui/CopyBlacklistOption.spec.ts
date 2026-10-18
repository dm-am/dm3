/**
 * @vitest-environment jsdom
 */

/**
 * "Перенести мой черный список" in the form of a new blog.
 *
 * It stood on every new blog, empty personal list or not, and promised to
 * close likes the list no longer closes. Now it is there only when there is
 * somebody to carry, and says who.
 *
 * What these catch:
 *  - the box drawn over an empty list, or over a list that did not come;
 *  - a name left out, or put away under "и еще N": the line names everyone
 *    the box carries, however long the list (owner, 12 October 2026: nothing
 *    shortened, no "Еще"), and reads the list page by page to do so;
 *  - the box drawn over a list that came only in part;
 *  - a login broken over two lines at its hyphen ("Player-" and "Two"): a
 *    login is never split anywhere else on the site, so each name is a piece
 *    of its own that does not break, and the line breaks between them;
 *  - the box starting anywhere but where the account setting stands, or drawn
 *    before the setting has come, where the setting could still overwrite a
 *    tick the author has made;
 *  - a value put into the form while there is no box: the server would then
 *    not apply the account setting itself.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { readFileSync } from "fs";
import { resolve } from "path";
import { blacklistApi } from "@/entities/user";
import type { BlacklistSettings } from "@/shared/api/models/personal";
import CopyBlacklistOption from "./CopyBlacklistOption.vue";

const DOES = "Внесет в черный список блога";

const page = (names: string[], total = names.length) =>
  ({
    data: {
      resources: names.map((username, i) => ({
        id: `e-${i}`,
        username,
        createdUtc: "2026-09-21T10:00:00Z",
      })),
      paging: { pages: 1, current: 1, skip: 0, take: 3, total },
    },
    error: null,
  }) as never;

/**
 * The server over a list of `names`: the page asked for, and the count of the
 * whole list, as GET users/me/blacklist answers.
 */
const server = (names: string[]) => (skip?: number, take?: number) => {
  const from = skip ?? 0;
  const size = take ?? 50;
  return Promise.resolve({
    data: {
      resources: names.slice(from, from + size).map((username, i) => ({
        id: `e-${from + i}`,
        username,
        createdUtc: "2026-09-21T10:00:00Z",
      })),
      paging: {
        pages: Math.ceil(names.length / size),
        current: Math.floor(from / size) + 1,
        skip: from,
        take: size,
        total: names.length,
      },
    },
    error: null,
  }) as never;
};

const SEVEN = [
  "Player-Two",
  "LongestLoginPossible",
  "RatingNegative",
  "Повелитель_Драконов1",
  "Mordred_Moon",
  "Моргана",
  "Ланселот",
];

const settingsWith = (autoPopulateContentBlacklist: boolean) =>
  ({
    data: {
      hideComments: false,
      hideMessages: false,
      hideGames: false,
      hideBlogs: false,
      blockDirectMessages: false,
      autoPopulateContentBlacklist,
    } satisfies BlacklistSettings,
    error: null,
  }) as never;

const failed = {
  data: null,
  error: { type: "", title: "Ошибка сервера", status: 500, traceId: "t" },
} as never;

function mountOption() {
  return mount(CopyBlacklistOption, {
    props: {
      does: DOES,
      modelValue: undefined,
      "onUpdate:modelValue": vi.fn(),
    },
  });
}

type Option = ReturnType<typeof mountOption>;

/** What the form last got from the box; undefined if it got nothing. */
const sent = (w: Option) =>
  w.emitted<[boolean]>("update:modelValue")?.at(-1)?.[0];

describe("CopyBlacklistOption", () => {
  beforeEach(() => vi.restoreAllMocks());
  afterEach(() => vi.restoreAllMocks());

  it("не рисуется, когда в личном списке никого", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(page([]));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(false);
    expect(w.text()).toBe("");
    expect(sent(w)).toBeUndefined();
  });

  it("не рисуется, когда список не пришел", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(failed);
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(false);
    expect(sent(w)).toBeUndefined();
  });

  it("называет внесенных под галочкой", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
      page(["Player-Two", "LongestLoginPossible", "RatingNegative"]),
    );
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    const w = mountOption();
    await flushPromises();

    expect(w.get("label").text()).toBe("Перенести мой черный список");
    expect(w.get(".copy-blacklist-hint").text()).toBe(
      `${DOES}: Player-Two, LongestLoginPossible, RatingNegative`,
    );
  });

  it('называет всех внесенных, без "и еще N"', async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockImplementation(server(SEVEN));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    const w = mountOption();
    await flushPromises();

    expect(w.get(".copy-blacklist-hint").text()).toBe(
      `${DOES}: ${SEVEN.join(", ")}`,
    );
    expect(w.text()).not.toMatch(/еще|…|\.\.\./);
  });

  // The server hands the list in pages of 200 at most (PagingQuery.Take): a
  // list longer than a page is read on until it is whole.
  it("дочитывает длинный список страницами и называет каждого", async () => {
    const all = Array.from({ length: 205 }, (_, i) => `User${i + 1}`);
    const list = vi
      .spyOn(blacklistApi, "getBlacklist")
      .mockImplementation(server(all));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    const w = mountOption();
    await flushPromises();

    expect(w.findAll(".copy-blacklist-name").map((n) => n.text())).toEqual(
      all.map((name, i) => (i < all.length - 1 ? `${name},` : name)),
    );
    for (const [, take] of list.mock.calls)
      expect(take).toBeLessThanOrEqual(200);
    expect(w.text()).not.toContain("еще");
  });

  it("не рисуется, когда пришла только часть списка", async () => {
    const all = Array.from({ length: 205 }, (_, i) => `User${i + 1}`);
    const first = server(all);
    vi.spyOn(blacklistApi, "getBlacklist").mockImplementation((skip, take) =>
      (skip ?? 0) === 0 ? first(skip, take) : (failed as never),
    );
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(false);
    expect(sent(w)).toBeUndefined();
  });

  it("держит каждое имя целым куском, а строка рвется между ними", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockImplementation(server(SEVEN));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    const w = mountOption();
    await flushPromises();

    // The comma goes with its name; the space between the pieces is the
    // line's, so a copy reads as it did.
    expect(w.findAll(".copy-blacklist-name").map((n) => n.text())).toEqual([
      "Player-Two,",
      "LongestLoginPossible,",
      "RatingNegative,",
      "Повелитель_Драконов1,",
      "Mordred_Moon,",
      "Моргана,",
      "Ланселот",
    ]);
    expect(w.get(".copy-blacklist-hint").text()).toBe(
      `${DOES}: ${SEVEN.join(", ")}`,
    );
    const style = readFileSync(
      resolve(__dirname, "CopyBlacklistOption.vue"),
      "utf8",
    ).split("<style")[1];
    expect(style).toMatch(/\.copy-blacklist-name\s*\n\s*white-space: nowrap/);
  });

  it.each([true, false])(
    "стоит так, как велит настройка аккаунта (%s)",
    async (setting) => {
      vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
        page(["Player-Two"]),
      );
      vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
        settingsWith(setting),
      );
      const w = mountOption();
      await flushPromises();

      expect(sent(w)).toBe(setting);
    },
  );

  it("без настройки стоит снятой", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
      page(["Player-Two"]),
    );
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(failed);
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(true);
    expect(sent(w)).toBe(false);
  });

  it("не рисуется, пока не пришла настройка: ее нечем перебить", async () => {
    let answer: (value: unknown) => void = () => {};
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
      page(["Player-Two"]),
    );
    vi.spyOn(blacklistApi, "getSettings").mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }) as never,
    );
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(false);

    answer(settingsWith(true));
    await flushPromises();

    expect(w.find("input").exists()).toBe(true);
    expect(sent(w)).toBe(true);
  });
});
