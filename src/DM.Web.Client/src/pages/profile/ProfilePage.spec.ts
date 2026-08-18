/**
 * @vitest-environment node
 */

/**
 * Two rules the owner set for the profile tabs, and nothing held either.
 *
 * Order: the category's own listing comes first, the "best of" spotlight
 * after it. The panels were built the other way round, so the Games tab
 * opened on a single post and the Blogs tab on a single publication, while
 * the table the tab is named for waited below them.
 *
 * Headings: a section heading that repeats the active tab ("Игры
 * пользователя" under the "Игры" tab) says nothing the tab strip has not
 * said already. The spotlights keep theirs — "Лучший игровой пост" is not
 * the name of the tab.
 *
 * The check reads ProfilePage's template through the SFC parser instead of
 * mounting the page: both rules are about static markup, while reaching the
 * panel at runtime takes a loaded profile, four stores and six requests.
 * Same tree walk as accessibleNames.spec.ts.
 */
import { describe, expect, it } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

const PAGE = join(dirname(fileURLToPath(import.meta.url)), "ProfilePage.vue");

/** @vue/compiler-core NodeTypes — the members this walk reads. */
const ELEMENT = 1;
const TEXT = 2;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** A literal attribute, or a directive with an expression. */
interface Prop {
  type: number;
  name: string;
  /** ATTRIBUTE only. */
  value?: { content: string };
  /** DIRECTIVE only. */
  exp?: { content: string };
  /** DIRECTIVE only: the bound name, e.g. `permissions` in `:permissions`. */
  arg?: { content: string };
}

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  /** ELEMENT only. */
  tag?: string;
  /** TEXT only. */
  content?: string;
  props?: Prop[];
  children?: Node[];
}

const template = (): Node => {
  const { descriptor } = parseSfc(readFileSync(PAGE, "utf8"), {
    filename: PAGE,
  });
  const ast = descriptor.template?.ast as unknown as Node | undefined;
  if (!ast) throw new Error("ProfilePage.vue has no template");
  return ast;
};

const elements = (node: Node, out: Node[] = []): Node[] => {
  if (node.type === ELEMENT) out.push(node);
  for (const child of node.children ?? []) elements(child, out);
  return out;
};

/** The static `class` attribute, "" when the element carries none. */
const className = (node: Node): string =>
  node.props?.find((p) => p.type === ATTRIBUTE && p.name === "class")?.value
    ?.content ?? "";

/** The `v-if` / `v-else-if` expression, "" when the element has neither. */
const condition = (node: Node): string =>
  node.props?.find(
    (p) => p.type === DIRECTIVE && (p.name === "if" || p.name === "else-if"),
  )?.exp?.content ?? "";

/** Static text of a node; interpolations are not read. */
const staticText = (node: Node): string =>
  (node.children ?? [])
    .filter((c) => c.type === TEXT)
    .map((c) => c.content ?? "")
    .join("")
    .trim();

/** The `<template v-else-if="activeTab === '…'">` body of one tab. */
const tabBody = (root: Node, tab: string): Node => {
  const panel = elements(root).find((n) => className(n) === "tab-content");
  if (!panel) throw new Error("the tab panel (.tab-content) is gone");
  const body = (panel.children ?? []).find(
    (c) => c.type === ELEMENT && condition(c) === `activeTab === '${tab}'`,
  );
  if (!body) throw new Error(`the "${tab}" tab has no branch in the panel`);
  return body;
};

/** The branch's own element children in source order, sections by class. */
const sections = (body: Node): string[] =>
  (body.children ?? [])
    .filter((c) => c.type === ELEMENT)
    .map((c) =>
      c.tag === "section" ? `section.${className(c)}` : (c.tag ?? ""),
    );

describe("ProfilePage tab panels", () => {
  const root = template();

  it("opens the Games tab with the games table, spotlight below it", () => {
    expect(sections(tabBody(root, "games"))).toEqual([
      "ProfileGamesTable",
      "section.featured-section",
      "ProfileSubscribersSection",
    ]);
  });

  it("opens the Blogs tab with the blogs table, spotlight below it", () => {
    expect(sections(tabBody(root, "blogs"))).toEqual([
      "ProfileBlogsTable",
      "section.featured-section",
      "ProfileSubscribersSection",
    ]);
  });

  it("gives the Topics tab its list with no wrapper of its own", () => {
    expect(sections(tabBody(root, "topics"))).toEqual([
      "ProfileTopicsList",
      "ProfileSubscribersSection",
    ]);
  });

  // The moderation watch decides the premoderation status of every game and
  // blog this user creates next, and the profile's moderation panel is the only
  // surface that carries both the flag and the permission to change it. The
  // block gates itself on that permission, so the page hands it the whole
  // permission set rather than deciding for it.
  it("carries the moderation watch inside the moderation panel", () => {
    const body = elements(root).find((n) => className(n) === "mod-body");
    if (!body) throw new Error("the moderation panel body (.mod-body) is gone");

    const watch = (body.children ?? []).find(
      (c) => c.type === ELEMENT && c.tag === "ModerationWatch",
    );
    expect(
      watch,
      "ModerationWatch is not in the moderation panel",
    ).toBeTruthy();

    const bound = (watch?.props ?? [])
      .filter((p) => p.type === DIRECTIVE && p.name === "bind")
      .map((p) => p.arg?.content);
    expect(bound).toEqual(["under-watch", "permissions", "target-username"]);
  });

  it("heads no section with the name of the tab it sits in", () => {
    const headings = elements(root)
      .filter((n) => n.tag === "BlockTitle")
      .map(staticText);

    expect(headings).not.toContain("Игры пользователя");
    expect(headings).not.toContain("Блоги пользователя");
    expect(headings).not.toContain("Топики пользователя");
    // A spotlight is not the tab it sits in, and keeps its heading.
    expect(headings).toContain("Лучший игровой пост");
    expect(headings).toContain("Самая популярная публикация");
  });
});
