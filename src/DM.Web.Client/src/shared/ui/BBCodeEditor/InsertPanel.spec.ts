/**
 * @vitest-environment jsdom
 */

/**
 * The panel an insertion of the markup editor opens under its button: what it
 * asks, what it refuses and says so in itself, and how it lets go. The editor's
 * side, the button and what lands in the text, is in BBCodeEditor.spec.ts.
 */
import { describe, it, expect, afterEach, vi } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { defineComponent, h, nextTick } from "vue";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  componentCss,
  outranks,
  ruleMap,
  scopedComponentCss,
  specificity,
  styleRules,
} from "@/shared/lib/testing/styles";
import FormField from "@/shared/ui/Form/FormField.vue";
import InsertPanel, { type InputField } from "./InsertPanel.vue";
import { IN_DIALOG } from "@/shared/ui/Layout/dialogContext";

const HERE = dirname(fileURLToPath(import.meta.url));

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
});

const address: InputField[] = [
  {
    name: "url",
    label: "URL",
    type: "url",
    placeholder: "https://example.com",
    required: true,
  },
];

async function mountPanel(
  fields: InputField[] = address,
  anchor?: HTMLElement,
) {
  const button = anchor ?? document.createElement("button");
  if (!button.isConnected) document.body.appendChild(button);
  const wrapper = mount(InsertPanel, {
    attachTo: document.body,
    props: { label: "Вставить ссылку", fields, anchor: button },
  });
  // The caret goes into the first field on the tick after mounting.
  await nextTick();
  return { wrapper, button };
}

const field = (wrapper: ReturnType<typeof mount>, name: string) =>
  wrapper.get<HTMLInputElement>(`input[name="${name}"]`);

const pressButton = (wrapper: ReturnType<typeof mount>, text: string) =>
  wrapper
    .findAll("button")
    .find((button) => button.text() === text)!
    .trigger("click");

const refusals = (wrapper: ReturnType<typeof mount>) =>
  wrapper.findAll(".insert-field-error").map((line) => line.text());

describe("InsertPanel", () => {
  // The panel of a [tip] in the window of a tag opens with the word the
  // moderator selected already in its field, and the caret waits in the
  // first field left to fill.
  it("opens with the values it is given, the caret in the first empty field", async () => {
    const { wrapper } = await mountPanel([
      { name: "word", label: "Слово", required: true, value: "спорные" },
      { name: "text", label: "Текст подсказки", required: true },
    ]);

    expect(field(wrapper, "word").element.value).toBe("спорные");
    expect(document.activeElement).toBe(field(wrapper, "text").element);

    await field(wrapper, "text").setValue("Политика");
    await pressButton(wrapper, "Вставить");
    expect(wrapper.emitted("submit")?.[0]).toEqual([
      { word: "спорные", text: "Политика" },
    ]);
  });

  describe("on the page", () => {
    it("is a panel and not a window: named, not modal, nothing dimmed", async () => {
      const { wrapper } = await mountPanel();
      const panel = wrapper.get('[role="dialog"]');
      expect(panel.attributes("aria-label")).toBe("Вставить ссылку");
      expect(panel.attributes("aria-modal")).toBeUndefined();
      // No backdrop and no second layer: the panel is the whole component.
      expect(wrapper.element).toBe(panel.element);
      expect(document.querySelector(".dialog-backdrop")).toBeNull();
    });

    it.each([
      ["in a window", true, []],
      [
        "on a page, while the field around is refused",
        false,
        ["Слишком длинное значение"],
      ],
    ] as const)(
      "keeps its own fields %s",
      async (_where, inWindow, refusal) => {
        // The editor stands in a field of a form, the reason of a warning, and
        // that field's rules reach every input and caption under it by class:
        // in a window a panel of FormField came out with captions in bold at
        // 15 and fields at 52, and on a page its fields go red with the
        // reason. Both sheets read as the build scopes them, the heavier rule
        // winning and a tie going to the field around, the worse case.
        const anchor = document.createElement("button");
        document.body.appendChild(anchor);
        const wrapper = mount(FormField, {
          attachTo: document.body,
          props: { label: "Причина предупреждения", errors: [...refusal] },
          slots: {
            default: () =>
              h(InsertPanel, {
                label: "Вставить ссылку",
                fields: address,
                anchor,
              }),
          },
          global: { provide: { [IN_DIALOG as symbol]: inWindow } },
        });
        await nextTick();
        const outer = wrapper.element as HTMLElement;
        const panel = outer.querySelector<HTMLElement>('[role="dialog"]')!;
        const scopeOf = (element: Element) =>
          element
            .getAttributeNames()
            .find((name) => name.startsWith("data-v-"))!;
        const sheets = [
          {
            owner: "field",
            css: scopedComponentCss(
              join(HERE, "../Form/FormField.vue"),
              scopeOf(outer),
            ),
          },
          {
            owner: "panel",
            css: scopedComponentCss(
              join(HERE, "InsertPanel.vue"),
              scopeOf(panel),
            ),
          },
        ];
        const rulesFor = (element: Element) =>
          sheets.flatMap(({ owner, css }) =>
            styleRules(css).flatMap(({ selector, declarations }) =>
              selector
                .split(",")
                .map((one) => one.trim())
                .filter((one) => {
                  try {
                    return !one.includes("::") && element.matches(one);
                  } catch {
                    return false;
                  }
                })
                .map((one) => ({
                  owner,
                  declarations,
                  weight: specificity(one),
                })),
            ),
          );
        /** Which sheet each property of the element comes from. */
        const winners = (element: Element) => {
          const rules = rulesFor(element);
          const won: Record<string, string> = {};
          for (const property of new Set(
            rules.flatMap((r) => Object.keys(r.declarations)),
          )) {
            const [first] = rules
              .filter((r) => property in r.declarations)
              .sort((a, b) =>
                outranks(a.weight, b.weight)
                  ? -1
                  : outranks(b.weight, a.weight)
                    ? 1
                    : a.owner === "field"
                      ? -1
                      : 1,
              );
            won[property] = first.owner;
          }
          return won;
        };

        // The check can see: the field around does style its own caption.
        expect(
          Object.values(winners(outer.querySelector(".form-field-label")!)),
        ).toContain("field");
        const taken = [panel, ...panel.querySelectorAll("*")].flatMap(
          (element) =>
            Object.entries(winners(element))
              .filter(([, owner]) => owner === "field")
              .map(
                ([property]) =>
                  `${element.tagName.toLowerCase()}.${element.className} ${property}`,
              ),
        );
        expect(taken).toEqual([]);
        anchor.remove();
      },
    );

    it('marks an optional field with "необязательно" and a required one with nothing', async () => {
      const { wrapper } = await mountPanel([
        { name: "url", label: "URL ссылки", type: "url", required: true },
        { name: "text", label: "Текст ссылки", type: "text" },
      ]);
      const [required, optional] = wrapper.findAll(".insert-field-label");
      expect(required.text()).toBe("URL ссылки");
      expect(optional.find(".insert-field-optional").text()).toBe(
        "необязательно",
      );
      expect(wrapper.text()).not.toContain("*");
    });

    it("puts the caret in the first empty field", async () => {
      const { wrapper } = await mountPanel([
        { name: "url", label: "URL", type: "url" },
        { name: "text", label: "Текст", type: "text" },
      ]);
      await nextTick();
      expect(document.activeElement).toBe(field(wrapper, "url").element);
    });

    it("gives each field an id of its own panel", async () => {
      const one = (await mountPanel()).wrapper;
      const two = (await mountPanel()).wrapper;
      expect(field(one, "url").attributes("id")).not.toBe(
        field(two, "url").attributes("id"),
      );
    });

    it("sets its captions as a field on a page has them", () => {
      // The captions were in the weight of the text, and the owner found
      // them weaker than a form's (28 September 2026). Bold, at the size of a
      // page's field, with "необязательно" beside it in the regular weight.
      const panel = ruleMap(componentCss(join(HERE, "InsertPanel.vue")));
      const page = ruleMap(componentCss(join(HERE, "../Form/FormField.vue")));
      expect(panel.get(".insert-field-label")?.["font-weight"]).toBe("700");
      expect(panel.get(".insert-field-optional")?.["font-weight"]).toBe("400");
      for (const property of ["font-weight", "font-size"]) {
        expect(panel.get(".insert-field-label")?.[property], property).toBe(
          page.get(".form-field-label")?.[property],
        );
      }
    });

    it("puts the main action first in the filled button, and Отмена after it", async () => {
      // Both were grey, and which of the two inserted could not be told.
      const { wrapper } = await mountPanel();
      const buttons = wrapper.findAll("button");
      expect(buttons.map((button) => button.text())).toEqual([
        "Вставить",
        "Отмена",
      ]);
      expect(
        buttons.map((button) => button.classes().includes("primary")),
      ).toEqual([true, false]);
    });
  });

  describe("where it stands", () => {
    it("hangs under its button, left edges together, and inside the editor", async () => {
      // jsdom lays nothing out: the boxes are given here. The editor is 600
      // wide and the panel 300.
      const host = document.createElement("div");
      const anchor = document.createElement("button");
      host.appendChild(anchor);
      document.body.appendChild(host);
      let buttonLeft = 0;
      vi.spyOn(anchor, "getBoundingClientRect").mockImplementation(
        () => new DOMRect(buttonLeft, 10, 40, 20),
      );
      vi.spyOn(host, "getBoundingClientRect").mockReturnValue(
        new DOMRect(0, 0, 600, 300),
      );
      Object.defineProperty(host, "clientWidth", { value: 600 });
      vi.spyOn(HTMLElement.prototype, "offsetParent", "get").mockReturnValue(
        host,
      );
      vi.spyOn(HTMLElement.prototype, "offsetWidth", "get").mockReturnValue(
        300,
      );
      const under = async (left: number) => {
        buttonLeft = left;
        const wrapper = mount(InsertPanel, {
          attachTo: host,
          props: { label: "Вставить ссылку", fields: address, anchor },
        });
        await nextTick();
        const { style } = wrapper.element as HTMLElement;
        const at = [style.left, style.top];
        wrapper.unmount();
        return at;
      };

      expect(await under(100)).toEqual(["100px", "34px"]);
      // "…" at the right end of the editor's row: the panel moves left as far
      // as it takes to stay inside the editor.
      expect(await under(560)).toEqual(["300px", "34px"]);

      host.remove();
    });
  });

  describe("a refusal", () => {
    it("is said under its field, in the panel", async () => {
      const { wrapper } = await mountPanel();
      await field(wrapper, "url").setValue("javascript:alert(1)");
      await pressButton(wrapper, "Вставить");
      expect(refusals(wrapper)).toEqual([
        "Разрешены только адреса http и https",
      ]);
      expect(wrapper.emitted("submit")).toBeFalsy();
      expect(wrapper.emitted("close")).toBeFalsy();
    });

    it("comes when the reader leaves the field", async () => {
      const { wrapper } = await mountPanel();
      await field(wrapper, "url").setValue("not-a-url");
      await field(wrapper, "url").trigger("blur");
      expect(refusals(wrapper)).toEqual(["Введите корректный URL"]);
      await field(wrapper, "url").setValue("https://example.com");
      await field(wrapper, "url").trigger("blur");
      expect(refusals(wrapper)).toEqual([]);
    });

    it("takes only http and https, for any address field", async () => {
      const refusal = async (value: string) => {
        const { wrapper } = await mountPanel();
        await field(wrapper, "url").setValue(value);
        await field(wrapper, "url").trigger("blur");
        return refusals(wrapper)[0] ?? null;
      };
      for (const refused of [
        "mailto:test@example.com",
        "tel:+100",
        "javascript:alert(1)",
        "data:text/html,<script>alert(1)</script>",
        "ftp://files.example.com/file.txt",
      ]) {
        expect(await refusal(refused), refused).toBe(
          "Разрешены только адреса http и https",
        );
      }
      for (const accepted of ["http://example.com", "https://example.com"]) {
        expect(await refusal(accepted), accepted).toBeNull();
      }
    });

    it("says what is missing, and the field's own check speaks first", async () => {
      const { wrapper } = await mountPanel([
        { name: "a", label: "A", type: "text", required: true },
        {
          name: "b",
          label: "B",
          type: "text",
          required: true,
          validator: (value) => (value.trim() ? null : "Введите имя"),
        },
      ]);
      await pressButton(wrapper, "Вставить");
      expect(refusals(wrapper)).toEqual([
        "Это поле обязательно",
        "Введите имя",
      ]);
    });

    it("lets the address check run after the field's own passes", async () => {
      const { wrapper } = await mountPanel([
        { ...address[0], validator: () => null },
      ]);
      await field(wrapper, "url").setValue("javascript:alert(1)");
      await pressButton(wrapper, "Вставить");
      expect(refusals(wrapper)).toEqual([
        "Разрешены только адреса http и https",
      ]);
    });
  });

  describe("the keys", () => {
    it("inserts on Enter from a field, and keeps it from a form around", async () => {
      const { wrapper } = await mountPanel();
      await field(wrapper, "url").setValue("https://example.com");
      const enter = new KeyboardEvent("keydown", {
        key: "Enter",
        bubbles: true,
        cancelable: true,
      });
      field(wrapper, "url").element.dispatchEvent(enter);
      expect(wrapper.emitted("submit")?.[0]).toEqual([
        { url: "https://example.com" },
      ]);
      // Enter in a field submits the form the editor stands in otherwise.
      expect(enter.defaultPrevented).toBe(true);
    });

    it("leaves Enter on a button, and Shift+Enter, alone", async () => {
      const { wrapper } = await mountPanel();
      await field(wrapper, "url").setValue("https://example.com");
      await wrapper
        .findAll("button")
        .find((button) => button.text() === "Отмена")!
        .trigger("keydown", { key: "Enter" });
      await field(wrapper, "url").trigger("keydown", {
        key: "Enter",
        shiftKey: true,
      });
      expect(wrapper.emitted("submit")).toBeFalsy();
    });

    it("closes on Escape, gives the caret back to the button, and stops there", async () => {
      const heard: string[] = [];
      const Host = defineComponent({
        setup: () => () =>
          h("div", { onKeydown: (e: KeyboardEvent) => heard.push(e.key) }, [
            h(InsertPanel, {
              label: "Вставить ссылку",
              fields: address,
              anchor: button,
              onClose: () => heard.push("close"),
            }),
          ]),
      });
      const button = document.createElement("button");
      document.body.appendChild(button);
      const wrapper = mount(Host, { attachTo: document.body });
      await nextTick();

      await wrapper.get('input[name="url"]').trigger("keydown", {
        key: "Escape",
      });

      // The window around the editor listens on its root, and one Escape
      // closed the panel and the window together.
      expect(heard).toEqual(["close"]);
      expect(document.activeElement).toBe(button);
      button.remove();
    });
  });

  describe("letting go", () => {
    it('closes on "Отмена" and gives the caret back to the button', async () => {
      const { wrapper, button } = await mountPanel();
      await pressButton(wrapper, "Отмена");
      expect(wrapper.emitted("close")).toHaveLength(1);
      expect(wrapper.emitted("submit")).toBeFalsy();
      expect(document.activeElement).toBe(button);
      button.remove();
    });

    it("closes on a press elsewhere and leaves the caret where it went", async () => {
      const { wrapper, button } = await mountPanel();
      const elsewhere = document.createElement("div");
      document.body.appendChild(elsewhere);
      elsewhere.dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
      expect(wrapper.emitted("close")).toHaveLength(1);
      expect(document.activeElement).not.toBe(button);
      elsewhere.remove();
      button.remove();
    });

    it("leaves a press on the panel and on its own button to them", async () => {
      const { wrapper, button } = await mountPanel();
      field(wrapper, "url").element.dispatchEvent(
        new MouseEvent("pointerdown", { bubbles: true }),
      );
      button.dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
      expect(wrapper.emitted("close")).toBeFalsy();
      button.remove();
    });

    it("closes when the caret leaves it for the page", async () => {
      const { wrapper, button } = await mountPanel();
      const next = document.createElement("input");
      document.body.appendChild(next);
      await field(wrapper, "url").trigger("focusout", { relatedTarget: next });
      expect(wrapper.emitted("close")).toHaveLength(1);
      next.remove();
      button.remove();
    });

    it("stays while the caret moves inside it", async () => {
      const { wrapper, button } = await mountPanel([
        { name: "url", label: "URL", type: "url" },
        { name: "text", label: "Текст", type: "text" },
      ]);
      await field(wrapper, "url").trigger("focusout", {
        relatedTarget: field(wrapper, "text").element,
      });
      expect(wrapper.emitted("close")).toBeFalsy();
      button.remove();
    });

    it("hands over every field, trimmed by nobody", async () => {
      const { wrapper, button } = await mountPanel([
        { name: "url", label: "URL", type: "url", required: true },
        { name: "text", label: "Текст", type: "text" },
      ]);
      await field(wrapper, "url").setValue("https://example.com");
      await field(wrapper, "text").setValue(" Главная ");
      await pressButton(wrapper, "Вставить");
      expect(wrapper.emitted("submit")?.[0]).toEqual([
        { url: "https://example.com", text: " Главная " },
      ]);
      button.remove();
    });
  });
});
