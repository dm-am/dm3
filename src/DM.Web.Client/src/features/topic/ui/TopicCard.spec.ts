/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from "vitest";
import { nextTick } from "vue";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import type { Username } from "@/shared/api/models/common/user";
import { UserRole } from "@/shared/api/models/common/user";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  captionBadgeOutline,
  markupOutline,
} from "@/shared/lib/testing/markupOutline";
import { Tooltip } from "@/shared/ui/Tooltip";
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

const copied = (wrapper: ReturnType<typeof render>): string =>
  (wrapper.get(".caption-line").element.textContent ?? "")
    .replace(/\s+/g, " ")
    .trim();

const parts = (wrapper: ReturnType<typeof render>) =>
  wrapper.findAll(".caption-line > .caption-line__part");

// The no-break space after "в" is copied as a plain one.
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
        "Редактировать | Удалить | Предупреждение | Нравится (2)",
    );
    const all = parts(wrapper);
    expect(all.at(-1)!.find(".like-badge").exists()).toBe(true);
    expect(wrapper.findAll(".like-badge")).toHaveLength(1);
  });

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

  it("puts Нравится right after the counter when the viewer has no action", () => {
    const wrapper = render({ canLike: true });

    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3) | Нравится`,
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
    expect(wrapper.findAll(".caption-action")).toHaveLength(1);
    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3) | Редактировать`,
    );
  });
});

describe("TopicCard: the like", () => {
  it("says the likes of one's own text and does not press", () => {
    const wrapper = render({
      canLike: false,
      isLikedByMe: false,
      likes: [liker("Player-Two"), liker("TestUser"), liker("TestHonorary")],
    });

    expect(wrapper.find("button.like-badge").exists()).toBe(false);
    expect(wrapper.get(".like-badge").text()).toBe("Нравится (3)");
  });

  it("shows no Нравится where there is nothing to press and nobody to list", () => {
    const wrapper = render({ canLike: false, likes: [] });

    expect(wrapper.find(".like-badge").exists()).toBe(false);
    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | Комментарии (3)`,
    );
  });

  it("presses Нравится for the viewer who likes it", () => {
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

    const footer = wrapper.get(".topic-footer").element;
    expect(footer.children).toHaveLength(1);
    expect(footer.children[0].matches(".caption-line")).toBe(true);
  });

  it("draws every part a moderator has, with no Еще and the name whole", () => {
    const wrapper = render(moderator);

    expect(copied(wrapper)).toBe(
      `Автор: SolohinLex [M] [offline], ${date} | ` +
        `Отредактировано ${copiedDate(MODIFIED)} | Комментарии (3) | ` +
        "Редактировать | Удалить | Предупреждение | Нравится (2)",
    );
  });

  it("ends the author's part with the comma and keeps the date and its time one part", () => {
    const wrapper = render({});
    const [who, when] = parts(wrapper);

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

describe("TopicCard: the text", () => {
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

// The role badge in the card caption: its nodes, their styles, its tooltip.
describe("TopicCard role badge", () => {
  it("draws the brackets as the line's text around the tooltip", () => {
    const wrapper = render({ author: { ...author, role: UserRole.Admin } });

    expect(markupOutline(wrapper, ".topic-footer .caption-line__part")).toEqual(
      [
        "span.caption-line__part { display: inline-block; max-width: 100% }",
        "  span.caption-part",
        '    "Автор: "',
        "    a.author-link { color: var(--link) }",
        '      "SolohinLex"',
        ...captionBadgeOutline("A", 2),
        '    " ["',
        "    span.offline { color: var(--text-muted) }",
        '      "offline"',
        '    "]"',
        '  span.caption-line__mark[aria-hidden="true"]',
        '    ","',
      ],
    );
    expect(
      wrapper.findAllComponents(Tooltip).map((tip) => tip.props("text")),
    ).toEqual(["Администратор"]);
  });
});
