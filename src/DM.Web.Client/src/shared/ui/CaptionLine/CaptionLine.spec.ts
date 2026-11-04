/**
 * @vitest-environment jsdom
 */

// The bars are the line's to place: the tests toggle parts as callers do and read the text a reader
// would copy. Where the line breaks, and that the browsers honour it, is the live stand's to show.
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub, shallowMount } from "@vue/test-utils";
import { defineComponent, h, nextTick, ref } from "vue";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import CaptionLine from "./CaptionLine.vue";
import CaptionPart from "./CaptionPart.vue";
import CaptionAction from "./CaptionAction.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

/** The like of the site, as a caption line meets it: by its name. */
const LikeBadge = defineComponent({
  name: "LikeBadge",
  render: () => h("span", { class: "like-badge" }, "♡4"),
});

/** The line as a reader copies it: runs of white space as one space. */
const copied = (element: Element): string =>
  (element.textContent ?? "").replace(/\s+/g, " ").trim();

function comment(initial: {
  edit?: boolean;
  remove?: boolean;
  warn?: boolean;
}) {
  const edit = ref(initial.edit ?? true);
  const remove = ref(initial.remove ?? true);
  const warn = ref(initial.warn ?? true);
  const wrapper = mount(
    defineComponent({
      components: { CaptionLine, CaptionPart, CaptionAction, LikeBadge },
      setup: () => ({ edit, remove, warn }),
      template: `
        <CaptionLine>
          <CaptionPart :bar="false">Автор: <a href="/u/x">X</a>,</CaptionPart>
          <span>09.09.2026 в 06:33</span>
          <CaptionAction v-if="edit">Редактировать</CaptionAction>
          <CaptionAction v-if="remove">Удалить</CaptionAction>
          <CaptionAction v-if="warn">Предупреждение</CaptionAction>
          <LikeBadge />
        </CaptionLine>
      `,
    }),
  );
  return { wrapper, edit, remove, warn };
}

describe("CaptionLine: the bars", () => {
  it("puts one bar between every two parts and a space after the author", () => {
    const { wrapper } = comment({});
    expect(copied(wrapper.element)).toBe(
      "Автор: X, 09.09.2026 в 06:33 | Редактировать | Удалить | Предупреждение | ♡4",
    );
  });

  it("closes up over a part that v-if leaves out, wherever it stood", async () => {
    const { wrapper, edit, remove, warn } = comment({});

    remove.value = false;
    await nextTick();
    expect(copied(wrapper.element)).toBe(
      "Автор: X, 09.09.2026 в 06:33 | Редактировать | Предупреждение | ♡4",
    );

    warn.value = false;
    await nextTick();
    expect(copied(wrapper.element)).toBe(
      "Автор: X, 09.09.2026 в 06:33 | Редактировать | ♡4",
    );

    edit.value = false;
    await nextTick();
    expect(copied(wrapper.element)).toBe("Автор: X, 09.09.2026 в 06:33 | ♡4");

    remove.value = true;
    await nextTick();
    expect(copied(wrapper.element)).toBe(
      "Автор: X, 09.09.2026 в 06:33 | Удалить | ♡4",
    );
  });

  it("makes each child of a v-for a part of its own", () => {
    const wrapper = mount(
      defineComponent({
        components: { CaptionLine, CaptionAction },
        setup: () => ({ actions: ["Редактировать", "Закрыть", "Удалить"] }),
        template: `
          <CaptionLine>
            <span>Комментарии (4)</span>
            <CaptionAction v-for="action in actions" :key="action">{{ action }}</CaptionAction>
          </CaptionLine>
        `,
      }),
    );
    expect(copied(wrapper.element)).toBe(
      "Комментарии (4) | Редактировать | Закрыть | Удалить",
    );
    expect(wrapper.findAll(".caption-line__part")).toHaveLength(4);
  });

  it("draws nothing between the parts for a single part", () => {
    const wrapper = mount(CaptionLine, {
      slots: { default: "<span>Редактировать</span>" },
    });
    expect(copied(wrapper.element)).toBe("Редактировать");
    expect(wrapper.find(".caption-line__bar").exists()).toBe(false);
  });

  it("keeps the bars out of the reader's speech", () => {
    const { wrapper } = comment({});
    const bars = wrapper.findAll(".caption-line__bar");
    expect(bars).toHaveLength(4);
    for (const bar of bars) {
      expect(bar.attributes("aria-hidden")).toBe("true");
    }
  });
});

/**
 * The line's own children in order, as a line break meets them: a part, a gap
 * with a bar, or the plain space of a part that opts out of its bar. Empty
 * text nodes are the anchors Vue puts around a fragment; they hold no
 * character and so no wrap opportunity.
 */
function joints(line: Element): string[] {
  return Array.from(line.childNodes)
    .filter((node) => node.nodeType !== Node.TEXT_NODE || node.textContent)
    .map((node) =>
      node.nodeType === Node.TEXT_NODE
        ? JSON.stringify(node.textContent)
        : (node as Element).className,
    );
}

const PART = "caption-line__part";
const GAP = "caption-line__gap";
const SPACE = JSON.stringify(" ");

describe("CaptionLine: where it may break", () => {
  const css = componentCss(resolve(HERE, "CaptionLine.vue"));

  it("sets two parts apart by a gap, or by the line's space where a part says so", () => {
    const { wrapper } = comment({});
    // The author runs into the date with a space; a gap stands before each
    // action and before the like.
    expect(joints(wrapper.element)).toEqual([
      PART,
      SPACE,
      PART,
      GAP,
      PART,
      GAP,
      PART,
      GAP,
      PART,
      GAP,
      PART,
    ]);
  });

  it("ends a part with its mark where it says so, and runs on with the line's space", () => {
    const wrapper = mount(
      defineComponent({
        components: { CaptionLine, CaptionPart },
        template: `
          <CaptionLine>
            <CaptionPart joint=",">Автор: X</CaptionPart>
            <CaptionPart joint=",">09.09.2026</CaptionPart>
          </CaptionLine>
        `,
      }),
    );
    expect(copied(wrapper.element)).toBe("Автор: X, 09.09.2026");
    expect(joints(wrapper.element)).toEqual([PART, SPACE, PART]);
    // The mark is the last thing of the part before the space, so a break
    // after it leaves the comma at the end of the line, and the part goes to
    // the next line with it rather than without it. Heard as the space after
    // it, as the bar is.
    const [author, date] = wrapper.findAll(`.${PART}`);
    const mark = author.element.lastElementChild!;
    expect(mark.className).toBe("caption-line__mark");
    expect(mark.textContent).toBe(",");
    expect(mark.getAttribute("aria-hidden")).toBe("true");
    // The last part joins nothing.
    expect(date.find(".caption-line__mark").exists()).toBe(false);
  });

  it("keeps every bar out of the parts", () => {
    const { wrapper } = comment({});
    for (const part of wrapper.findAll(`.${PART}`)) {
      expect(part.find(".caption-line__bar").exists()).toBe(false);
    }
  });

  it("gives a gap its bar and then the breakable space, and nothing else", () => {
    const { wrapper } = comment({});
    const gaps = wrapper.findAll(`.${GAP}`);
    expect(gaps).toHaveLength(4);
    for (const gap of gaps) {
      // The space last, so the break after it is the end of the gap and the
      // gap never splits over two lines.
      const [bar, space, ...rest] = Array.from(gap.element.childNodes);
      expect((bar as Element).className).toBe("caption-line__bar");
      expect(bar.textContent).toBe(" |");
      expect(space.nodeType).toBe(Node.TEXT_NODE);
      expect(space.textContent).toBe(" ");
      expect(rest).toEqual([]);
    }
  });

  it("lets a part that is words of the sentence wrap at its spaces", () => {
    // A title after the dash of a list: as one box it went to the next line
    // whole and left the dash alone above it.
    const Caller = defineComponent({
      components: { CaptionLine, CaptionPart, CaptionAction },
      template: `
        <CaptionLine>
          <CaptionPart flow>Обсуждение правил ролевой игры</CaptionPart>
          <CaptionAction>Отписаться</CaptionAction>
        </CaptionLine>
      `,
    });
    for (const wrapper of [
      mount(Caller),
      shallowMount(Caller, { global: { stubs: { CaptionLine: false } } }),
    ]) {
      const [title, action] = wrapper.findAll(`.${PART}`);
      expect(title.classes()).toContain("caption-line__part--flow");
      expect(action.classes()).not.toContain("caption-line__part--flow");
    }
  });

  it("clips the gap itself in Firefox and nowhere else", () => {
    // Firefox sizes the bar to the gap before the break empties it; Chromium
    // loses bars in the middle of a line under a clip-path on an inline.
    expect(css).toMatch(
      /@supports \(-moz-appearance: none\) \{\s*\.caption-line__gap \{\s*clip-path: inset\(0\);\s*\}\s*\}/,
    );
    expect(css.match(/clip-path/g)).toHaveLength(1);
  });
});

describe("CaptionLine: one size for the line", () => {
  it("sets the size the caller picks", () => {
    const wrapper = mount(CaptionLine, {
      props: { size: "secondary" },
      slots: { default: "<span>x</span>" },
    });
    expect(wrapper.classes()).toContain("caption-line--secondary");
    expect(mount(CaptionLine).classes()).toContain("caption-line--tertiary");
  });
});

describe("CaptionAction", () => {
  const rules = ruleMap(componentCss(resolve(HERE, "CaptionAction.vue")));
  const hover = rules.get(".caption-action:hover:not(:disabled)") ?? {};

  it("is a button of type button that passes its listeners through", async () => {
    let pressed = 0;
    const wrapper = mount(CaptionAction, {
      slots: { default: "Удалить" },
      attrs: { onClick: () => (pressed += 1) },
    });
    const button = wrapper.get("button");
    expect(button.attributes("type")).toBe("button");
    await button.trigger("click");
    expect(pressed).toBe(1);
  });

  it("is a link when it navigates", () => {
    const to = {
      name: "blog-publication-edit",
      params: { id: "1", pubId: "2" },
    };
    const wrapper = mount(CaptionAction, {
      props: { to },
      slots: { default: "Редактировать" },
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    const link = wrapper.getComponent(RouterLinkStub);
    expect(link.props("to")).toEqual(to);
    expect(link.classes()).toContain("caption-action");
    expect(wrapper.find("button").exists()).toBe(false);
  });

  it("answers the pointer the way a grey link does", () => {
    // The actions of a caption line behave as the site's grey links, which
    // turn $link-hover with the underline, as every link of DM2 did.
    expect(hover).toEqual({
      color: "var(--link-hover)",
      "text-decoration": "underline",
    });
    expect(rules.get(".caption-action")?.transition).toBe("color 0.15s ease");
  });
});
