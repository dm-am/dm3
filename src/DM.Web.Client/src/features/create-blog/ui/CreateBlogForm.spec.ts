/**
 * @vitest-environment jsdom
 */

// The statuses the interceptor speaks for are left to it; the rest is said by the form.

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { blogApi } from "@/entities/blog";
import { blacklistApi } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { problem } from "@/shared/lib/testing/problem";
import { mountPage } from "@/shared/lib/testing/mountPage";
import type { BlacklistSettings } from "@/shared/api/models/personal";
import CreateBlogForm from "./CreateBlogForm.vue";

const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
  info: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

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

const listOf = (names: string[], total = names.length) =>
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

// Routed: the unsaved-changes guard of the form runs on leaving it.
const mountForm = () =>
  mountPage(CreateBlogForm, "/blogs/create", {
    routes: [
      ["create-blog", "/blogs/create"],
      ["blog", "/blogs/:id"],
    ],
    routed: true,
  });

async function submitInto(error: GeneralError) {
  vi.spyOn(blogApi, "createBlog").mockResolvedValue({
    data: null,
    error,
  } as never);

  const { wrapper } = await mountForm();
  await wrapper.find("#title").setValue("Записки Лекса");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

beforeEach(() => {
  localStorage.clear();
  // The auth store reads the signed-in viewer from here.
  localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
  vi.clearAllMocks();
});

afterEach(() => {
  vi.restoreAllMocks();
});

const withEmptyBlacklist = () => {
  vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(false));
  vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(listOf([]));
};

describe("CreateBlogForm", () => {
  beforeEach(withEmptyBlacklist);

  it.each<[number, string, string]>([
    [403, "Аккаунт заблокирован", "Аккаунт заблокирован"],
    [429, "Слишком много запросов", "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера", "Ошибка сервера"],
    [409, "Блог с таким названием уже есть", "Блог с таким названием уже есть"],
  ])("says a %i under the button", async (status, title, line) => {
    const wrapper = await submitInto(problem(status, title));

    expect(wrapper.get(".form-actions + .action-result").text()).toBe(line);
    expect(toast.error).not.toHaveBeenCalled();
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    const wrapper = await submitInto(problem(404, ""));

    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось создать блог",
    );
    expect(toast.error).not.toHaveBeenCalled();
  });
});

describe("CreateBlogForm: creating", () => {
  beforeEach(withEmptyBlacklist);

  it("opens the new blog by its public name", async () => {
    vi.spyOn(blogApi, "createBlog").mockResolvedValue({
      data: { resource: { id: "b-1", publicId: "records-of-lex" } },
      error: null,
    } as never);
    const { wrapper, router } = await mountForm();
    await wrapper.find("#title").setValue("Записки Лекса");

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe("/blogs/records-of-lex");
  });
});

// Without a box the field is left out, and the server applies the account setting itself.
describe("CreateBlogForm: carrying the blacklist over", () => {
  const copyBox = (wrapper: VueWrapper) =>
    wrapper.find<HTMLInputElement>(".copy-blacklist input[type='checkbox']");

  async function sent(wrapper: VueWrapper) {
    const create = vi.spyOn(blogApi, "createBlog").mockResolvedValue({
      data: null,
      error: problem(409, "Блог с таким названием уже есть"),
    } as never);
    await wrapper.find("#title").setValue("Записки Лекса");
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    return create.mock.calls[0][0];
  }

  it("draws no box and sends no field when the own list is empty", async () => {
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(listOf([]));
    const { wrapper } = await mountForm();
    await flushPromises();

    expect(copyBox(wrapper).exists()).toBe(false);
    expect(wrapper.text()).not.toContain("черный список");
    // Undefined is left out of the request body: the setting decides.
    expect((await sent(wrapper)).copyBlacklist).toBeUndefined();
  });

  it.each([true, false])(
    "takes the box from the account setting (%s) and sends it",
    async (setting) => {
      vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
        settingsWith(setting),
      );
      vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
        listOf(["Player-Two"]),
      );
      const { wrapper } = await mountForm();
      await flushPromises();

      expect(copyBox(wrapper).element.checked).toBe(setting);
      expect((await sent(wrapper)).copyBlacklist).toBe(setting);
    },
  );

  it("sends the author's choice", async () => {
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
      listOf(["Player-Two"]),
    );
    const { wrapper } = await mountForm();
    await flushPromises();

    await copyBox(wrapper).setValue(false);

    expect((await sent(wrapper)).copyBlacklist).toBe(false);
  });

  it("says under the box what it does and to whom", async () => {
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    vi.spyOn(blacklistApi, "getBlacklist")
      .mockResolvedValueOnce(
        listOf(["Player-Two", "LongestLoginPossible", "RatingNegative"], 5),
      )
      .mockResolvedValueOnce(
        listOf(["Mordred_Moon", "Повелитель_Драконов1"], 5),
      );
    const { wrapper } = await mountForm();
    await flushPromises();

    expect(wrapper.get(".copy-blacklist-hint").text()).toBe(
      "Внесет в черный список блога: Player-Two, LongestLoginPossible, RatingNegative, Mordred_Moon, Повелитель_Драконов1",
    );
  });
});

describe("CreateBlogForm: laid out as the game form", () => {
  beforeEach(withEmptyBlacklist);

  it("names the fields in the words of the site", async () => {
    const { wrapper } = await mountForm();
    await flushPromises();

    expect(
      wrapper.findAll(".form-field-label label").map((l) => l.text()),
    ).toEqual(["Название:", "Кто видит оформляющийся блог:", "Описание:"]);
    expect(
      wrapper.findAll("#blog-draft-visibility option").map((o) => o.text()),
    ).toEqual(["Все", "Только участники"]);
  });
});
