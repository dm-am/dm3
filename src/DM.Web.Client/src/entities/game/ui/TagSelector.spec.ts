/**
 * @vitest-environment jsdom
 */

/**
 * TagSelector error copy.
 *
 * The tag list was the one place in the client that put an English sentence in
 * front of a Russian reader: `apiError.title || "Failed to load tags"`. Both
 * halves of the UI_STANDARDS rule are pinned here — the copy is Russian, and the
 * server's raw title does not replace it. The second case is the one that looked
 * harmless: the API answers a 500 with a title of its own, and a call site that
 * prefers it shows whatever the server happened to say.
 */

import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import TagSelector from "./TagSelector.vue";
import type { Tag } from "../model/types";
import { gameApi } from "../api";

const FAILURE_MESSAGE = "Не удалось загрузить теги";

describe("TagSelector", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("reports a request that never reached the API in Russian", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: null,
      error: { type: "Unknown", title: "", status: 0, traceId: "" },
    });

    const wrapper = mount(TagSelector);
    await flushPromises();

    expect(wrapper.find(".selector-error").text()).toBe(FAILURE_MESSAGE);
  });

  it("does not show the title the server sent", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Internal server error",
        status: 500,
        traceId: "",
      },
    });

    const wrapper = mount(TagSelector);
    await flushPromises();

    expect(wrapper.find(".selector-error").text()).toBe(FAILURE_MESSAGE);
  });
});

/**
 * Per-group limits.
 *
 * How many tags of a group a game may carry is a column of the group, served
 * with the catalogue, so these cases hand the picker their own numbers instead
 * of the ones the site ships with: the behaviour is "whatever the group says",
 * and moderation moves the numbers without a release.
 *
 * The two shapes are deliberately different. A group taking one tag behaves as
 * a switch: picking a second one drops the first, because refusing a click on a
 * single-choice control is a dead end the reader has to work out. Above one it
 * is a ceiling, and the tags past it go dim rather than silently ignoring the
 * click.
 */
describe("TagSelector group limits", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  const catalogue: Tag[] = [
    tag(1, "Скоростной", "Темп", 4, 1),
    tag(2, "Неторопливый", "Темп", 4, 1),
    tag(10, "D&D 5e", "Система", 0, 2),
    tag(11, "GURPS", "Система", 0, 2),
    tag(12, "Fate Core", "Система", 0, 2),
    tag(20, "Без мата", "Ограничения", 5, null),
    tag(21, "Без насилия", "Ограничения", 5, null),
    tag(22, "Для своих", "Ограничения", 5, null),
  ];

  it("draws a group the control was never told about", async () => {
    // The order used to be a list kept here, and anything outside it was
    // dropped rather than appended: two groups of eight never reached the
    // screen, one of them the group a master marks sensitive content with.
    // The catalog decides which groups exist and in what order; this control
    // only draws them.
    const withNewGroup = [
      ...catalogue,
      tag(90, "Шок-контент", "Деликатный контент", 7, null),
    ];
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: withNewGroup, paging: null },
      error: null,
    } as never);

    const wrapper = mount(TagSelector, { props: { modelValue: [] } });
    await flushPromises();

    expect(wrapper.text()).toContain("Деликатный контент");
    expect(wrapper.text()).toContain("Шок-контент");
  });

  function tag(
    id: number,
    title: string,
    groupTitle: string,
    groupSortOrder: number,
    groupMaxTagsPerGame: number | null,
  ): Tag {
    return {
      id,
      title,
      groupTitle,
      groupSortOrder,
      groupMaxTagsPerGame,
      sortOrder: id,
      gamesCount: 0,
    };
  }

  async function selector(selected: number[]) {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: catalogue, paging: null },
      error: null,
    });

    const wrapper = mount(TagSelector, {
      props: { modelValue: selected },
    });
    await flushPromises();
    return wrapper;
  }

  function button(wrapper: VueWrapper, title: string) {
    const found = wrapper
      .findAll("button.tag-button")
      .find((b) => b.text() === title);
    if (!found) throw new Error(`Тег ${title} не отрисован`);
    return found;
  }

  function lastSelection(wrapper: VueWrapper): number[] {
    const emitted = wrapper.emitted("update:modelValue");
    if (!emitted) throw new Error("Выбор не изменился");
    return emitted[emitted.length - 1][0] as number[];
  }

  it("switches the choice inside a group that takes one tag", async () => {
    const wrapper = await selector([1]);

    await button(wrapper, "Неторопливый").trigger("click");

    expect(lastSelection(wrapper)).toEqual([2]);
  });

  it("keeps a group of one clickable while it is full", async () => {
    const wrapper = await selector([1]);

    expect(
      button(wrapper, "Неторопливый").attributes("disabled"),
    ).toBeUndefined();
  });

  it("deselects the chosen tag of a group of one when it is clicked again", async () => {
    const wrapper = await selector([1]);

    await button(wrapper, "Скоростной").trigger("click");

    expect(lastSelection(wrapper)).toEqual([]);
  });

  it("dims the rest of a group that has reached a limit above one", async () => {
    const wrapper = await selector([10, 11]);

    const blocked = button(wrapper, "Fate Core");
    expect(blocked.attributes("disabled")).toBeDefined();
    expect(blocked.classes()).toContain("blocked");
  });

  it("leaves the chosen tags of a full group clickable so the choice can be undone", async () => {
    const wrapper = await selector([10, 11]);

    await button(wrapper, "D&D 5e").trigger("click");

    expect(lastSelection(wrapper)).toEqual([11]);
  });

  it("says why a dimmed tag cannot be taken", async () => {
    vi.useFakeTimers();
    try {
      const wrapper = await selector([10, 11]);
      const trigger = button(wrapper, "Fate Core").element.closest(
        ".tooltip-trigger",
      );
      if (!trigger) throw new Error("Тег не обернут подсказкой");

      trigger.dispatchEvent(new MouseEvent("mouseenter"));
      // The tooltip opens after a hover delay, not on the event itself.
      await vi.runOnlyPendingTimersAsync();
      await wrapper.vm.$nextTick();

      expect(document.body.textContent).toContain(
        'Из группы "Система" можно выбрать не больше 2 тегов',
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it("takes no tag when a dimmed one is clicked", async () => {
    const wrapper = await selector([10, 11]);

    await button(wrapper, "Fate Core").trigger("click");

    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
  });

  it("keeps a group below its limit open", async () => {
    const wrapper = await selector([10]);

    await button(wrapper, "GURPS").trigger("click");

    expect(lastSelection(wrapper)).toEqual([10, 11]);
  });

  it("never dims a group that sets no limit", async () => {
    const wrapper = await selector([20, 21, 22]);

    for (const title of ["Без мата", "Без насилия", "Для своих"]) {
      expect(button(wrapper, title).attributes("disabled")).toBeUndefined();
    }
  });
});
