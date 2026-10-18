/**
 * @vitest-environment jsdom
 */

/**
 * A refused creation has one carrier, and this form is not always it.
 *
 * What the form can show, it shows under the field the server named. Everything
 * else used to go out as a toast of its own — beside the toast the response
 * interceptor raises for the very same answer. On a broken server the author
 * got "Ошибка сервера" twice, on a blocked account the interceptor's sentence
 * and this one both, and error toasts do not fade: the pair sat on the screen
 * until they were dismissed by hand.
 *
 * So the statuses the interceptor speaks for are left to it, and what is left
 * over — a conflict, a missing blog, a 400 that named no field of this form —
 * is still said here, because nobody else would.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { mount, flushPromises } from "@vue/test-utils";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import { createPinia } from "pinia";
import { blogApi } from "@/entities/blog";
import { blacklistApi } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import type { BlacklistSettings } from "@/shared/api/models/personal";
import CreateBlogForm from "./CreateBlogForm.vue";

// One instance for the whole file, so "and nothing else said it" is a question
// this can answer.
const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
  info: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

// The form leaves for the blog it created, and it guards the way out while the
// fields hold text (UnsavedChangesGuard registers onBeforeRouteLeave). Both are
// doubles; a mock that leaves the hook out fails the mount instead of the
// assertion.
vi.mock("vue-router", () => ({
  useRouter: () => ({ push: vi.fn() }),
  onBeforeRouteLeave: vi.fn(),
}));

// The description field drags the whole tiptap stack in and none of it is the
// subject here. A render function, not a `template` string: the test build runs
// the runtime-only Vue.
vi.mock("@/shared/ui/BBCodeEditor", async () => {
  const { defineComponent, h } = await import("vue");
  return {
    BBCodeEditor: defineComponent({
      name: "BBCodeEditor",
      props: { modelValue: { type: String, default: "" } },
      setup: (props) => () =>
        h("div", { class: "editor-stub" }, props.modelValue),
    }),
  };
});

/** A problem document, the shape the API answers a refusal with. */
const problem = (status: number, title: string): GeneralError => ({
  type: "",
  title,
  status,
  traceId: "trace",
});

/** The account's blacklist settings, with "Переносить..." as given. */
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

/** A first page of the author's own blacklist, with the count of all. */
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

/** Fill the one required field and send the form into the given answer. */
async function submitInto(error: GeneralError) {
  vi.spyOn(blogApi, "createBlog").mockResolvedValue({
    data: null,
    error,
  } as never);

  const wrapper = mount(CreateBlogForm, {
    global: { plugins: [createPinia()] },
  });
  await wrapper.find("#title").setValue("Записки Лекса");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

describe("CreateBlogForm", () => {
  beforeEach(() => {
    localStorage.clear();
    // The form submits for a signed-in viewer only, and the auth store reads
    // who that is from here.
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    vi.clearAllMocks();
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(listOf([]));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // What no field shows stands under the button that was pressed, the three
  // the response interceptor used to plate included, and no plate goes up
  // (the owner's rule of 26 September 2026).
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
    // A 404 carries no title here; the fallback is the form's own business.
    const wrapper = await submitInto(problem(404, ""));

    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось создать блог",
    );
    expect(toast.error).not.toHaveBeenCalled();
  });
});

/**
 * "Перенести мой черный список" (CopyBlacklistOption, which has its own spec
 * for what it draws): what the form sends of it. The box starts where the
 * account setting "Переносить список в мои новые игры и блоги" stands, and the
 * form sends what the box shows. Where there is no box - nobody on the
 * author's own list - the field is left out, and the server applies the
 * setting itself: a false sent from a box nobody saw was how the setting once
 * never reached a blog.
 */
describe("CreateBlogForm: перенос черного списка", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const copyBox = (wrapper: ReturnType<typeof mount>) =>
    wrapper.find<HTMLInputElement>(".copy-blacklist input[type='checkbox']");

  /** The form mounted and sent; the payload of the creation it asked for. */
  async function sent(wrapper: ReturnType<typeof mount>) {
    const create = vi.spyOn(blogApi, "createBlog").mockResolvedValue({
      data: null,
      error: problem(409, "Блог с таким названием уже есть"),
    } as never);
    await wrapper.find("#title").setValue("Записки Лекса");
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    return create.mock.calls[0][0];
  }

  const mountForm = () =>
    mount(CreateBlogForm, { global: { plugins: [createPinia()] } });

  it("без людей в личном списке галочки нет, и поле не уходит", async () => {
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(listOf([]));
    const wrapper = mountForm();
    await flushPromises();

    expect(copyBox(wrapper).exists()).toBe(false);
    expect(wrapper.text()).not.toContain("черный список");
    // Undefined is left out of the request body: the setting decides.
    expect((await sent(wrapper)).copyBlacklist).toBeUndefined();
  });

  it.each([true, false])(
    "берет галочку из настройки аккаунта (%s) и шлет ее",
    async (setting) => {
      vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
        settingsWith(setting),
      );
      vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
        listOf(["Player-Two"]),
      );
      const wrapper = mountForm();
      await flushPromises();

      expect(copyBox(wrapper).element.checked).toBe(setting);
      expect((await sent(wrapper)).copyBlacklist).toBe(setting);
    },
  );

  it("шлет выбор автора", async () => {
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(settingsWith(true));
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(
      listOf(["Player-Two"]),
    );
    const wrapper = mountForm();
    await flushPromises();

    await copyBox(wrapper).setValue(false);

    expect((await sent(wrapper)).copyBlacklist).toBe(false);
  });

  // What the box does, and to whom: every name on the list is said, the ones
  // of its second page too.
  it("говорит под галочкой, что она делает и с кем", async () => {
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
    const wrapper = mountForm();
    await flushPromises();

    expect(wrapper.get(".copy-blacklist-hint").text()).toBe(
      "Внесет в черный список блога: Player-Two, LongestLoginPossible, RatingNegative, Mordred_Moon, Повелитель_Драконов1",
    );
  });
});

/**
 * The form of a new blog is laid out as the form of a new game, DM2's one
 * form of captioned rows, and says the blog in the making in the site's word.
 *
 * What these catch: the blog's rows laid out otherwise than the game's (the
 * two forms are one pattern); "черновик" back in a caption (the site says a
 * blog in the making, never a draft); the switch of the comments back on the
 * form or in the request, where there is nothing to switch (comments are never
 * off).
 */
describe("CreateBlogForm: как форма игры", () => {
  const HERE = dirname(fileURLToPath(import.meta.url));
  const blog = ruleMap(componentCss(resolve(HERE, "CreateBlogForm.vue")));
  const game = ruleMap(
    componentCss(resolve(HERE, "../../create-game/ui/CreateGameForm.vue")),
  );

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    vi.spyOn(blacklistApi, "getSettings").mockResolvedValue(
      settingsWith(false),
    );
    vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue(listOf([]));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("кладет поля рядами формы игры: подпись слева, поле справа", () => {
    const row = blog.get(".create-blog-form .row");

    expect(row?.display).toBe("grid");
    expect(row).toEqual(game.get(".create-game-form .row"));
  });

  it("называет поля словами сайта и не знает черновика", async () => {
    const wrapper = mount(CreateBlogForm, {
      global: { plugins: [createPinia()] },
    });
    await flushPromises();

    expect(
      wrapper.findAll(".form-field-label label").map((l) => l.text()),
    ).toEqual(["Название:", "Кто видит оформляющийся блог:", "Описание:"]);
    expect(
      wrapper.findAll("#blog-draft-visibility option").map((o) => o.text()),
    ).toEqual(["Все", "Только участники"]);
    expect(wrapper.text()).not.toMatch(/черновик/i);
    expect(wrapper.text()).not.toContain("Комментарии");
  });

  it("не шлет выключателя комментариев", async () => {
    const create = vi.spyOn(blogApi, "createBlog").mockResolvedValue({
      data: null,
      error: problem(409, "Блог с таким названием уже есть"),
    } as never);
    const wrapper = mount(CreateBlogForm, {
      global: { plugins: [createPinia()] },
    });
    await wrapper.find("#title").setValue("Записки Лекса");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(create.mock.calls[0][0]).not.toHaveProperty("commentsEnabled");
  });
});
