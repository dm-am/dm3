/**
 * @vitest-environment jsdom
 */

/**
 * The rolls of a new post, set up in the old site's line: the count, "x", a
 * button for each die of the old site, "D" with a field of edges for any
 * other die, D100, "+" and the modifier, then "Комментарий" and its field. A
 * die is thrown by its own button with what the line holds, the comment, the
 * count and the modifier go back to empty, 1 and 0 after the throw, and the
 * two boxes over the line hold for every roll. The rolls that go out are
 * listed in the words of the published post, each with its cross.
 *
 * What is sent is what the server reads (CreatePostDiceRoll): a die without
 * the box of explosions explodes 0 times, with the box and an empty field it
 * explodes without a limit, which the server writes as its own cap of 100.
 * A count or a number of edges the server would refuse adds nothing, and the
 * line under the dice says why: a press that went through and a post the
 * server then refused whole would be worse.
 */
import { describe, it, expect, afterEach } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import type { DiceRollInput } from "@/entities/game";
import DiceComposer from "./DiceComposer.vue";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

let wrapper: VueWrapper | null = null;

afterEach(() => {
  wrapper?.unmount();
  wrapper = null;
});

/** The composer with its list held the way the page holds it, by v-model. */
function mountComposer(rolls: DiceRollInput[] = []) {
  const composer = mount(DiceComposer, {
    attachTo: document.body,
    props: {
      modelValue: rolls,
      "onUpdate:modelValue": (next: DiceRollInput[]) =>
        composer.setProps({ modelValue: next }),
    },
  });
  wrapper = composer;
  return composer;
}

const rollsOf = (composer: VueWrapper) =>
  (composer.props() as { modelValue: DiceRollInput[] }).modelValue;

/** A control by the name it is read out by. */
const named = (composer: VueWrapper, name: string) =>
  composer.get(`[aria-label='${name}']`);

const value = (composer: VueWrapper, name: string) =>
  (named(composer, name).element as HTMLInputElement).value;

const comment = (composer: VueWrapper) => composer.get(".dice-comment");

/** A line as a reader sees it: the joins that hold it together taken out. */
const seen = (text: string) =>
  text.split(String.fromCharCode(0x2060)).join("").replace(/\s+/g, " ").trim();

describe("DiceComposer", () => {
  it("lays the line out as the old site does, the comment after the dice", () => {
    const composer = mountComposer();

    const line = composer.get(".dice-line");
    // Every control of the line in its order: the fields by what they hold,
    // the dice by their figures.
    const parts = line
      .findAll("input, button, .dice-sign, .dice-comment-caption")
      .map((one) =>
        one.element.tagName === "INPUT"
          ? `[${one.attributes("aria-label") ?? "Комментарий"}]`
          : one.text(),
      );
    expect(parts).toEqual([
      "[Сколько кубиков]",
      "x",
      "D3",
      "D4",
      "D6",
      "D8",
      "D10",
      "D12",
      "D20",
      "D",
      "[Число граней]",
      "D100",
      "+",
      "[Модификатор]",
      "Комментарий",
      "[Комментарий]",
    ]);
    expect(value(composer, "Сколько кубиков")).toBe("1");
    expect(value(composer, "Модификатор")).toBe("0");
  });

  it("puts the two boxes of the old site over the line, in its words", () => {
    const composer = mountComposer();

    expect(
      composer
        .findAll(".dice-options .dice-check")
        .map((one) => seen(one.text())),
    ).toEqual([
      "Показывать всем результаты бросков",
      "Броски взрываются максимум раз",
    ]);
    // Shown to everybody until the box is cleared, as on the old site.
    const [shown] = composer.findAll(".dice-check input[type='checkbox']");
    expect((shown.element as HTMLInputElement).checked).toBe(true);
  });

  it("throws a die by its button, with the count, the modifier and the comment of the line", async () => {
    const composer = mountComposer();

    await named(composer, "Сколько кубиков").setValue("2");
    await named(composer, "Модификатор").setValue("-3");
    await comment(composer).setValue("  Урон  ");
    await named(composer, "Бросить D6").trigger("click");

    expect(rollsOf(composer)).toEqual([
      {
        dice: 6,
        count: 2,
        bonus: -3,
        explosion: 0,
        public: true,
        comment: "Урон",
      },
    ]);
  });

  it("throws the D3 of the old site's line", async () => {
    const composer = mountComposer();

    await named(composer, "Бросить D3").trigger("click");

    expect(rollsOf(composer)).toEqual([
      expect.objectContaining({ dice: 3, count: 1, bonus: 0 }),
    ]);
  });

  it("puts the comment, the count and the modifier back after the throw, and keeps the boxes", async () => {
    const composer = mountComposer();

    await composer.get(".dice-check input[type='checkbox']").setValue(false);
    await named(composer, "Сколько кубиков").setValue("3");
    await named(composer, "Модификатор").setValue("5");
    await comment(composer).setValue("Восприятие");
    await named(composer, "Бросить D20").trigger("click");

    expect((comment(composer).element as HTMLInputElement).value).toBe("");
    expect(value(composer, "Сколько кубиков")).toBe("1");
    expect(value(composer, "Модификатор")).toBe("0");

    await named(composer, "Бросить D4").trigger("click");
    expect(rollsOf(composer)[1]).toEqual({
      dice: 4,
      count: 1,
      bonus: 0,
      explosion: 0,
      public: false,
      comment: undefined,
    });
  });

  it("explodes without a limit when the box is ticked and the field left empty", async () => {
    const composer = mountComposer();

    const [, explodes] = composer.findAll(".dice-check input[type='checkbox']");
    await explodes.setValue(true);
    await named(composer, "Бросить D10").trigger("click");

    expect(rollsOf(composer)[0].explosion).toBe(100);
  });

  it("explodes as many times as written, and a number written ticks the box", async () => {
    const composer = mountComposer();

    await named(composer, "Сколько раз взрывается кубик").setValue("3");
    await named(composer, "Бросить D8").trigger("click");

    const [, explodes] = composer.findAll(".dice-check input[type='checkbox']");
    expect((explodes.element as HTMLInputElement).checked).toBe(true);
    expect(rollsOf(composer)[0].explosion).toBe(3);
  });

  it("does not explode without the box, whatever the field says", async () => {
    const composer = mountComposer();

    await named(composer, "Сколько раз взрывается кубик").setValue("3");
    const [, explodes] = composer.findAll(".dice-check input[type='checkbox']");
    await explodes.setValue(false);
    await named(composer, "Бросить D12").trigger("click");

    expect(rollsOf(composer)[0].explosion).toBe(0);
  });

  it("throws a die of the edges written beside D, by D and by Enter in the field, and keeps the edges", async () => {
    const composer = mountComposer();

    await named(composer, "Число граней").setValue("54");
    await named(composer, "Бросить кубик с числом граней из поля").trigger(
      "click",
    );
    await named(composer, "Число граней").trigger("keydown", { key: "Enter" });

    expect(rollsOf(composer)).toEqual([
      expect.objectContaining({ dice: 54, count: 1, bonus: 0 }),
      expect.objectContaining({ dice: 54, count: 1, bonus: 0 }),
    ]);
    expect(value(composer, "Число граней")).toBe("54");
  });

  it("adds nothing for edges the server refuses, and says so under the line", async () => {
    const composer = mountComposer();

    await named(composer, "Бросить кубик с числом граней из поля").trigger(
      "click",
    );
    expect(composer.get(".action-result").text()).toBe("Граней от 2 до 1000");

    await named(composer, "Число граней").setValue("1");
    await named(composer, "Бросить кубик с числом граней из поля").trigger(
      "click",
    );

    expect(rollsOf(composer)).toEqual([]);
    expect(composer.get(".action-result").text()).toBe("Граней от 2 до 1000");
  });

  it("adds nothing for a count the server refuses, and says so under the line", async () => {
    const composer = mountComposer();

    await named(composer, "Сколько кубиков").setValue("101");
    await named(composer, "Бросить D6").trigger("click");

    expect(rollsOf(composer)).toEqual([]);
    expect(composer.get(".action-result").text()).toBe("Кубиков от 1 до 100");
    expect(value(composer, "Сколько кубиков")).toBe("101");
  });

  it("lists the rolls in the words of the post, the hidden one marked, and takes one away by its cross", async () => {
    const composer = mountComposer([
      { dice: 20, count: 1, bonus: 5, public: true, comment: "Восприятие" },
      { dice: 6, count: 2, bonus: 0, public: true, comment: "Урон" },
      { dice: 100, count: 1, bonus: 0, public: false },
    ]);

    const lines = () =>
      composer.findAll(".dice-pending li").map((one) => seen(one.text()));
    expect(lines()).toEqual([
      '1) 1D20+5 - "Восприятие" ×',
      '2) 2D6 - "Урон" ×',
      "3) 1D100 (видит только мастер) ×",
    ]);

    await named(composer, "Убрать бросок 2").trigger("click");

    expect(rollsOf(composer).map((roll) => roll.dice)).toEqual([20, 100]);
    expect(lines()).toEqual([
      '1) 1D20+5 - "Восприятие" ×',
      "2) 1D100 (видит только мастер) ×",
    ]);
  });

  it("has no list while there is nothing to send", () => {
    const composer = mountComposer();

    expect(composer.find(".dice-pending").exists()).toBe(false);
  });

  it("throws nothing while the post is on its way", async () => {
    const composer = mountComposer();
    await composer.setProps({ disabled: true });

    for (const one of composer.findAll(".dice-die"))
      expect(one.attributes("disabled")).toBeDefined();
    expect(comment(composer).attributes("disabled")).toBeDefined();
    expect(
      named(composer, "Число граней").attributes("disabled"),
    ).toBeDefined();
  });
});

/**
 * The look of the line, read off the stylesheet (jsdom lays nothing out): the
 * dice are buttons of the site in the bold brown of the old site's figures,
 * the line and its parts wrap rather than run past the column, and the
 * comment takes the rest of the line, under the dice where it has no room.
 */
describe("DiceComposer line", () => {
  const rules = ruleMap(
    componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "DiceComposer.vue"),
    ),
  );

  it("draws a die as a button of the site, its figure bold in the ink of the headings", () => {
    const die = rules.get(".dice-die");
    expect(die).toMatchObject({
      height: "38px",
      "border-radius": "8px",
      color: "var(--heading)",
      "font-weight": "700",
    });
  });

  it("wraps the line and the dice rather than run past the column", () => {
    expect(rules.get(".dice-line")).toMatchObject({
      display: "flex",
      "flex-wrap": "wrap",
    });
    expect(rules.get(".dice-roll")).toMatchObject({
      display: "flex",
      "flex-wrap": "wrap",
    });
    expect(rules.get(".dice-comment-line")).toMatchObject({
      flex: "1 1 350px",
    });
  });

  // The rolls that go out are the rolls the post will show, so they are
  // listed in the look of its strip (GamePost.vue, .dice-rolls) and on its
  // ground: on another one they read as another kind of thing.
  it("lists the rolls that go out on the ground of the strip of a post", () => {
    const strip = ruleMap(
      componentCss(
        join(dirname(fileURLToPath(import.meta.url)), "GamePost.vue"),
      ),
    ).get(".dice-rolls");
    const pending = rules.get(".dice-pending");

    expect(strip?.["background-color"]).toBeTruthy();
    for (const property of [
      "background-color",
      "color",
      "padding",
      "font-size",
    ])
      expect(pending?.[property], property).toBe(strip?.[property]);
  });
});
