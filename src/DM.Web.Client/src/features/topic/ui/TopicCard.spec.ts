/**
 * @vitest-environment jsdom
 */

/**
 * The footer of a topic card (and of a publication, which is drawn by the same
 * card) is one caption line: who wrote it and when, "Отредактировано" and the
 * comments counter, then what the viewer may do, each after " | ", and the like
 * last of all, after every action.
 *
 * The line is read the way a reader copies it. The order is the owner's, and the
 * like being last is his correction of the approved mock-up, so it is asserted
 * by position: the badge is the last part of the line.
 */
import { describe, it, expect } from "vitest";
import { nextTick } from "vue";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { useUiStore } from "@/shared/stores/ui";
import type { Username } from "@/shared/api/models/common/user";
import { UserRole } from "@/shared/api/models/common/user";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import TopicCard from "./TopicCard.vue";

const CREATED = "2026-09-09T06:33:00Z";
const MODIFIED = "2026-09-10T08:06:00Z";

const author = {
  username: "SolohinLex" as Username,
  role: UserRole.Moderator,
  lastActivityUtc: null,
};

const liker = (name: string) => ({ username: name as Username });

type Props = InstanceType<typeof TopicCard>["$props"];

function render(props: Partial<Props> = {}) {
  return mount(TopicCard, {
    props: {
      title: "Правила",
      contentHtml: "<p>Текст</p>",
      author,
      createdUtc: CREATED,
      commentsCount: 3,
      commentsTo: { name: "topic" },
      ...props,
    },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
}

/** The caption line as a reader copies it. */
const copied = (wrapper: ReturnType<typeof render>): string =>
  (wrapper.get(".caption-line").element.textContent ?? "")
    .replace(/\s+/g, " ")
    .trim();

const parts = (wrapper: ReturnType<typeof render>) =>
  wrapper.findAll(".caption-line > .caption-line__part");

/**
 * A date as it reaches the clipboard: the no-break space after "в" is copied
 * as a plain one.
 */
const copiedDate = (utc: string) => formatDateFull(utc).replace(/\s/g, " ");

const date = copiedDate(CREATED);

describe("TopicCard: the caption line", () => {
  it("runs author, date and counter, then the actions, the like last", () => {
    const wrapper = render({
      canEdit: true,
      canDelete: true,
      canWarn: true,
      canLike: true,
      likes: [liker("Player-Two"), liker("TestUser")],
    });

    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3) | ` +
        "Редактировать | Удалить | Предупреждение | 2",
    );
    const all = parts(wrapper);
    expect(all.at(-1)!.find(".like-badge").exists()).toBe(true);
    expect(wrapper.findAll(".like-badge")).toHaveLength(1);
  });

  // Closing and reopening are a setting of the edit form, so a closed topic
  // offers the same words as an open one and no "Открыть" of its own.
  it("puts the edit date after the date and has no Открыть on a closed topic", () => {
    const wrapper = render({
      modifiedUtc: MODIFIED,
      isClosed: true,
      canEdit: true,
    });

    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | ` +
        `Отредактировано ${copiedDate(MODIFIED)} | Комментарии (3) | Редактировать`,
    );
  });

  it("joins the like to the counter when the viewer has no action", () => {
    const wrapper = render({ canLike: true });

    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3) |`,
    );
    const last = parts(wrapper).at(-1)!;
    expect(last.find("button.like-badge").attributes("aria-pressed")).toBe(
      "false",
    );
  });

  it("emits the action a word of the line stands for", async () => {
    const wrapper = render({
      canEdit: true,
      canDelete: true,
      canWarn: true,
      canLike: true,
    });
    const action = (text: string) =>
      wrapper.findAll("button.caption-action").find((b) => b.text() === text)!;

    await action("Редактировать").trigger("click");
    await action("Удалить").trigger("click");
    await action("Предупреждение").trigger("click");
    await wrapper.get("button.like-badge").trigger("click");

    expect(Object.keys(wrapper.emitted())).toEqual(
      expect.arrayContaining(["edit", "delete", "warn", "toggleLike"]),
    );
  });

  it("links Редактировать to the edit page when the card is given one", () => {
    const to = { name: "blog-publication-edit", params: { pubId: "p-1" } };
    const wrapper = render({ editTo: to, canEdit: true });

    const edit = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.text() === "Редактировать")!;
    expect(edit.props("to")).toEqual(to);
    // The link stands in for the event button, not beside it.
    expect(wrapper.findAll(".caption-action")).toHaveLength(1);
    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3) | Редактировать`,
    );
  });
});

describe("TopicCard: the like", () => {
  it("draws the likes of one's own text, filled or not, and does not press", () => {
    const wrapper = render({
      canLike: false,
      isLikedByMe: false,
      likes: [liker("Player-Two"), liker("TestUser"), liker("TestHonorary")],
    });

    expect(wrapper.find("button.like-badge").exists()).toBe(false);
    expect(wrapper.get(".like-badge").attributes("aria-label")).toBe(
      "Нравится: 3",
    );
  });

  it("shows no heart where there is nothing to press and nobody to list", () => {
    const wrapper = render({ canLike: false, likes: [] });

    expect(wrapper.find(".like-badge").exists()).toBe(false);
    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3)`,
    );
  });

  it("fills the heart the viewer has pressed", () => {
    const wrapper = render({
      canLike: true,
      isLikedByMe: true,
      likes: [liker("TestUser")],
    });

    expect(wrapper.get("button.like-badge").attributes("aria-pressed")).toBe(
      "true",
    );
  });
});

describe("TopicCard: the preview (home news)", () => {
  it("keeps the caption and drops every action and the like", () => {
    const wrapper = render({
      previewOnly: true,
      canWarn: true,
      canLike: true,
      likes: [liker("TestUser")],
    });

    expect(wrapper.find(".caption-action").exists()).toBe(false);
    expect(wrapper.find(".like-badge").exists()).toBe(false);
    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3)`,
    );
  });
});

/**
 * The caption wraps between its parts at every width and puts nothing away
 * (owner, 12 October 2026: a block on a phone is the block of the full
 * version, with no "Еще" and no "…", the name never cut). Where it breaks
 * is the caption line's, read in its own spec and on the live stand; here the
 * card is read for drawing every part whole and for writing nothing that would
 * put one away.
 */
describe("TopicCard: every part at every width", () => {
  const moderator: Partial<Props> = {
    modifiedUtc: MODIFIED,
    canEdit: true,
    canDelete: true,
    canWarn: true,
    canLike: true,
    likes: [liker("Player-Two"), liker("TestUser")],
  };

  it("is the line itself under the text, not a row that scrolls", () => {
    const wrapper = render(moderator);

    expect(wrapper.find(".scroll-row").exists()).toBe(false);
    const footer = wrapper.get(".topic-footer").element;
    expect(footer.children).toHaveLength(1);
    expect(footer.children[0].matches(".caption-line")).toBe(true);
  });

  it("draws every part a moderator has, with no Еще and the name whole", () => {
    const wrapper = render(moderator);
    const line = wrapper.get(".caption-line");

    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | ` +
        `Отредактировано ${copiedDate(MODIFIED)} | Комментарии (3) | ` +
        "Редактировать | Удалить | Предупреждение | 2",
    );
    expect(line.find(".more-menu").exists()).toBe(false);
    expect(line.find("[hidden]").exists()).toBe(false);
    const name = wrapper.get(".author-link");
    expect(name.attributes("title")).toBeUndefined();
    expect(name.attributes("style")).toBeUndefined();
    // Nothing is written for the queue of a line that put parts away.
    expect(
      line.element.querySelectorAll(
        "[data-fit-order], [data-fit-shrink], [data-fit-keep]",
      ),
    ).toHaveLength(0);
  });

  it("ends the author's part with the comma and keeps the date and its time one part", () => {
    const wrapper = render({});
    const [who, when] = parts(wrapper);

    // The comma ends the author, so a phone breaks after it and the date
    // goes to the next line whole, with its time.
    expect(who.element.lastElementChild?.textContent).toBe(",");
    expect(when.text().replace(/\s+/g, " ")).toBe(date);
  });

  it("has no comma after the author when there is no date to run into", () => {
    const wrapper = render({ createdUtc: null });

    expect(wrapper.find(".caption-line__mark").exists()).toBe(false);
    expect(copied(wrapper)).toBe(
      "Автор: SolohinLex [M] [offline] | Комментарии (3)",
    );
  });
});

/**
 * The text of a topic stands as the text of a comment, DM2 showing the topic
 * as its comment No 1: on the step of 20, flush on both sides at every width,
 * and hyphenated while "Перенос слов" of the settings panel is on.
 */
describe("TopicCard: the text", () => {
  it("justifies at every width and hyphenates through the class alone", () => {
    const css = componentCss(
      resolve(dirname(fileURLToPath(import.meta.url)), "TopicCard.vue"),
    );
    const merged = (selector: string) =>
      Object.assign(
        {},
        ...styleRules(css)
          .filter((rule) => rule.selector === selector)
          .map((rule) => rule.declarations),
      ) as Record<string, string>;

    expect(css).not.toMatch(/@media/);
    expect(merged(".topic-text")).toMatchObject({
      "line-height": "1.25",
      "text-align": "justify",
    });
    expect(merged(".topic-text.hyphenated")).toMatchObject({
      hyphens: "auto",
      "-webkit-hyphens": "auto",
    });
    // The switch turned off has to take the property with it, so the bare
    // block may not carry it on its own.
    expect(merged(".topic-text")).not.toHaveProperty("hyphens");
  });

  it("follows the switch of the settings panel", async () => {
    localStorage.clear();
    setActivePinia(createPinia());
    const ui = useUiStore();
    ui.setHyphenateWords(true);
    const wrapper = render();

    expect(wrapper.get(".topic-text").classes()).toContain("hyphenated");
    ui.setHyphenateWords(false);
    await nextTick();
    expect(wrapper.get(".topic-text").classes()).not.toContain("hyphenated");
  });
});
