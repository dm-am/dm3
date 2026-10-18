/**
 * @vitest-environment jsdom
 */

/**
 * What the rubric section shows for the two things it can do.
 *
 * Both used to answer with a line in the corner, and both move the list that
 * is on screen instead - but only the delete did it where the reader was
 * looking. A created rubric was sent with no position at all, so the server
 * filed it under SortOrder 0 and it took its place among the other zeros by
 * title: in a blog with a dozen rubrics, somewhere in the middle of a list the
 * reader had just scrolled past to reach the field. The section now asks for
 * the place itself, one past the highest, which is the row directly above that
 * field.
 *
 * What each test here catches, since a rubric that lands anywhere at all looks
 * created either way:
 *  - the position left out of the request: the new row goes back to sorting by
 *    title among the zeros, and the first test reads it in the wrong place;
 *  - the position computed from the count instead of the highest order: a blog
 *    whose rubrics were reordered gets a collision, and the second test names
 *    the number that is actually sent;
 *  - either toast put back: one event announced twice, and the list below is
 *    the half that does not need announcing.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useBlogDetailsStore,
  type Blog,
  type CreateRubricInput,
  type Rubric,
} from "@/entities/blog";
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

// The confirmation of a delete is the real window, drawn in place: the delete
// is its request, and a refusal is printed in it.
const stubs = { Teleport: true };

let created: ReturnType<typeof vi.fn>;
let deleted: ReturnType<typeof vi.fn>;

/**
 * The section over a blog with two rubrics.
 *
 * The store stands in for the round trip it makes: the endpoint keeps the
 * position it is handed (BlogRepository.CreateRubric), the blog is read again,
 * and the list is ordered by that position on the way to the screen. Which is
 * why a request without one shows up here as a row in the wrong place, exactly
 * as it does in the browser.
 */
function mountSection() {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  store.blog = {
    id: "the-blog",
    title: "Дневник",
    rubrics: [...EXISTING],
  } as unknown as Blog;

  created = vi.fn(async (input: CreateRubricInput) => {
    store.blog!.rubrics!.push(
      rubric("r-new", input.title, input.sortOrder ?? 0),
    );
    return null;
  });
  deleted = vi.fn(async (rubricId: string) => {
    store.blog!.rubrics = store.blog!.rubrics!.filter((r) => r.id !== rubricId);
    return null;
  });
  vi.spyOn(store, "createRubric").mockImplementation(
    created as unknown as typeof store.createRubric,
  );
  vi.spyOn(store, "deleteRubric").mockImplementation(
    deleted as unknown as typeof store.deleteRubric,
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
    expect(created.mock.calls[0][0]).toEqual({
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
      status: 400,
      title: "",
      type: "",
      traceId: "t",
    } as GeneralError);

    await create(wrapper, "Черновики");

    expect(titles(wrapper)).toEqual(["Бестиарий", "Хроники"]);
    // Where the press was, and in no plate (the owner's rule of 26 September
    // 2026, UI_STANDARDS "Всплывающие сообщения").
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

    expect(deleted).toHaveBeenCalledWith("r-1", {
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
      status: 400,
      title: "",
      type: "",
      traceId: "t",
    } as GeneralError);

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
