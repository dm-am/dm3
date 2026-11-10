/**
 * @vitest-environment jsdom
 */

// A created rubric is asked for one past the highest place, so it is the row
// above the field; a delete takes the row out; neither raises a toast.
import {
  describe,
  it,
  expect,
  vi,
  beforeEach,
  afterEach,
  type Mock,
} from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  blogApi,
  useBlogDetailsStore,
  type Blog,
  type CreateRubricInput,
  type Rubric,
} from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import RubricsSection from "./RubricsSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** A rubric as the blog serves it. */
const rubric = (id: string, title: string, sortOrder: number): Rubric => ({
  id,
  title,
  sortOrder,
  publicationCount: 0,
  unreadPublicationsCount: 0,
  unreadCommentsCount: 0,
});

/** The two rubrics the blog starts with, already ordered by their place. */
const EXISTING = [rubric("r-1", "Бестиарий", 1), rubric("r-2", "Хроники", 2)];

// The confirmation of a delete is the real window, drawn in place.
const stubs = { Teleport: true };

let created: Mock;
let deleted: Mock;

/**
 * The section over a blog with two rubrics, with the real store and the API
 * doubled: the server keeps the position it is handed
 * (BlogRepository.CreateRubric) and serves the blog with its rubrics again.
 */
function mountSection() {
  const pinia = createPinia();
  setActivePinia(pinia);
  const held = [...EXISTING];
  const served = (): Blog => ({
    ...createMockBlog("the-blog", "Дневник"),
    rubrics: [...held],
  });
  useBlogDetailsStore().blog = served();

  created = vi
    .spyOn(blogApi, "createRubric")
    .mockImplementation(async (_blogId, input: CreateRubricInput) => {
      held.push(rubric("r-new", input.title, input.sortOrder ?? 0));
      return { data: null, error: null };
    }) as unknown as Mock;
  deleted = vi
    .spyOn(blogApi, "deleteRubric")
    .mockImplementation(async (_blogId, rubricId: string) => {
      held.splice(
        held.findIndex((one) => one.id === rubricId),
        1,
      );
      return { data: null, error: null };
    }) as unknown as Mock;
  vi.spyOn(blogApi, "getBlog").mockImplementation(
    async () => ({ data: { resource: served() }, error: null }) as never,
  );

  return mount(RubricsSection, { global: { plugins: [pinia], stubs } });
}

/** The rubric list as the reader reads it, top to bottom. */
const titles = (wrapper: VueWrapper) =>
  wrapper.findAll(".rubric-title").map((row) => row.text());

/** Name a rubric and press "Создать". */
async function create(wrapper: VueWrapper, title: string) {
  await wrapper.find('input[aria-label="Название рубрики"]').setValue(title);
  await wrapper.find(".add-row button").trigger("click");
  await flushPromises();
}

describe("RubricsSection create", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("puts the new rubric last, in the row above the field it was named in", async () => {
    const wrapper = mountSection();

    await create(wrapper, "Черновики");

    expect(titles(wrapper)).toEqual(["Бестиарий", "Хроники", "Черновики"]);
  });

  it("asks for the place one past the highest the blog holds", async () => {
    const wrapper = mountSection();

    await create(wrapper, "Черновики");

    expect(created).toHaveBeenCalledTimes(1);
    expect(created.mock.calls[0][1]).toEqual({
      title: "Черновики",
      sortOrder: 3,
    });
  });

  it("says nothing in the corner about the row it has just shown", async () => {
    const wrapper = mountSection();

    await create(wrapper, "Черновики");

    expect(saidOutLoud()).toEqual([]);
    expect(
      (
        wrapper.find('input[aria-label="Название рубрики"]')
          .element as HTMLInputElement
      ).value,
    ).toBe("");
  });

  it("keeps the name in the field and speaks up when the server refuses", async () => {
    const wrapper = mountSection();
    created.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
    });

    await create(wrapper, "Черновики");

    expect(titles(wrapper)).toEqual(["Бестиарий", "Хроники"]);
    // Where the press was, and in no plate.
    expect(wrapper.get(".add-row + .action-result").text()).toBe(
      "Не удалось создать рубрику",
    );
    expect(saidOutLoud()).toEqual([]);
    expect(
      (
        wrapper.find('input[aria-label="Название рубрики"]')
          .element as HTMLInputElement
      ).value,
    ).toBe("Черновики");
  });
});

describe("RubricsSection delete", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("takes the row out of the list and says nothing about it", async () => {
    const wrapper = mountSection();

    await wrapper.findAll(".remove-btn")[0].trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(deleted).toHaveBeenCalledWith("the-blog", "r-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(titles(wrapper)).toEqual(["Хроники"]);
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
    expect(saidOutLoud()).toEqual([]);
  });

  it("says a refused delete in the window, which stays open over the list", async () => {
    const wrapper = mountSection();
    deleted.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
    });

    await wrapper.findAll(".remove-btn")[0].trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(titles(wrapper)).toEqual(["Бестиарий", "Хроники"]);
    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить рубрику",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });
});
