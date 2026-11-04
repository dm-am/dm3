/**
 * @vitest-environment jsdom
 */

// Both reads own their refusal: one line stands where the panels would be,
// and the interceptor is told on the request. The 429 title names no number,
// so the wait is checked apart.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  flushPromises,
  enableAutoUnmount,
  type VueWrapper,
} from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
import ModerationTags from "./ModerationTags.vue";
import { mountPage, windows } from "@/shared/lib/testing/mountPage";

const {
  getTagGroups,
  getTags,
  getTagForEdit,
  deleteTagGroup,
  deleteTag,
  publicTags,
} = vi.hoisted(() => ({
  getTagGroups: vi.fn(),
  getTags: vi.fn(),
  getTagForEdit: vi.fn(),
  deleteTagGroup: vi.fn(),
  deleteTag: vi.fn(),
  publicTags: vi.fn(),
}));

// The network only: the games store, the windows and the plates are the real ones.
vi.mock("@/entities/game/api/gameTagApi", () => ({
  default: { getTagGroups, getTags, getTagForEdit, deleteTagGroup, deleteTag },
}));
vi.mock("@/entities/game/api/gameApi", () => ({
  default: { getTags: publicTags },
}));

/** What the page says it will answer for, on both reads. */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true };

const served = (resources: unknown[]) => ({
  data: { resources, paging: null },
  error: null,
});

const refused = (error: Record<string, unknown>) => ({ data: null, error });

enableAutoUnmount(afterEach);

async function render() {
  const { wrapper } = await mountPage(ModerationTags, "/moderation/tags", {
    routes: [["moderation-tags", "/moderation/tags"]],
  });
  await flushPromises();
  return wrapper;
}

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

beforeEach(() => {
  const { toasts, dismiss } = useToast();
  for (const toast of [...toasts.value]) dismiss(toast.id);
  publicTags.mockReset();
  publicTags.mockResolvedValue(served([]));
});

describe("the tag section", () => {
  beforeEach(() => {
    getTagGroups.mockReset();
    getTags.mockReset();
    getTagGroups.mockResolvedValue(
      served([{ id: "g-1", title: "Жанр", sortOrder: 0, tagsCount: 1 }]),
    );
    getTags.mockResolvedValue(
      served([
        {
          id: "t-1",
          shortId: 7,
          groupId: "g-1",
          groupTitle: "Жанр",
          title: "Детектив",
          sortOrder: 0,
          gamesCount: 0,
        },
      ]),
    );
  });

  it("draws both panels from what the two reads returned", async () => {
    const wrapper = await render();

    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.text()).toContain("Жанр");
    expect(wrapper.text()).toContain("Детектив");
  });

  // Asserted on the requests: the flag on the way out is what reaches the interceptor.
  it("takes the refusal of both reads over from the interceptor", async () => {
    await render();

    expect(getTagGroups).toHaveBeenCalledWith(OWNS_REFUSAL);
    expect(getTags).toHaveBeenCalledWith(OWNS_REFUSAL);
  });

  it("puts the reason the server named where the panels would be", async () => {
    getTagGroups.mockResolvedValue(
      refused({ type: "Forbidden", status: 403, title: "Раздел закрыт" }),
    );

    const wrapper = await render();

    expect(wrapper.find(".error-message").text()).toBe("Раздел закрыт");
    // Nothing of the section is left standing beside the line.
    expect(wrapper.find(".groups-panel").exists()).toBe(false);
    expect(wrapper.find(".tags-panel").exists()).toBe(false);
  });

  it("reads both lists again from the line's own retry", async () => {
    getTagGroups.mockResolvedValueOnce(
      refused({ type: "ServerError", status: 500, title: "Внутренняя ошибка" }),
    );
    const wrapper = await render();

    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(getTagGroups).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.text()).toContain("Детектив");
  });

  /** The failure of the second read is a failure of the section too. */
  it("answers for a refusal of the tags read as well", async () => {
    getTags.mockResolvedValue(
      refused({ type: "ServerError", status: 500, title: "Внутренняя ошибка" }),
    );

    const wrapper = await render();

    expect(wrapper.find(".error-message").text()).toBe("Внутренняя ошибка");
  });

  // The 429 title carries no number, so the line says the seconds itself.
  it("names the wait when a read is rate limited", async () => {
    getTagGroups.mockResolvedValue(
      refused({
        type: "TooManyRequests",
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      }),
    );

    const wrapper = await render();

    expect(wrapper.find(".error-message").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });
});

// Each confirmation runs its request, prints a refusal in itself and closes on success alone.
describe("the tag section deletes", () => {
  beforeEach(() => {
    getTagGroups.mockReset();
    getTags.mockReset();
    deleteTagGroup.mockReset();
    deleteTag.mockReset();
    getTagGroups.mockResolvedValue(
      served([
        { id: "g-1", title: "Жанр", sortOrder: 0, tagsCount: 1 },
        { id: "g-2", title: "Пустая", sortOrder: 1, tagsCount: 0 },
      ]),
    );
    getTags.mockResolvedValue(
      served([
        {
          id: "t-1",
          shortId: 7,
          groupId: "g-1",
          groupTitle: "Жанр",
          title: "Детектив",
          sortOrder: 0,
          gamesCount: 0,
        },
      ]),
    );
  });

  const confirm = async (wrapper: VueWrapper, label: string) => {
    await wrapper.get(`button[aria-label="${label}"]`).trigger("click");
    await windows().get(".dialog-btn-submit").trigger("click");
    await flushPromises();
  };

  // The server refuses to delete a group that holds tags and a tag that stands
  // in a game, so neither has a trash; the count in the row says why.
  it("draws no trash where the server would refuse the delete", async () => {
    getTags.mockResolvedValue(
      served([
        {
          id: "t-1",
          shortId: 7,
          groupId: "g-1",
          groupTitle: "Жанр",
          title: "Детектив",
          sortOrder: 0,
          gamesCount: 3,
        },
        {
          id: "t-2",
          shortId: 8,
          groupId: "g-1",
          groupTitle: "Жанр",
          title: "Нуар",
          sortOrder: 1,
          gamesCount: 0,
        },
      ]),
    );
    const wrapper = await render();

    const groupTrash = wrapper.findAll('button[aria-label="Удалить группу"]');
    const tagTrash = wrapper.findAll('button[aria-label="Удалить тег"]');
    // One trash each: the empty group "Пустая" and the tag "Нуар" no game uses.
    expect(groupTrash).toHaveLength(1);
    expect(
      groupTrash[0].element.closest(".group-item-wrapper")?.textContent,
    ).toContain("Пустая");
    expect(tagTrash).toHaveLength(1);
    expect(tagTrash[0].element.closest(".tag-item")?.textContent).toContain(
      "Нуар",
    );
    // Nothing is drawn out of reach instead, and every row keeps its pencil.
    expect(wrapper.findAll("button[disabled]")).toHaveLength(0);
    expect(
      wrapper.findAll('button[aria-label="Редактировать группу"]'),
    ).toHaveLength(2);
    expect(
      wrapper.findAll('button[aria-label="Редактировать тег"]'),
    ).toHaveLength(2);
  });

  it("deletes an empty group with the window's flags and reads the catalogue again", async () => {
    deleteTagGroup.mockResolvedValue({ data: null, error: null });
    const wrapper = await render();

    await confirm(wrapper, "Удалить группу");

    expect(deleteTagGroup).toHaveBeenCalledWith("g-2", OWNS_REFUSAL);
    expect(getTagGroups).toHaveBeenCalledTimes(2);
    // The public catalogue the game form picks from is asked past its copy.
    expect(publicTags).toHaveBeenCalledWith(true);
    expect(windows().find(".dialog-container").exists()).toBe(false);
  });

  // Shown rows stay: a read again after a write does not blank the section.
  it("keeps the panels standing under aria-busy while it reads them again", async () => {
    deleteTagGroup.mockResolvedValue({ data: null, error: null });
    const wrapper = await render();
    let answer: (value: unknown) => void = () => {};
    getTagGroups.mockReturnValueOnce(
      new Promise((resolve) => (answer = resolve)),
    );

    await confirm(wrapper, "Удалить группу");

    const section = wrapper.get(".moderation-tags");
    expect(section.attributes("aria-busy")).toBe("true");
    expect(wrapper.find(".groups-panel").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("Загрузка...");

    answer(served([{ id: "g-1", title: "Жанр", sortOrder: 0, tagsCount: 1 }]));
    await flushPromises();
    expect(section.attributes("aria-busy")).toBeUndefined();
  });

  it.each([
    ["Удалить группу", deleteTagGroup, "Не удалось удалить группу тегов"],
    ["Удалить тег", deleteTag, "Не удалось удалить тег"],
  ])(
    "prints a refused %s in the window and stays open",
    async (label, request, sentence) => {
      request.mockResolvedValue(refused({ type: "Conflict", status: 409 }));
      const wrapper = await render();

      await confirm(wrapper, label);

      expect(windows().get(".dialog-refusal").text()).toBe(sentence);
      expect(windows().find(".dialog-container").exists()).toBe(true);
      expect(plates()).toEqual([]);
      expect(getTagGroups).toHaveBeenCalledTimes(1);
    },
  );
});

// The row of every tag stands in the frame of a group row, so the counts stand
// in one column; a group that holds tags has its pencil and no trash.
describe("the list of groups", () => {
  beforeEach(() => {
    getTagGroups.mockResolvedValue(
      served([{ id: "g-1", title: "Жанр", sortOrder: 0, tagsCount: 1 }]),
    );
    getTags.mockResolvedValue(served([]));
  });

  it("draws the row of every tag in the frame of a group row", async () => {
    const wrapper = await render();

    const rows = wrapper.findAll(".groups-list .group-item");
    expect(rows.map((row) => row.get(".group-title").text())).toEqual([
      "Все теги",
      "Жанр",
    ]);
    expect(
      rows.map((row) => row.element.parentElement?.classList[0] ?? null),
    ).toEqual(["group-item-wrapper", "group-item-wrapper"]);

    // The group holds a tag: its pencil is drawn, its trash is not.
    expect(
      wrapper.findAll(".group-item-wrapper .group-actions .icon-btn"),
    ).toHaveLength(1);
  });
});

// The list carries the description rendered, as the tooltip of the tag shows it;
// the edit window reads the tag again and opens on the description as typed.
describe("the description of a tag", () => {
  const TAG = {
    id: "t-1",
    shortId: 7,
    groupId: "g-1",
    groupTitle: "Жанр",
    title: "Детектив",
    sortOrder: 0,
    gamesCount: 1,
  };
  const RENDERED =
    '<span class="bb-tip" data-tip-text="Расследование">Сыщик</span> ищет';
  const TYPED = '[tip="Расследование"]Сыщик[/tip] ищет';

  beforeEach(() => {
    getTagForEdit.mockReset();
    getTagGroups.mockResolvedValue(
      served([{ id: "g-1", title: "Жанр", sortOrder: 0, tagsCount: 1 }]),
    );
    getTags.mockResolvedValue(served([{ ...TAG, description: RENDERED }]));
  });

  it("is drawn on the card as the server rendered it", async () => {
    const wrapper = await render();

    expect(wrapper.get(".tag-description .bb-tip").text()).toBe("Сыщик");
    expect(wrapper.get(".tag-description").text()).not.toContain("[tip");
  });

  it("opens the edit window as typed", async () => {
    getTagForEdit.mockResolvedValue({
      data: { ...TAG, description: TYPED },
      error: null,
    });
    const wrapper = await render();

    await wrapper
      .get('button[aria-label="Редактировать тег"]')
      .trigger("click");
    await flushPromises();

    expect(getTagForEdit).toHaveBeenCalledWith("t-1");
    expect(
      (windows().get("#tag-description").element as HTMLTextAreaElement).value,
    ).toBe(TYPED);
  });

  it("opens no window when the tag does not come, and says so in its row", async () => {
    getTagForEdit.mockResolvedValue(refused({ status: 404, title: "" }));
    const wrapper = await render();

    await wrapper
      .get('button[aria-label="Редактировать тег"]')
      .trigger("click");
    await flushPromises();

    expect(windows().find("#tag-description").exists()).toBe(false);
    expect(wrapper.get(".tag-item .action-result").text()).toBe(
      "Не удалось загрузить тег",
    );
    expect(plates()).toEqual([]);
  });
});
