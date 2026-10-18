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
import { readFileSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import {
  compileSass,
  componentCss,
  ruleMap,
  styleRules,
} from "@/shared/lib/testing/styles";
import TagSelector from "./TagSelector.vue";
import type { Tag } from "../model/types";
import { gameApi } from "../api";

const HERE = dirname(fileURLToPath(import.meta.url));

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

// --- paint of a rule, from the palettes of ThemeVariables.css ---------------

type Rgb = [number, number, number];

const THEMES = readFileSync(
  resolve(HERE, "../../../assets/styles/ThemeVariables.css"),
  "utf8",
).replace(/\/\*[\s\S]*?\*\//g, "");

function palette(header: string): Record<string, string> {
  const at = THEMES.indexOf(header);
  if (at < 0) throw new Error(`ThemeVariables.css has no "${header}" block`);
  const body = THEMES.slice(at + header.length);
  const tokens: Record<string, string> = {};
  for (const [, name, value] of body
    .slice(0, body.indexOf("}"))
    .matchAll(/^\s*--([\w-]+):\s*([^;]+);/gm)) {
    tokens[name] = value.trim();
  }
  return tokens;
}

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

/**
 * A tag of the creation form wears the bubble of an applied filter.
 *
 * The same tags stand as bubbles under the filter bar of /games ("Тег: D&D
 * 5e"), and the form is to draw them the same way, not as a grey chip a
 * shade deeper than its ground whose frame darkens under the pointer. The
 * filter is the reference here and the tag is what
 * changes: the bubble's fill, frame, corners and size, and its hover, which
 * lays the site's wash over the fill and leaves the frame alone. A chosen tag
 * is a pressed button and not a bold word: the fill of the active segment of a
 * SegmentedControl under the press wash, a shade the hover never reaches, and
 * aria-pressed says it to a screen reader. It carries no cross: it is taken
 * off by a second press, not by a mark of its own.
 */
describe("TagSelector look", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  const bubbleCss = compileSass(
    '@use "@/assets/styles/Filters" as *\n.probe\n  +filter-bubbles',
  );
  const bubbles = styleRules(bubbleCss);
  const bubble = ruleMap(bubbleCss).get(".probe .bubble");
  const sheet = ruleMap(componentCss(resolve(HERE, "TagSelector.vue")));
  const tagRule = sheet.get(".tag-button");

  it("takes the fill, frame, corners and size of a filter's bubble", () => {
    for (const property of [
      "background-color",
      "border",
      "border-radius",
      "padding",
      "font-size",
      "box-sizing",
      "min-height",
      "line-height",
    ]) {
      expect(tagRule?.[property], property).toBe(bubble?.[property]);
    }
  });

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

  // The pointer resting on a tag that is not chosen must not draw it chosen:
  // the tag just taken off stays under the pointer, and on a touch screen the
  // hover stays on it until the next touch. Measured as paint, in both
  // palettes, because the two rules name different tokens and only their
  // composition says whether they meet. Ten points is the least step the
  // palette gives a control fill over its ground (ThemeVariables.css).
  it("keeps the chosen fill out of the hover's reach, in both palettes", () => {
    const hover = sheet.get(".tag-button:hover:not(:disabled):not(.selected)");
    const chosen = sheet.get(".tag-button.selected");
    const hoverRules = [...sheet.keys()].filter((s) => s.includes(":hover"));
    expect(hoverRules).toEqual([
      ".tag-button:hover:not(:disabled):not(.selected)",
    ]);

    for (const header of ["html.theme_Light {", "html.theme_Dark {"]) {
      const tokens = palette(header);
      const rest = solid(tagRule?.["background-color"], tokens);
      const underPointer = wash(rest, hover?.["background-image"], tokens);
      const pressed = wash(
        solid(chosen?.["background-color"], tokens),
        chosen?.["background-image"],
        tokens,
      );

      expect(
        Math.abs(level(pressed) - level(underPointer)),
        header,
      ).toBeGreaterThanOrEqual(10);
      expect(Math.abs(level(pressed) - level(rest)), header).toBeGreaterThan(
        Math.abs(level(underPointer) - level(rest)),
      );
    }
  });

  // A touch screen keeps :hover on the tag last touched, so a tag just taken
  // off would stand washed until the next touch. The wash answers only a
  // pointer that hovers, and nowhere else.
  it("lays the hover wash under a pointer that hovers only", () => {
    const css = componentCss(resolve(HERE, "TagSelector.vue"));
    expect(
      styleRules(css).filter((rule) => rule.selector.includes(":hover")),
    ).toHaveLength(1);
    expect(css.replace(/\s+/g, " ")).toContain(
      "@media (hover: hover) { .tag-button:hover:not(:disabled):not(.selected) {",
    );
  });

  it("says which tags are pressed with aria-pressed", async () => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: {
        resources: [
          {
            id: 3,
            title: "D&D 5e",
            groupTitle: "Система",
            groupSortOrder: 0,
            groupMaxTagsPerGame: null,
            sortOrder: 3,
            gamesCount: 0,
          },
          {
            id: 4,
            title: "GURPS",
            groupTitle: "Система",
            groupSortOrder: 0,
            groupMaxTagsPerGame: null,
            sortOrder: 4,
            gamesCount: 0,
          },
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
        resources: [
          {
            id: 3,
            title: "D&D 5e",
            groupTitle: "Система",
            groupSortOrder: 0,
            groupMaxTagsPerGame: null,
            sortOrder: 3,
            gamesCount: 0,
          },
        ],
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
