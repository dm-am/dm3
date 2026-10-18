/**
 * @vitest-environment jsdom
 */

/**
 * The blog's blacklist: what the section hands the shared list.
 *
 * The twin of pages/game/settings/BlacklistSection.spec.ts, and it is a
 * separate file for the same reason the two sections are separate: they share
 * BlacklistEditor and nothing else.
 *
 * A name is put on with the field and "Добавить", the last line of the
 * table of the list, which wait
 * for the `add` the section hands them. What these catch:
 *  - `await` dropped from the reload: the field empties over the old list,
 *    and the row arrives a round trip later;
 *  - the reload dropped altogether: the name never appears without F5;
 *  - a refused add reported as a good one, or said in a plate in the corner
 *    as well as under the field;
 *  - the field suggesting the author and the assistants, whom the server
 *    refuses;
 *  - a list that did not come said to be empty;
 *  - the line of what the list does in other words than the blog's.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, useBlogDetailsStore, type Blog } from "@/entities/blog";
import { UserAutocomplete } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { User } from "@/shared/api/models/common";
import BlacklistSection from "./BlacklistSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const aUser = (username: string): User =>
  ({ id: `id-${username}`, username }) as unknown as User;

const LANCELOT = aUser("Ланселот");
const MORDRED = aUser("Мордред");

/** A list request nobody has answered yet. */
function pending<T>() {
  let settle: (value: T) => void = () => {};
  const promise = new Promise<T>((resolve) => {
    settle = resolve;
  });
  return { promise, answer: (value: T) => settle(value) };
}

const listOf = (users: User[]) =>
  ({ data: { resources: users }, error: null }) as never;

/**
 * The blacklist of a blog, open in front of its author: one name on screen,
 * and the second list request held back, so the test can look at the screen
 * between the answer to the block and the answer to the list.
 */
async function mountSection(first: unknown = listOf([LANCELOT])) {
  setActivePinia(createPinia());
  const store = useBlogDetailsStore();
  store.blog = {
    id: "the-blog-guid",
    title: "Записки",
    author: { username: "Мерлин" },
    assistants: [{ username: "Кей" }],
  } as unknown as Blog;

  const reload = pending<unknown>();
  vi.spyOn(blogApi, "getBlacklist")
    .mockResolvedValueOnce(first as never)
    .mockImplementationOnce(() => reload.promise as never);

  const wrapper = mount(BlacklistSection, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return { wrapper, reload };
}

type Section = Awaited<ReturnType<typeof mountSection>>["wrapper"];

const namesOnScreen = (wrapper: Section) =>
  wrapper
    .findAll("tbody tr.table-row .col-user .user-link")
    .map((link) => link.text());

const field = (wrapper: Section) => wrapper.getComponent(UserAutocomplete);

/** Picks a name in the field and presses "Добавить". */
async function addByField(wrapper: Section, username: string) {
  field(wrapper).vm.$emit("update:modelValue", username);
  await flushPromises();
  const button = wrapper.findAll("button").find((b) => b.text() === "Добавить");
  await button!.trigger("click");
  await flushPromises();
}

describe("BlacklistSection (блог)", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => vi.restoreAllMocks());

  it("не очищает поле, пока список не показал новое имя", async () => {
    const { wrapper, reload } = await mountSection();
    const add = vi.spyOn(blogApi, "addToBlacklist").mockResolvedValue({
      data: MORDRED,
      error: null,
    } as never);

    await addByField(wrapper, "Мордред");

    // The server took the block; the list has not answered yet. The field
    // keeps the name, so it does not empty over a list that says nothing.
    expect(add).toHaveBeenCalledWith("the-blog-guid", "Мордред");
    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
    expect(field(wrapper).props("modelValue")).toBe("Мордред");

    reload.answer(listOf([LANCELOT, MORDRED]));
    await flushPromises();

    expect(namesOnScreen(wrapper)).toEqual(["Ланселот", "Мордред"]);
    expect(field(wrapper).props("modelValue")).toBe("");
    expect(blogApi.getBlacklist).toHaveBeenLastCalledWith("the-blog-guid");
    expect(saidOutLoud()).toEqual([]);
  });

  it("пишет отказ под полем и не трогает список", async () => {
    const { wrapper } = await mountSection();
    vi.spyOn(blogApi, "addToBlacklist").mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Сначала уберите пользователя из ассистентов блога",
        status: 409,
        traceId: "t",
      },
    } as never);

    await addByField(wrapper, "Кей");

    expect(wrapper.get(".bl-add .action-result").text()).toBe(
      "Сначала уберите пользователя из ассистентов блога",
    );
    expect(field(wrapper).props("modelValue")).toBe("Кей");
    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
    expect(blogApi.getBlacklist).toHaveBeenCalledTimes(1);
    expect(saidOutLoud()).toEqual([]);
  });

  it("не дает полю подсказывать тех, кто ведет блог", async () => {
    const { wrapper } = await mountSection();

    expect([...(field(wrapper).props("exclude") as string[])]).toEqual(
      expect.arrayContaining(["Мерлин", "Кей", "Ланселот"]),
    );
  });

  // The bin of a row answers under the row of the name it was pressed on,
  // and the name stays in the list; it used to be a plate in the corner.
  it("на отказе удаления оставляет имя и говорит об отказе под ним", async () => {
    const { wrapper } = await mountSection();
    vi.spyOn(blogApi, "removeFromBlacklist").mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "t" },
    } as never);

    const remove = wrapper.get("tbody tr.table-row .col-action button");
    expect(remove.attributes("aria-label")).toBe(
      "Удалить Ланселот из черного списка",
    );
    await remove.trigger("click");
    await flushPromises();

    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
    expect(wrapper.get(".table-details-row .action-result").text()).toBe(
      "Пользователь не удален из черного списка",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  it("говорит о неудачной загрузке, а не о пустом списке", async () => {
    const { wrapper } = await mountSection({
      data: null,
      error: { status: 500, title: "", type: "", traceId: "t" },
    });

    expect(wrapper.text()).toContain("Не удалось загрузить черный список");
    expect(wrapper.text()).not.toContain("В черном списке пока никого нет");
  });

  // What the list does, in this blog's own words:
  // the editor is shared, the line is the section's.
  it("говорит под заголовком, что делает список", async () => {
    const { wrapper } = await mountSection();

    expect(wrapper.get(".settings-section h2").text()).toBe("Черный список");
    expect(wrapper.get(".bl-hint").text()).toBe(
      "Внесенные не пишут комментарии в блоге.",
    );
  });
});
