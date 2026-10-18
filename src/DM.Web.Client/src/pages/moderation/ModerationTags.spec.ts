/**
 * @vitest-environment jsdom
 */

/**
 * The tag section answers a failed load itself, so this is about who speaks.
 *
 * Two reads fill the section - the groups and the tags - and neither leaves
 * anything on screen when it fails: the panels are drawn from what they
 * returned, so the page puts a line where both panels would be. That line is
 * the one carrier of the failure, and the response interceptor has to be told,
 * or the same sentence stands twice: once in the middle of the section, once on
 * a plate in the corner. The interceptor lives below the mock this suite
 * installs, so the telling is checked on the request, where the decision is.
 *
 * A wait is checked separately from the rest because it is the one refusal the
 * page cannot relay as the server worded it: the title of a 429 names no
 * number, and the seconds arrive beside it.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import ModerationTags from "./ModerationTags.vue";
import SOURCE from "./ModerationTags.vue?raw";

const getTagGroups = vi.hoisted(() => vi.fn());
const getTags = vi.hoisted(() => vi.fn());
const fetchTags = vi.hoisted(() => vi.fn());
const deleteTagGroup = vi.hoisted(() => vi.fn());
const deleteTag = vi.hoisted(() => vi.fn());

vi.mock("@/entities/game", () => ({
  gameTagApi: {
    getTagGroups,
    getTags,
    deleteTagGroup,
    deleteTag,
  },
  useGamesStore: () => ({ fetchTags }),
}));

/** The two write dialogs have their own specs; nothing here opens one. */
vi.mock("./dialogs/TagGroupDialog.vue", () => ({
  default: { template: "<div />" },
}));
vi.mock("./dialogs/TagDialog.vue", () => ({
  default: { template: "<div />" },
}));

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

/** The gate has its own spec; here the viewer is a moderator. */
const gate = vi.hoisted(() => ({ hasAccess: true }));

vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return {
    useRoleGate: () => ({ hasAccess: ref(gate.hasAccess), deniedText: "" }),
  };
});

const toastError = vi.hoisted(() => vi.fn());

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ error: toastError, success: vi.fn() }),
}));

/** What the page says it will answer for, on both reads. */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true };

const served = (resources: unknown[]) => ({
  data: { resources, paging: null },
  error: null,
});

const refused = (error: Record<string, unknown>) => ({ data: null, error });

enableAutoUnmount(afterEach);

function render() {
  return mount(ModerationTags, {
    global: {
      stubs: {
        SecondaryText: true,
        SvgIcon: true,
        EmptyState: true,
        // The confirmations of a delete are the real window, drawn in place.
        Teleport: true,
        // Passthrough: the controls it wraps are part of the panel it hides
        // when a stub swallows its slot.
        Tooltip: { template: "<div><slot /></div>" },
      },
    },
  });
}

describe("the tag section", () => {
  beforeEach(() => {
    getTagGroups.mockReset();
    getTags.mockReset();
    fetchTags.mockReset();
    toastError.mockReset();
    gate.hasAccess = true;
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
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".error").exists()).toBe(false);
    expect(wrapper.text()).toContain("Жанр");
    expect(wrapper.text()).toContain("Детектив");
  });

  /**
   * Asserted on the requests rather than on the plate: the interceptor that
   * would have raised it sits below the mock, and the flag on the way out is
   * the only thing that can reach it.
   */
  it("takes the refusal of both reads over from the interceptor", async () => {
    render();
    await flushPromises();

    expect(getTagGroups).toHaveBeenCalledWith(OWNS_REFUSAL);
    expect(getTags).toHaveBeenCalledWith(OWNS_REFUSAL);
  });

  it("puts the reason the server named where the panels would be", async () => {
    getTagGroups.mockResolvedValue(
      refused({ type: "Forbidden", status: 403, title: "Раздел закрыт" }),
    );

    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".error").text()).toBe("Раздел закрыт");
    // Nothing of the section is left standing beside the line.
    expect(wrapper.find(".groups-panel").exists()).toBe(false);
    expect(wrapper.find(".tags-panel").exists()).toBe(false);
  });

  /** The failure of the second read is a failure of the section too. */
  it("answers for a refusal of the tags read as well", async () => {
    getTags.mockResolvedValue(
      refused({ type: "ServerError", status: 500, title: "Внутренняя ошибка" }),
    );

    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".error").text()).toBe("Внутренняя ошибка");
  });

  /**
   * The wait comes over with the refusal, so the line has to say what the plate
   * would have: the seconds the server asked for. The title sent with a 429
   * carries no number, so relaying it would drop the one part a reader acts on.
   */
  it("names the wait when a read is rate limited", async () => {
    getTagGroups.mockResolvedValue(
      refused({
        type: "TooManyRequests",
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      }),
    );

    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".error").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });
});

/**
 * The two deletes. Each confirmation runs its request itself: it waits for the
 * answer, prints a refusal in itself and closes on success alone, and the
 * request carries the flags that keep the interceptor quiet about it, the way
 * the reads do.
 */
describe("the tag section deletes", () => {
  beforeEach(() => {
    getTagGroups.mockReset();
    getTags.mockReset();
    fetchTags.mockReset();
    deleteTagGroup.mockReset();
    deleteTag.mockReset();
    toastError.mockReset();
    gate.hasAccess = true;
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

  const confirm = async (wrapper: ReturnType<typeof render>, label: string) => {
    await wrapper.get(`button[aria-label="${label}"]`).trigger("click");
    await wrapper.get(".dialog-btn-submit").trigger("click");
    await flushPromises();
  };

  /**
   * The server refuses to delete a group that holds tags and a tag that
   * stands in a game. Their trash used to be drawn pale, and pressing it did
   * nothing: the refusal drawn in advance. Now there is no trash to press, and
   * the count in the row says why.
   */
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
    const wrapper = render();
    await flushPromises();

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
    const wrapper = render();
    await flushPromises();

    await confirm(wrapper, "Удалить группу");

    expect(deleteTagGroup).toHaveBeenCalledWith("g-2", OWNS_REFUSAL);
    expect(getTagGroups).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it.each([
    ["Удалить группу", deleteTagGroup, "Не удалось удалить группу тегов"],
    ["Удалить тег", deleteTag, "Не удалось удалить тег"],
  ])(
    "prints a refused %s in the window and stays open",
    async (label, request, sentence) => {
      request.mockResolvedValue(refused({ type: "Conflict", status: 409 }));
      const wrapper = render();
      await flushPromises();

      await confirm(wrapper, label);

      expect(wrapper.get(".dialog-refusal").text()).toBe(sentence);
      expect(wrapper.find(".dialog-container").exists()).toBe(true);
      expect(toastError).not.toHaveBeenCalled();
      expect(getTagGroups).toHaveBeenCalledTimes(1);
    },
  );
});

/**
 * The counts of the list of groups stand in one column. The row of every tag
 * stood outside the frame a group row has, and a button there is as wide as
 * its words: its count stood against the title ("Все теги72"), where the rows
 * under it hold theirs apart at the right, before their buttons. Nothing is
 * laid out here, so the row is checked for the frame and for the room it keeps
 * for the buttons it has not got, read from the rules where they are written.
 *
 * A group that holds tags has no trash, and its buttons keep the room of two
 * all the same: the actions are as wide as two buttons whatever is drawn in
 * them, and the row of every tag leaves that room and the gap before it.
 */
describe("the list of groups", () => {
  beforeEach(() => {
    gate.hasAccess = true;
    getTagGroups.mockResolvedValue(
      served([{ id: "g-1", title: "Жанр", sortOrder: 0, tagsCount: 1 }]),
    );
    getTags.mockResolvedValue(served([]));
  });

  it("draws the row of every tag in the frame of a group row", async () => {
    const wrapper = render();
    await flushPromises();

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
      wrapper.findAll(".group-item-wrapper .group-actions .btn-icon"),
    ).toHaveLength(1);

    // Two buttons and the gap between them, kept by the actions of a group
    // whatever is drawn in them, and left by the row of every tag with the
    // gap before them.
    expect(rows[0].classes()).toContain("group-item--all");
    expect(SOURCE).toMatch(
      /\n\$two-icon-buttons: calc\(2 \* #\{\$icon-button\} \+ #\{\$tiny\}\)\n/,
    );
    expect(SOURCE).toMatch(
      /\n\.group-actions\n(\s+[^\n]+\n)*?\s+width: \$two-icon-buttons\n/,
    );
    expect(SOURCE).toMatch(
      /\n\.group-item--all\n\s+margin-right: calc\(#\{\$two-icon-buttons\} \+ #\{\$tiny\}\)/,
    );
  });
});
