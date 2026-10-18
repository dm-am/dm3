/**
 * @vitest-environment jsdom
 */

/**
 * How the blog information form reports a save.
 *
 * It cannot report one by changing: the fields after a save hold the very
 * values the reader typed into them, and the section used to answer with a
 * toast in the corner because of it. The answer now belongs to the submit
 * button — "Сохранено" and inert while the form matches the server's copy,
 * "Сохранить" again the moment a field moves.
 *
 * What each test here catches, since a button that always reads "Сохранить"
 * looks perfectly normal on screen:
 *  - remember() left out of save(), or called before the request: the button
 *    never changes, or changes over a save that never happened;
 *  - a comparison that reads the title alone: the choice of who sees the
 *    blog in the making stops taking the button back to "Сохранить";
 *  - remember() above the error return in save(): a refused save reads as a
 *    successful one;
 *  - the toast put back beside the button: one event announced twice.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  blogApi,
  DraftVisibility,
  useBlogDetailsStore,
  type Blog,
} from "@/entities/blog";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import BlogInfoSection from "./BlogInfoSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** A blog whose settings are open in front of its owner. */
function mountSection() {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  store.blog = {
    id: "the-blog",
    title: "Дневник",
    draftVisibility: DraftVisibility.Public,
    author: { username: "the-owner" },
    assistants: [],
  } as unknown as Blog;
  // The reload after a successful save is not this spec's subject, and left
  // real it would reach for the endpoint.
  vi.spyOn(store, "loadBlog").mockResolvedValue({ ok: true });

  return mount(BlogInfoSection, { global: { plugins: [pinia] } });
}

/** The submit button of the form, the one control that answers for the save. */
const submitButton = (wrapper: VueWrapper) => wrapper.find(".controls button");

/** What the button says, and whether it can still be pressed. */
const answer = (wrapper: VueWrapper) => ({
  label: submitButton(wrapper).text(),
  inert: submitButton(wrapper).attributes("disabled") !== undefined,
});

/** Press the submit and let the request, the reload and the tick settle. */
async function save(wrapper: VueWrapper) {
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

describe("BlogInfoSection save confirmation", () => {
  let update: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    update = vi.fn().mockResolvedValue({ data: null, error: null });
    vi.spyOn(blogApi, "updateBlog").mockImplementation(
      update as unknown as typeof blogApi.updateBlog,
    );
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("does not claim a save the reader has not made", () => {
    const wrapper = mountSection();

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("reads Сохранено and stops answering once the server confirms", async () => {
    const wrapper = mountSection();

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
  });

  it("says nothing in the corner about the save it has just shown", async () => {
    const wrapper = mountSection();

    await save(wrapper);

    expect(saidOutLoud()).toEqual([]);
  });

  it("offers the save again as soon as the title changes", async () => {
    const wrapper = mountSection();
    await save(wrapper);

    await wrapper.find("#blog-title").setValue("Другое название");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  // The field the button forgets when the comparison is written against the
  // title alone: the choice travels in the same patch and has to count.
  it("offers the save again when who sees the blog in the making changes", async () => {
    const wrapper = mountSection();
    await save(wrapper);

    await wrapper
      .find("#blog-settings-draft-visibility")
      .setValue(DraftVisibility.Private);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("sends exactly the fields the button watches", async () => {
    const wrapper = mountSection();
    await wrapper.find("#blog-title").setValue("Новое название");

    await save(wrapper);

    expect(update).toHaveBeenCalledTimes(1);
    expect(update.mock.calls[0][1]).toEqual({
      title: "Новое название",
      draftVisibility: DraftVisibility.Public,
    });
    expect(answer(wrapper).label).toBe("Сохранено");
  });

  it("keeps offering the save after the server refuses it", async () => {
    const wrapper = mountSection();
    // A 400: the statuses the interceptor speaks for are the ones the section
    // stays quiet about, and this is not one of them.
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
    });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    // Where the press was, and in no plate (the owner's rule of 26 September
    // 2026, UI_STANDARDS "Всплывающие сообщения").
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось сохранить информацию блога",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

/**
 * The settings of a blog are the settings of a game in small: DM2's captioned
 * rows, the blog in the making said in the site's word, and no switch of the
 * comments, which are never off.
 */
describe("BlogInfoSection as DM2's settings", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("captions its fields in the site's words, with no draft and no comments switch", () => {
    const wrapper = mountSection();

    expect(
      wrapper.findAll(".form-field-label label").map((l) => l.text()),
    ).toEqual(["Название:", "Кто видит оформляющийся блог:"]);
    expect(
      wrapper
        .findAll("#blog-settings-draft-visibility option")
        .map((o) => o.text()),
    ).toEqual(["Все", "Только участники"]);
    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false);
    expect(wrapper.text()).not.toMatch(/черновик|Комментарии/i);
  });
});
