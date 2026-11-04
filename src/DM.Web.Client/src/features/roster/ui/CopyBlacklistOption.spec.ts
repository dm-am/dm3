/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
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

// The page asked for and the count of the whole list, as GET users/me/blacklist answers.
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

const sent = (w: Option) =>
  w.emitted<[boolean]>("update:modelValue")?.at(-1)?.[0];

describe("CopyBlacklistOption", () => {
  beforeEach(() => vi.restoreAllMocks());
  afterEach(() => vi.restoreAllMocks());

  it("is not drawn when the own list is empty", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(page([]));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(false);
    expect(w.text()).toBe("");
    expect(sent(w)).toBeUndefined();
  });

  it("is not drawn when the list did not come", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(failed);
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(false);
    expect(sent(w)).toBeUndefined();
  });

  it("names the listed under the box", async () => {
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

  it('names everyone listed, with no "и еще N"', async () => {
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

  // The server hands pages of 200 at most, so a longer list is read on until whole.
  it("reads a long list page by page and names everyone", async () => {
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

  it("is not drawn when only part of the list came", async () => {
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

  it("keeps each name whole and breaks the line between them", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockImplementation(server(SEVEN));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    const w = mountOption();
    await flushPromises();

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
  });

  it.each([true, false])(
    "starts as the account setting says (%s)",
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

  it("starts unticked without the setting", async () => {
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
      page(["Player-Two"]),
    );
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(failed);
    const w = mountOption();
    await flushPromises();

    expect(w.find("input").exists()).toBe(true);
    expect(sent(w)).toBe(false);
  });

  it("is not drawn before the setting comes", async () => {
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
