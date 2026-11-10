/**
 * @vitest-environment jsdom
 */

// The save is answered by the submit button: "Сохранено" and inert while the
// form matches the server's copy, "Сохранить" again when a field moves, and a
// refused save never reads as a saved one.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, DraftVisibility, useBlogDetailsStore } from "@/entities/blog";
import { useAuthStore, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import BlogInfoSection from "./BlogInfoSection.vue";
import { problem } from "@/shared/lib/testing/problem";
import { expectRefusalUnderField } from "@/shared/lib/testing/formField";

// The editor is tiptap; the form reads its value and nothing else.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** A blog whose settings are open; the reader, when named, is signed in. */
function mountSection(readerId?: string) {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (readerId) useAuthStore().user = { id: readerId } as User;
  const store = useBlogDetailsStore();
  store.blog = {
    ...createMockBlog("the-blog", "Дневник"),
    draftVisibility: DraftVisibility.Public,
    author: createMockUser("owner-id", "the-owner"),
    assistants: [],
  };
  // The reload after a save is not this spec's subject.
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

  // The choice travels in the same patch and has to count.
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
    // A 400: not one of the statuses the interceptor speaks for.
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
    });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    // Where the press was, and in no plate.
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось сохранить информацию блога",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

// DM2's captioned rows, the blog in the making in the site's word, no switch of comments.
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

// The server gives the description as typed to the owner alone: a lead would be
// given it drawn, and a save of the drawing would write the markup over.
describe("BlogInfoSection description", () => {
  const TYPED =
    '[b]Записки[/b] о походах [spoiler text="Карта"]тропа[/spoiler]';
  let update: ReturnType<typeof vi.fn>;
  let read: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    update = vi.fn().mockResolvedValue({ data: null, error: null });
    vi.spyOn(blogApi, "updateBlog").mockImplementation(
      update as unknown as typeof blogApi.updateBlog,
    );
    read = vi.fn().mockResolvedValue({
      data: { resource: { id: "the-blog", description: TYPED } },
      error: null,
    });
    vi.spyOn(blogApi, "getBlogForEdit").mockImplementation(
      read as unknown as typeof blogApi.getBlogForEdit,
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const editor = (wrapper: VueWrapper) =>
    wrapper.findComponent({ name: "BBCodeEditor" });

  it("opens in front of the owner on the text as typed", async () => {
    const wrapper = mountSection("owner-id");
    await flushPromises();

    expect(read).toHaveBeenCalledWith("the-blog");
    expect(editor(wrapper).props("modelValue")).toBe(TYPED);
    expect(
      wrapper.findAll(".form-field-label label").map((l) => l.text()),
    ).toEqual(["Название:", "Кто видит оформляющийся блог:", "Описание:"]);
  });

  it("is not offered to a lead of the blog, and not read for him", async () => {
    const wrapper = mountSection("assistant-id");
    await flushPromises();

    expect(read).not.toHaveBeenCalled();
    expect(editor(wrapper).exists()).toBe(false);
  });

  it("is not sent untouched", async () => {
    const wrapper = mountSection("owner-id");
    await flushPromises();

    await save(wrapper);

    expect(update.mock.calls[0][1]).not.toHaveProperty("description");
  });

  it("is sent as typed once changed, and the save is offered again", async () => {
    const wrapper = mountSection("owner-id");
    await flushPromises();
    await save(wrapper);
    expect(answer(wrapper).label).toBe("Сохранено");

    editor(wrapper).vm.$emit("update:modelValue", "[i]Новое[/i] описание");
    await flushPromises();
    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    await save(wrapper);

    expect(update.mock.calls.at(-1)?.[1]).toMatchObject({
      description: "[i]Новое[/i] описание",
    });
  });

  // An erased description is a change like any other: it goes, and the field
  // then holds what the server kept, not what was sent.
  it("sends an erased description as an empty string and reads the field back", async () => {
    const wrapper = mountSection("owner-id");
    await flushPromises();
    read.mockResolvedValue({
      data: { resource: { id: "the-blog", description: "" } },
      error: null,
    });

    editor(wrapper).vm.$emit("update:modelValue", "");
    await flushPromises();
    await save(wrapper);

    expect(update.mock.calls.at(-1)?.[1]).toMatchObject({ description: "" });
    expect(read).toHaveBeenCalledTimes(2);
    expect(editor(wrapper).props("modelValue")).toBe("");
    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
  });

  it("says the server's refusal of the description under the field, never Сохранено", async () => {
    const wrapper = mountSection("owner-id");
    await flushPromises();
    update.mockResolvedValue({
      data: null,
      error: problem(400, "", {
        errors: { Description: ["Описание нельзя стереть"] },
      } as never),
    });

    editor(wrapper).vm.$emit("update:modelValue", "");
    await flushPromises();
    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    expect(read).toHaveBeenCalledTimes(1);
    await expectRefusalUnderField(
      wrapper,
      "Описание:",
      "Описание нельзя стереть",
      () => {
        editor(wrapper).vm.$emit("update:modelValue", "и снова");
        return flushPromises();
      },
    );
  });

  it("sends the title as typed, spaces included", async () => {
    const wrapper = mountSection();
    await wrapper.find("#blog-title").setValue("  Дневник  ");

    await save(wrapper);

    expect(update.mock.calls.at(-1)?.[1]).toMatchObject({
      title: "  Дневник  ",
    });
  });
});
