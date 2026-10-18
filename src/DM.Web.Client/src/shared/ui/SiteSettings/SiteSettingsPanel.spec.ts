/**
 * @vitest-environment jsdom
 */

/**
 * The rows of the settings of the site, in the bubble of the gear of the
 * floating column (ScrollNav) at every width.
 *
 * "Перенос слов", the row the panel took from the old site's own panel, is the
 * switch of the "Тема" row, the same control of the same size, and not the old
 * site's pair of "Включить | Выключить" buttons: those tripled the width of
 * the panel. It is on for a device that has never answered, and what the
 * reader turns is kept on the device.
 *
 * "Выравнивание по ширине" came from the same panel and is gone: the owner
 * took the switch away, and posts and comments are always justified.
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { Theme } from "@/shared/api/models/personal";
import SiteSettingsPanel from "./SiteSettingsPanel.vue";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

enableAutoUnmount(afterEach);

const SWITCHES = [
  {
    label: "Перенос слов",
    key: "dm_hyphenate_words",
    read: () => useUiStore().hyphenateWords,
  },
];

async function openPanel() {
  const wrapper = mount(SiteSettingsPanel, {
    attachTo: document.body,
    global: { plugins: [createPinia()] },
  });
  await flushPromises();
  return wrapper;
}

type Panel = Awaited<ReturnType<typeof openPanel>>;

/** The row of the panel under `label`. */
const row = (wrapper: Panel, label: string) => {
  const found = wrapper
    .findAll(".settings-row")
    .find((one) => one.get(".settings-label").text() === label);
  if (!found) throw new Error(`the panel has no "${label}" row`);
  return found;
};

/** The checkbox of the switch in the row under `label`. */
const checkbox = (wrapper: Panel, label: string) =>
  row(wrapper, label).get<HTMLInputElement>('input[type="checkbox"]');

/** Turns the switch the way a click on it does: the box flips, then reports. */
async function turn(wrapper: Panel, label: string, on: boolean) {
  await checkbox(wrapper, label).setValue(on);
  await flushPromises();
}

describe("SiteSettingsPanel typesetting switches", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("lists the rows under their labels, and no justification", async () => {
    const wrapper = await openPanel();

    const labels = wrapper
      .findAll(".settings-label")
      .map((label) => label.text());
    expect(labels).toEqual(["Верстка", "Тема", "Перенос слов"]);
    expect(
      wrapper.find('input[aria-label="Выравнивание по ширине"]').exists(),
    ).toBe(false);
  });

  /**
   * The owner's word on the pair of buttons these rows first had: a row of the
   * panel is a label and a small control, like its neighbours. So each row
   * holds exactly what the "Тема" row holds, one switch named after the row,
   * and no button at all.
   */
  it("gives each row the switch of the theme row and no buttons", async () => {
    const wrapper = await openPanel();

    // The class the switch is drawn by, and the theme row wears it too: the
    // comparison below is with that row, not with a name picked for the test.
    expect(row(wrapper, "Тема").find(".settings-switch").exists()).toBe(true);
    for (const { label } of SWITCHES) {
      const one = row(wrapper, label);
      expect(one.findAll("button"), label).toHaveLength(0);
      expect(one.findAll('input[type="checkbox"]'), label).toHaveLength(1);
      expect(checkbox(wrapper, label).attributes("aria-label")).toBe(label);
      expect(one.find(".settings-switch").exists(), label).toBe(true);
    }
  });

  it("keeps the moon to the theme row", async () => {
    const wrapper = await openPanel();

    expect(row(wrapper, "Тема").get(".settings-switch").classes()).toContain(
      "settings-switch--theme",
    );
    for (const { label } of SWITCHES) {
      expect(
        row(wrapper, label).get(".settings-switch").classes(),
        label,
      ).not.toContain("settings-switch--theme");
    }
  });

  for (const { label, key, read } of SWITCHES) {
    it(`shows "${label}" on for a device that has never answered`, async () => {
      const wrapper = await openPanel();

      expect(checkbox(wrapper, label).element.checked).toBe(true);
    });

    it(`shows "${label}" off when it was turned off on a previous visit`, async () => {
      localStorage.setItem(key, "off");
      const wrapper = await openPanel();

      expect(checkbox(wrapper, label).element.checked).toBe(false);
    });

    it(`turns "${label}" off and on again and keeps it on the device`, async () => {
      const wrapper = await openPanel();

      await turn(wrapper, label, false);
      expect(read()).toBe(false);
      expect(localStorage.getItem(key)).toBe("off");
      expect(checkbox(wrapper, label).element.checked).toBe(false);

      await turn(wrapper, label, true);
      expect(read()).toBe(true);
      expect(localStorage.getItem(key)).toBe("on");
      expect(checkbox(wrapper, label).element.checked).toBe(true);
    });
  }

  it("leaves the theme alone when the typesetting row is turned", async () => {
    const wrapper = await openPanel();
    const before = useUiStore().theme;

    await turn(wrapper, "Перенос слов", false);

    expect(useUiStore().theme).toBe(before);
    expect(checkbox(wrapper, "Тема").element.checked).toBe(false);
  });

  /**
   * On a phone the switch is a small target at the far end of its row. The
   * row is the label of its switch, so a press on the word turns it as well.
   */
  it("turns a switch from a press anywhere on its row", async () => {
    const wrapper = await openPanel();
    const dark = useUiStore().theme === Theme.Dark;

    (
      row(wrapper, "Тема").get(".settings-label").element as HTMLElement
    ).click();
    await flushPromises();
    expect(useUiStore().theme === Theme.Dark).toBe(!dark);

    (
      row(wrapper, "Перенос слов").get(".settings-label").element as HTMLElement
    ).click();
    await flushPromises();
    expect(useUiStore().hyphenateWords).toBe(false);
  });

  it("switches the layout of messages from its row", async () => {
    const wrapper = await openPanel();

    await wrapper
      .get('button[aria-label="Компактная верстка сообщений"]')
      .trigger("click");
    expect(useUiStore().messageLayout).toBe("compact");

    await wrapper
      .get('button[aria-label="Полная верстка сообщений"]')
      .trigger("click");
    expect(useUiStore().messageLayout).toBe("full");
  });
});

/**
 * How the switches stand in the panel, read off its stylesheet. The panel is
 * drawn in the browser by e2e/tests/common/settings-panel.spec.ts; these are
 * the rules that drawing rests on.
 */
describe("SiteSettingsPanel switches in the panel", () => {
  const rules = styleRules(
    componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "SiteSettingsPanel.vue"),
    ),
  );
  const declared = (selector: string) =>
    rules
      .filter((rule) => rule.selector === selector)
      .map((rule) => rule.declarations);

  // The bubble hangs off the right edge of the screen, so its width is its
  // position: a controls column sized by its widest control moved every label
  // whenever a control changed.
  it("declares the controls column instead of finding it", () => {
    expect(declared(".settings-panel")[0]["grid-template-columns"]).toBe(
      "auto 64px",
    );
  });

  // The owner's word: the switches were not centred under the layout toggle.
  // Both stand in the middle of the controls column, so their centres meet
  // whatever either of them measures; the labels keep the left edge.
  it("centres every switch under the layout toggle", () => {
    expect(declared(".settings-row")[0]["justify-items"]).toBe("start");
    expect(declared(".settings-control")[0]["justify-self"]).toBe("center");
    expect(declared(".layout-toggle")[0]["justify-self"]).toBe("center");
  });

  // The input takes the caret but is hidden at no size, so the ring of a
  // focused control has to go round the switch the reader sees.
  it("rings the switch the caret is on, as every block control is ringed", () => {
    expect(declared(".settings-switch input:focus-visible + .slider")).toEqual([
      { outline: "2px solid var(--link)", "outline-offset": "0" },
    ]);
  });
});
