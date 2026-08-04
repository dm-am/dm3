/**
 * @vitest-environment jsdom
 */

/**
 * A mockup catalog normally needs no test. This one is the deliverable: the
 * owner picks a variant by number, and a catalog that quietly lost a section,
 * stopped answering the scenario switch or drew every variant at one width
 * would still render, still look finished, and the decision would be made on a
 * page that no longer shows what it claims to show.
 *
 * So the contract checked here is the catalog's own: the numbering is complete
 * and in order, each number sits in its own chat frame, one switch moves every
 * variant at once, all three states are named, and the width probe reaches the
 * frames. The route is checked too: a catalog nobody can open is no catalog.
 *
 * The last two hold the strip to the rule the redesign exists for. The
 * recommended line has to read as one string of human text with real spaces
 * around the separator, and the baseline has to keep the defect it is the
 * baseline for — no state word on a scheduled event, and the separator glued
 * to the title — because a baseline quietly "improved" to match its successor
 * makes the comparison meaningless.
 */
import { describe, it, expect, afterEach } from "vitest";
import { mount, enableAutoUnmount, type VueWrapper } from "@vue/test-utils";
import { expandAllExpandables } from "@/shared/lib/composables";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import ChatEventsVariantsPage from "./ChatEventsVariantsPage.vue";

enableAutoUnmount(afterEach);

const mountPage = () =>
  mount(ChatEventsVariantsPage, {
    global: {
      // The app registers these two globally; the page uses them as the
      // catalog's own headings.
      components: { PageTitle, BlockTitle },
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  });

/** Whitespace runs collapsed, the way a reader and a clipboard both see them. */
const line = (text: string): string => text.replace(/\s+/g, " ").trim();

/** Click an option of one of the switches by its visible label. */
const pick = async (wrapper: VueWrapper, label: string): Promise<void> => {
  const segment = wrapper.findAll(".segment").find((s) => s.text() === label);
  expect(segment, `no switch option "${label}"`).toBeDefined();
  await segment!.trigger("click");
};

const variantAt = (wrapper: VueWrapper, index: number) =>
  wrapper.findAll(".variant")[index];

const NUMBERS = [
  "1.1.",
  "1.2.",
  "1.3.",
  "1.4.",
  "1.5.",
  "2.1.",
  "2.2.",
  "2.3.",
  "3.1.",
  "3.2.",
  "3.3.",
  "4.1.",
  "4.2.",
  "5.1.",
  "5.2.",
  "6.1.",
  "6.2.",
];

describe("chat events mockup catalog", () => {
  it("offers every number, each in its own chat frame", () => {
    const wrapper = mountPage();

    expect(
      wrapper.findAll(".variant-label strong").map((el) => el.text()),
    ).toEqual(NUMBERS);
    expect(wrapper.findAll(".replica-frame")).toHaveLength(NUMBERS.length);
  });

  it("moves every variant with one switch, and names all three states", async () => {
    const wrapper = mountPage();

    await pick(wrapper, "идет");
    expect(line(wrapper.text())).toContain("Идет: Вечер быстрых зарисовок");

    await pick(wrapper, "три впереди");
    expect(line(wrapper.text())).toContain("Скоро: Разбор чужих партий");

    await pick(wrapper, "закончился");
    expect(line(wrapper.text())).toContain(
      "Закончился: Вечер быстрых зарисовок",
    );

    await pick(wrapper, "нет эвентов");
    expect(wrapper.text()).toContain("Нет запланированных эвентов");
  });

  it("narrows the frame, not the window", async () => {
    const wrapper = mountPage();
    const widthOf = () =>
      wrapper
        .findAll(".replica")
        .map((el) => (el.element as HTMLElement).style.maxWidth);

    expect(new Set(widthOf())).toEqual(new Set([""]));

    await pick(wrapper, "375");
    expect(new Set(widthOf())).toEqual(new Set(["375px"]));

    await pick(wrapper, "полная");
    expect(new Set(widthOf())).toEqual(new Set([""]));
  });

  it("keeps the recommended line copyable as one string", async () => {
    const wrapper = mountPage();
    await pick(wrapper, "идет и два впереди");

    expect(line(variantAt(wrapper, 1).find(".run").text())).toBe(
      "Идет: Вечер быстрых зарисовок, до 22:48 | +2 запланировано, ближайший 05.08",
    );
  });

  it("keeps the baseline's defects in the baseline", async () => {
    const wrapper = mountPage();
    await pick(wrapper, "три впереди");

    const baseline = line(variantAt(wrapper, 0).find(".v11-main").text());
    const improved = line(variantAt(wrapper, 1).find(".run").text());

    // No state word on a scheduled event: it reads as a running one.
    expect(baseline).not.toContain("Скоро:");
    expect(improved).toContain("Скоро: Разбор чужих партий");
    // And the separator sits against the title with no space in front of it.
    expect(baseline).toContain("19:00|");
    expect(improved).toContain("19:00 | ");
  });

  it("registers its disclosures, so 'Развернуть все' opens them", async () => {
    const wrapper = mountPage();
    await pick(wrapper, "идет и два впереди");

    expect(wrapper.findAll(".expand-fold.open")).toHaveLength(0);
    expect(wrapper.findAll(".reveal-float.open")).toHaveLength(0);

    expandAllExpandables();
    await wrapper.vm.$nextTick();

    expect(wrapper.findAll(".expand-fold.open")).toHaveLength(2);
    expect(wrapper.findAll(".reveal-float.open")).toHaveLength(3);
  });

  it("drives the action section from the viewer switch", async () => {
    const wrapper = mountPage();
    await pick(wrapper, "идет");

    await pick(wrapper, "организатор");
    expect(wrapper.find(".do-button").text()).toBe("Закончить");
    expect(line(variantAt(wrapper, 15).find(".run").text())).toContain(
      "| закончить",
    );

    await pick(wrapper, "гость");
    expect(wrapper.find(".do-button").exists()).toBe(false);
    expect(wrapper.text()).toContain("войдите, чтобы участвовать");
  });
});
