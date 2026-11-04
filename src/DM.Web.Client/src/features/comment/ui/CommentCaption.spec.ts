/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, type VueWrapper } from "@vue/test-utils";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  captionBadgeOutline,
  markupOutline,
} from "@/shared/lib/testing/markupOutline";
import { Tooltip } from "@/shared/ui/Tooltip";
import {
  LIKE_BUTTON,
  freshComment,
  mountComponent,
  signInAs,
} from "./CommentItem.fixtures";

vi.mock("vue-router", async () =>
  (await import("./CommentItem.doubles")).vueRouter(),
);

enableAutoUnmount(afterEach);

beforeEach(freshComment);

describe("CommentItem caption line", () => {
  const CREATED = "2026-09-09T06:33:00Z";
  const EDITED = "2026-09-09T08:06:00Z";

  // The no-break space a date keeps after "в" is text and stays.
  const collapse = (text: string) => text.replace(/[^\S\u00a0]+/g, " ");

  const parts = (line: Element | null) => {
    if (!line) throw new Error("no caption line");
    return [...line.querySelectorAll(":scope > .caption-line__part")].map(
      (part) =>
        collapse(part.textContent ?? "")
          .replace(/ \|$/, "")
          .trim(),
    );
  };

  const footerLine = (wrapper: VueWrapper) =>
    wrapper.element.querySelector(".comment-footer .caption-line");

  const moderatorsComment = (extra: Record<string, unknown> = {}) =>
    mountComponent(
      vi.fn(),
      {
        createdUtc: CREATED as never,
        modifiedUtc: EDITED as never,
        author: {
          username: "TestMentor",
          role: "Moderator",
          lastActivityUtc: null,
        } as never,
        likes: [{ id: "u-1", username: "Other" }] as never,
      },
      { number: 7, ...extra },
    );

  // A warning reaches only below the reader's rank, hence a senior moderator.
  it("runs the author, the dates, the actions and the like, in that order", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = moderatorsComment();

    expect(parts(footerLine(wrapper))).toEqual([
      // The comma lets a narrow column break the line after the author.
      "Автор: TestMentor [M] [offline],",
      formatDateFull(CREATED),
      `Отредактировано ${formatDateFull(EDITED)}`,
      "Редактировать",
      "Удалить",
      "Предупреждение",
      "Нравится (1)",
    ]);
  });

  it("puts the like right after the date when the viewer has no action", () => {
    signInAs("Reader");
    const wrapper = mountComponent(vi.fn(), {
      createdUtc: CREATED as never,
      likes: [{ id: "u-1", username: "Other" }] as never,
    });

    expect(parts(footerLine(wrapper))).toEqual([
      "Автор: SolohinLex [offline],",
      formatDateFull(CREATED),
      "Нравится (1)",
    ]);
  });

  it("keeps the number out of the line, in its own corner", () => {
    signInAs("Mod", "Moderator");
    const wrapper = moderatorsComment();

    const number = wrapper.get(".comment-number").element;
    expect(number.closest(".caption-line")).toBeNull();
    expect(number.closest(".comment-footer")).not.toBeNull();
  });

  it("holds the edit and the actions under the text in the full layout, as a post does", () => {
    signInAs("Senior", "SeniorModerator");
    const wrapper = moderatorsComment({ compact: false });

    const line = wrapper.get(".comment-body > .comment-edit-line");
    expect(parts(line.element.querySelector(".caption-line"))).toEqual([
      `Отредактировано ${formatDateFull(EDITED)}`,
      "Редактировать",
      "Удалить",
      "Предупреждение",
    ]);
    expect(wrapper.find(".comment-footer .caption-line").exists()).toBe(false);
    expect(wrapper.get(".comment-footer").text()).toBe("7");
  });

  it("stands the name, the avatar, the date and the like in the author's column, in that order", () => {
    signInAs("Mod", "Moderator");
    const wrapper = moderatorsComment({ compact: false });

    const column = wrapper.get(".comment-header .author-inner");
    expect(
      [...column.element.children].map((child) => child.className),
    ).toEqual(["author-line", "avatar-link", "comment-date", "comment-like"]);
    expect(column.get(".comment-date").text()).toBe(formatDateFull(CREATED));
    expect(column.find(".comment-like .like-badge").exists()).toBe(true);
    expect(column.text()).not.toContain("Отредактировано");
  });

  it("draws no edit line where there is nothing to say", () => {
    localStorage.clear();
    const wrapper = mountComponent(vi.fn(), {}, { compact: false, number: 3 });

    expect(wrapper.find(".comment-edit-line").exists()).toBe(false);
  });

  it("draws the like for a guest without letting them press it", () => {
    localStorage.clear();
    const wrapper = mountComponent(vi.fn(), {
      likes: [{ id: "u-1", username: "Other" }] as never,
    });

    expect(wrapper.find(LIKE_BUTTON).exists()).toBe(false);
    expect(wrapper.find(".like-badge").exists()).toBe(true);
    expect(parts(footerLine(wrapper)).at(-1)).toBe("Нравится (1)");
  });
});

// The role badge in the caption line: its nodes, their styles, its tooltip.
describe("CommentCaption role badge", () => {
  it("draws the brackets as the line's text around the tooltip", () => {
    const online = { lastActivityUtc: new Date().toISOString() };
    const wrapper = mountComponent(vi.fn(), {
      author: { username: "Модератор", role: "Moderator", ...online } as never,
    });

    expect(markupOutline(wrapper, ".author-info")).toEqual([
      "span.caption-part.author-info",
      '  "Автор: "',
      "  a.author-link { color: var(--link); text-decoration: none; transition: color 0.15s ease }",
      '    "Модератор"',
      ...captionBadgeOutline("M", 1),
      '  " ["',
      "  span.online { color: var(--accent-green) }",
      '    "online"',
      '  "]"',
    ]);
    expect(
      wrapper.findAllComponents(Tooltip).map((tip) => tip.props("text")),
    ).toEqual(["Модератор", ""]);
  });
});
