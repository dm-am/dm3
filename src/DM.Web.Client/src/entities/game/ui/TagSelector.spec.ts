/**
 * @vitest-environment jsdom
 */

// The copy of a failure is Russian, and the server's raw title does not replace it.

import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  compileSass,
  componentCss,
  ruleMap,
  styleRules,
} from "@/shared/lib/testing/styles";
import { PALETTES } from "@/shared/lib/testing/themeColours";
import TagSelector from "./TagSelector.vue";
import type { Tag } from "../model/gameTypes";
import gameApi from "../api/gameApi";
import { useGamesStore } from "../model/store";

const HERE = dirname(fileURLToPath(import.meta.url));

const FAILURE_MESSAGE = "Не удалось загрузить теги";

/** A tag of the catalogue, its sort order its id. */
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

// The catalogue lives in the games store: every case starts with none cached.
beforeEach(() => {
  setActivePinia(createPinia());
});

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

    expect(wrapper.find(".error-state .error-message").text()).toBe(
      FAILURE_MESSAGE,
    );
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

    expect(wrapper.find(".error-state .error-message").text()).toBe(
      FAILURE_MESSAGE,
    );
  });
});

// Until the catalogue answers the form holds its place, and says nothing it does not know.
describe("TagSelector before the catalogue answers", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  const CATALOGUE = {
    data: {
      resources: [tag(3, "D&D 5e", "Система", 0, null)],
      paging: null,
    },
    error: null,
  };

  it("stands the skeleton of the groups, with no words, while the tags load", async () => {
    vi.spyOn(gameApi, "getTags").mockReturnValue(new Promise(() => {}));

    const wrapper = mount(TagSelector);
    await flushPromises();

    expect(wrapper.find(".tags-skeleton").exists()).toBe(true);
    expect(wrapper.text()).toBe("");
  });

  it("says there are no tags only once an empty catalogue has answered", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });

    const wrapper = mount(TagSelector);
    await flushPromises();

    expect(wrapper.find(".tags-skeleton").exists()).toBe(false);
    expect(wrapper.text()).toBe("Нет доступных тегов");
  });

  it("asks the origin again from the refusal's button and draws the answer", async () => {
    const getTags = vi
      .spyOn(gameApi, "getTags")
      .mockResolvedValueOnce({
        data: null,
        error: { type: "", title: "", status: 500, traceId: "" },
      })
      .mockResolvedValueOnce(CATALOGUE);

    const wrapper = mount(TagSelector);
    await flushPromises();
    await wrapper.get(".error-state .error-retry").trigger("click");
    await flushPromises();

    expect(getTags).toHaveBeenLastCalledWith(true);
    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.get("button.tag-button").text()).toBe("D&D 5e");
  });

  it("opens on the catalogue the store already holds, with no skeleton", async () => {
    const getTags = vi.spyOn(gameApi, "getTags").mockResolvedValue(CATALOGUE);
    await useGamesStore().fetchTags();

    const wrapper = mount(TagSelector);

    expect(wrapper.find(".tags-skeleton").exists()).toBe(false);
    expect(wrapper.get("button.tag-button").text()).toBe("D&D 5e");
    expect(getTags).toHaveBeenCalledTimes(1);
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
    // The catalog decides which groups exist and in what order.
    const withNewGroup = [
      ...catalogue,
      tag(90, "Шок-контент", "Деликатный контент", 7, null),
    ];
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: withNewGroup, paging: null },
      error: null,
    });

    const wrapper = mount(TagSelector, { props: { modelValue: [] } });
    await flushPromises();

    expect(wrapper.text()).toContain("Деликатный контент");
    expect(wrapper.text()).toContain("Шок-контент");
  });

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

type Rgb = [number, number, number];

/** The value of the first `var(--x)` a declaration names. */
function token(value: string | undefined, tokens: Record<string, string>) {
  const name = /var\(--([\w-]+)\)/.exec(value ?? "")?.[1];
  const found = name ? tokens[name] : undefined;
  if (!found) throw new Error(`no palette token in "${value}"`);
  return found;
}

/** A solid fill, `var(--x)` down to its channels. */
function solid(value: string | undefined, tokens: Record<string, string>): Rgb {
  let hex = token(value, tokens);
  if (/^#[0-9a-f]{3}$/i.test(hex)) {
    hex = hex.replace(/[0-9a-f]/gi, (digit) => digit + digit);
  }
  if (!/^#[0-9a-f]{6}$/i.test(hex)) throw new Error(`${hex} is not solid`);
  return [1, 3, 5].map((at) => parseInt(hex.slice(at, at + 2), 16)) as Rgb;
}

/** `base` under the translucent gradient of a background-image, if any. */
function wash(
  base: Rgb,
  image: string | undefined,
  tokens: Record<string, string>,
): Rgb {
  if (image === undefined) return base;
  const overlay = /^rgba\(\s*(\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\s*\)$/.exec(
    token(image, tokens),
  );
  if (!overlay) throw new Error(`no translucent overlay in "${image}"`);
  const alpha = Number(overlay[4]);
  return base.map(
    (channel, at) => channel * (1 - alpha) + Number(overlay[at + 1]) * alpha,
  ) as Rgb;
}

/** One number for a grey of the palette: the mean of its channels. */
const level = ([r, g, b]: Rgb) => (r + g + b) / 3;

// .tag-button is the box of a filter bubble (+bubble-box); its hover and its chosen fill copy the bubble and the
// active segment, and these hold the copy.
describe("TagSelector look", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  const bubbleCss = compileSass(
    '@use "@/assets/styles/FilterBubbles" as *\n.probe\n  +filter-bubbles',
  );
  const bubbles = styleRules(bubbleCss);
  const sheet = ruleMap(componentCss(resolve(HERE, "TagSelector.vue")));
  const tagRule = sheet.get(".tag-button");

  it("answers the pointer the way a bubble does, with the frame left alone", () => {
    const bubbleHover = bubbles.find((rule) =>
      rule.selector.includes(
        ".probe .bubble:not(.bubble-owners):not(.bubble-expandable):hover",
      ),
    )?.declarations;
    const tagHover = sheet.get(
      ".tag-button:hover:not(:disabled):not(.selected)",
    );

    expect(bubbleHover?.["background-image"]).toBeDefined();
    expect(tagHover?.["background-image"]).toBe(
      bubbleHover?.["background-image"],
    );
    expect(tagHover?.["border-color"]).toBeUndefined();
  });

  it("draws the chosen tag pressed, as the active segment, and not bold", () => {
    const chosen = sheet.get(".tag-button.selected");
    const segment = ruleMap(
      componentCss(
        resolve(
          HERE,
          "../../../shared/ui/SegmentedControl/SegmentedControl.vue",
        ),
      ),
    ).get(".segment.active");

    expect(segment?.["background-color"]).toBeDefined();
    expect(chosen?.["background-color"]).toBe(segment?.["background-color"]);
    expect(chosen?.["font-weight"]).toBeUndefined();
    expect(tagRule?.["font-weight"]).toBeUndefined();
  });

  // Ten points is the least step the palette gives a control fill over its ground.
  it("keeps the chosen fill out of the hover's reach, in both palettes", () => {
    const hover = sheet.get(".tag-button:hover:not(:disabled):not(.selected)");
    const chosen = sheet.get(".tag-button.selected");
    const hoverRules = [...sheet.keys()].filter((s) => s.includes(":hover"));
    expect(hoverRules).toEqual([
      ".tag-button:hover:not(:disabled):not(.selected)",
    ]);

    for (const [theme, tokens] of Object.entries(PALETTES)) {
      const rest = solid(tagRule?.["background-color"], tokens);
      const underPointer = wash(rest, hover?.["background-image"], tokens);
      const pressed = wash(
        solid(chosen?.["background-color"], tokens),
        chosen?.["background-image"],
        tokens,
      );

      expect(
        Math.abs(level(pressed) - level(underPointer)),
        theme,
      ).toBeGreaterThanOrEqual(10);
      expect(Math.abs(level(pressed) - level(rest)), theme).toBeGreaterThan(
        Math.abs(level(underPointer) - level(rest)),
      );
    }
  });

  it("says which tags are pressed with aria-pressed", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: {
        resources: [
          tag(3, "D&D 5e", "Система", 0, null),
          tag(4, "GURPS", "Система", 0, null),
        ],
        paging: null,
      },
      error: null,
    });

    const wrapper = mount(TagSelector, { props: { modelValue: [3] } });
    await flushPromises();

    expect(
      wrapper
        .findAll("button.tag-button")
        .map((b) => [b.text(), b.attributes("aria-pressed")]),
    ).toEqual([
      ["D&D 5e", "true"],
      ["GURPS", "false"],
    ]);
  });

  it("puts no cross on a chosen tag", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: {
        resources: [tag(3, "D&D 5e", "Система", 0, null)],
        paging: null,
      },
      error: null,
    });

    const wrapper = mount(TagSelector, { props: { modelValue: [3] } });
    await flushPromises();

    const chosen = wrapper.get("button.tag-button.selected");
    expect(chosen.text()).toBe("D&D 5e");
    expect(chosen.find("button, svg, .bubble-remove-btn").exists()).toBe(false);
  });
});
