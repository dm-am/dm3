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
import {
  componentCss,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";

const HERE = dirname(fileURLToPath(import.meta.url));
const PAGE = join(HERE, "ProfilePage.vue");
/** The "Модерация" tab, a component of its own. */
const TAB = join(HERE, "moderation", "ProfileModerationTab.vue");

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

const template = (file = PAGE): Node => {
  const { descriptor } = parseSfc(readFileSync(file, "utf8"), {
    filename: file,
  });
  const ast = descriptor.template?.ast as unknown as Node | undefined;
  if (!ast) throw new Error(`${file} has no template`);
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

  /**
   * Moderation is a tab of the profile, the last of the strip, and not a red
   * bar of its own between the actions and the contacts: the bar pushed the
   * profile down by a screen and a half when it was opened. The tab draws only
   * over the moderated profile the server served, so a viewer it was not
   * served to has nothing to open.
   */
  it("keeps moderation in a tab of its own, and no bar above the contacts", () => {
    const panel = elements(root).find((n) => className(n) === "tab-content");
    const tab = (panel?.children ?? []).find(
      (c) => c.type === ELEMENT && c.tag === "ProfileModerationTab",
    );
    expect(tab, "the moderation tab has no branch in the panel").toBeTruthy();
    expect(condition(tab!)).toBe(
      "activeTab === 'moderation' && moderatedProfile",
    );

    expect(elements(root).map(className)).not.toContain("mod-bar");
    expect(elements(root).map((n) => n.tag)).not.toContain("ModerationWatch");
  });

  // The moderation watch decides the premoderation status of every game and
  // blog this user creates next, and the moderation tab is the only surface
  // that carries both the flag and the permission to change it. The block
  // gates itself on that permission, so the tab hands it the whole permission
  // set rather than deciding for it.
  it("carries the moderation watch inside the moderation tab", () => {
    const tab = elements(template(TAB)).find(
      (n) => n.tag === "ModerationWatch",
    );
    expect(tab, "ModerationWatch is not in the moderation tab").toBeTruthy();

    const bound = (tab?.props ?? [])
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

/**
 * The band under the page and the bar of the edit mode, the same at every
 * width as on the full version: the profile keeps 80 under itself and a tab at
 * least 100 tall, and the bar of the edit mode stands sticky in the flow under
 * the last field, its buttons going down a line only where the column has no
 * room for them beside the words.
 *
 * Read off the compiled stylesheet: a document that lays nothing out has no
 * band to measure.
 */
describe("ProfilePage band under the page", () => {
  /** The widest window a rule applies in: its @media (max-width), or none. */
  const rules = (css: string) =>
    [
      ...css.matchAll(
        /@media \(max-width: (\d+)px\)\s*\{((?:[^{}]*\{[^{}]*\})*)\s*\}|([^{}@]+\{[^{}]*\})/g,
      ),
    ].flatMap(([, width, media, plain]) =>
      styleRules(media ?? plain).map((rule) => ({
        upTo: width ? Number(width) : Infinity,
        ...rule,
      })),
    );

  const sheet = rules(componentCss(PAGE));

  /** What one selector declares in windows up to `width`, merged in order. */
  const declaredAt = (selector: string, width: number): Declarations =>
    Object.assign(
      {},
      ...sheet
        .filter((rule) => rule.selector === selector && width <= rule.upTo)
        .map((rule) => rule.declarations),
    ) as Declarations;

  it("keeps the band of the full version at every width", () => {
    for (const width of [360, 390, 430, 680, 768, 1000, 1001, 1366, 1920]) {
      expect(
        declaredAt(".profile-page", width)["padding-bottom"],
        `${width}`,
      ).toBe("80px");
      expect(declaredAt(".tab-content", width)["min-height"], `${width}`).toBe(
        "100px",
      );
    }
  });

  it("keeps the bar of the edit mode in the flow, the same at every width", () => {
    for (const width of [360, 680, 1024, 1366]) {
      const bar = declaredAt(".save-bar", width);
      expect(bar.position, `${width}`).toBe("sticky");
      expect(bar["flex-wrap"], `${width}`).toBe("wrap");
    }
  });
});
