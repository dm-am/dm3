/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { enableAutoUnmount, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia } from "pinia";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { markupOutline } from "@/shared/lib/testing/markupOutline";
import { Tooltip } from "@/shared/ui/Tooltip";
import CommentItem from "./CommentItem.vue";
import {
  RouterLinkStub,
  comment,
  freshComment,
  mountComponent,
  signInAs,
} from "./CommentItem.fixtures";

vi.mock("vue-router", async () =>
  (await import("./CommentItem.doubles")).vueRouter(),
);

enableAutoUnmount(afterEach);

beforeEach(freshComment);

describe("CommentItem author", () => {
  const nameIn = (wrapper: VueWrapper) => wrapper.get(".comment-header a");

  it("names a removed account in the full layout the way it names an author", () => {
    const living = nameIn(mountComponent(vi.fn(), {}, { compact: false }));
    const removed = nameIn(
      mountComponent(vi.fn(), { author: null as never }, { compact: false }),
    );

    expect(living.text()).toBe("SolohinLex");
    expect(removed.text()).toBe("Аккаунт удален");
    expect(removed.classes()).toEqual(living.classes());
    expect(removed.classes()).toContain("author-name");
  });
});

// jsdom lays nothing out: what is read is the markup the line wraps by.
describe("CommentItem at every width", () => {
  const CREATED = "2026-09-09T06:33:00Z";
  const EDITED = "2026-09-09T08:06:00Z";
  const spaced = (text: string) => text.replace(/\s+/g, " ").trim();

  const moderatorsComment = (compact: boolean) => {
    signInAs("Senior", "SeniorModerator");
    return mountComponent(
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
      { compact, number: 7 },
    );
  };

  it("keeps the name, the letter of the role and [offline] in the header, a space of the line apart", () => {
    const wrapper = moderatorsComment(false);
    const line = wrapper.get(".comment-header .author-line");

    expect(
      [...line.element.children].map((child) => [
        child.className,
        child.className === "author-gap"
          ? child.textContent
          : spaced(child.textContent ?? ""),
      ]),
    ).toEqual([
      ["author-name", "TestMentor"],
      ["author-gap", " "],
      ["author-role", "[M]"],
      ["author-gap", " "],
      ["author-presence", "[offline]"],
    ]);
    expect(line.text()).toBe("TestMentor [M] [offline]");
    expect(line.find(".copy-space").exists()).toBe(false);
  });

  it("names a removed account in the header without a mark of presence", () => {
    const wrapper = mountComponent(
      vi.fn(),
      { author: null as never },
      { compact: false },
    );

    const line = wrapper.get(".comment-header .author-line");
    expect([...line.element.children].map((child) => child.className)).toEqual([
      "author-name",
    ]);
    expect(line.text()).toBe("Аккаунт удален");
  });

  it("writes the date of the author's column with its time in one text", () => {
    const wrapper = moderatorsComment(false);
    const date = wrapper.get(".comment-header .comment-date");

    expect(spaced(date.text())).toBe(spaced(formatDateFull(CREATED)));
    expect(date.element.children.length).toBe(0);
  });

  it("writes the date of the compact caption with its time in one part", () => {
    const wrapper = moderatorsComment(true);
    const date = wrapper
      .findAll(".comment-footer .caption-line__part")
      .find(
        (part) =>
          /\d{2}\.\d{2}\.\d{4}/.test(part.text()) && !/Автор/.test(part.text()),
      )!;

    expect(spaced(date.text())).toBe(spaced(formatDateFull(CREATED)));
    expect(date.element.firstElementChild?.children.length).toBe(0);
  });
});

describe("CommentItem of a guest", () => {
  const guests = (compact: boolean) =>
    mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: RouterLinkStub },
      },
      props: {
        comment: comment({ author: null as never }),
        readonly: true,
        guest: true,
        compact,
      },
    });

  it("signs the header with the word, in the name's place and weight", () => {
    const wrapper = guests(false);
    const line = wrapper.get(".comment-header .author-line");

    expect(line.text()).toBe("Гость");
    expect(
      [...line.element.children].map((child) => [
        child.tagName,
        child.className,
      ]),
    ).toEqual([["SPAN", "author-name"]]);
  });

  it("leads nowhere from the picture or the word", () => {
    const wrapper = guests(false);

    expect(wrapper.findAllComponents(RouterLinkStub)).toEqual([]);
    expect(wrapper.get(".avatar-link").element.tagName).toBe("SPAN");
    expect(
      wrapper.get(".avatar-link .default-avatar").attributes("aria-label"),
    ).toBe("Гость");
  });

  it("signs the compact caption with the word", () => {
    const wrapper = guests(true);

    expect(
      wrapper.get(".comment-footer .author-info").text().replace(/\s+/g, " "),
    ).toBe("Автор: Гость");
    expect(wrapper.findAllComponents(RouterLinkStub)).toEqual([]);
  });

  it("is a removed account where the comment does not say it is a guest's", () => {
    const wrapper = mount(CommentItem, {
      global: {
        plugins: [createPinia()],
        stubs: { RouterLink: RouterLinkStub },
      },
      props: {
        comment: comment({ author: null as never }),
        readonly: true,
        compact: false,
      },
    });

    expect(wrapper.get(".comment-header .author-line").text()).toBe(
      "Аккаунт удален",
    );
  });
});

// The role badge of the author column: its nodes, their styles, its tooltip.
describe("CommentAuthor role badge", () => {
  it("draws the brackets inside the letter, under its tooltip", () => {
    const senior = { username: "Старший", role: "SeniorModerator" };
    const wrapper = mountComponent(
      vi.fn(),
      { author: { ...senior, lastActivityUtc: null } as never },
      { compact: false },
    );

    expect(markupOutline(wrapper, ".author-line")).toEqual([
      "span.author-line { display: block; padding-bottom: 4px }",
      "  a.author-name { color: var(--text); display: inline-block; font-size: 16px; font-weight: 700; max-width: 100%; text-decoration: none; transition: color 0.15s ease }",
      '    "Старший"',
      "  span.author-gap { word-spacing: calc(2px - 0.2676em) }",
      '    " "',
      "  span.author-role { display: inline-block; max-width: 100% }",
      "    span.tooltip-trigger { display: inline }",
      "      b.role-letter { color: var(--accent-green); cursor: help; font-weight: bold }",
      '        "[M]"',
      "    <!--teleport start-->",
      "    <!--teleport end-->",
      "  span.author-gap { word-spacing: calc(2px - 0.2676em) }",
      '    " "',
      "  span.author-presence { display: inline-block; max-width: 100% }",
      '    "["',
      "    span.offline { color: var(--text-muted); padding: 0 2px }",
      '      "offline"',
      '    "]"',
    ]);
    expect(
      wrapper.findAllComponents(Tooltip).map((tip) => tip.props("text")),
    ).toEqual(["Старший модератор", ""]);
  });
});
