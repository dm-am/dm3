/**
 * @vitest-environment node
 */

/**
 * The chat's search and its archive-date control used to live inside the chat:
 * the search was an absolutely positioned panel filling the chat frame and
 * covering the very feed it searched, and both were opened from text links in
 * the events strip, which turned an informational readout into a control bar
 * and made "Поиск | К дате" a composite that copied broken.
 *
 * They stand above the frame now, on one search row, and the results lay over
 * the feed from there. That is a fact about where the elements are, so it is
 * checked where it can be broken: in the page's own template.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse } from "vue/compiler-sfc";

const HERE = dirname(fileURLToPath(import.meta.url));

type Prop = {
  type: number;
  name?: string;
  arg?: { content?: string };
  value?: { content?: string };
};
type Node = { type: number; tag?: string; props?: Prop[]; children?: Node[] };
type Placed = { node: Node; ancestors: Node[] };

/** Every element of an SFC template, each with the elements it sits inside. */
function elements(file: string): Placed[] {
  const raw = readFileSync(join(HERE, file), "utf8");
  const { descriptor } = parse(raw, { filename: file });
  const found: Placed[] = [];
  const visit = (node: Node, ancestors: Node[]): void => {
    for (const child of node.children ?? []) {
      if (child.type !== 1) continue;
      found.push({ node: child, ancestors });
      visit(child, [...ancestors, child]);
    }
  };
  const root = descriptor.template?.ast as Node | undefined;
  if (root) visit(root, []);
  return found;
}

const classOf = (node: Node): string =>
  node.props?.find((p) => p.type === 6 && p.name === "class")?.value?.content ??
  "";

const hasClass = (node: Node, name: string): boolean =>
  classOf(node).split(/\s+/).includes(name);

/** Attribute names and directive arguments, as the template spells them. */
const bindingsOf = (node: Node): string[] =>
  (node.props ?? []).map((p) =>
    p.type === 6 ? (p.name ?? "") : (p.arg?.content ?? p.name ?? ""),
  );

const page = elements("GlobalChatPage.vue");
const find = (tag: string) => page.find((e) => e.node.tag === tag);

describe("the chat's search stands above the chat", () => {
  it("reads the page it is supposed to read", () => {
    expect(page.length).toBeGreaterThan(20);
    expect(page.some((e) => hasClass(e.node, "globalChat-container"))).toBe(
      true,
    );
  });

  it("keeps the search row outside the chat frame", () => {
    const bar = find("MessageSearchBar");
    expect(bar, "the page renders MessageSearchBar").toBeDefined();
    expect(
      bar!.ancestors.some((a) => hasClass(a, "globalChat-container")),
    ).toBe(false);
  });

  it("carries the date control on that row, spelled in full", () => {
    const jump = find("ChatDateJump");
    expect(jump, "the page renders ChatDateJump").toBeDefined();
    expect(jump!.ancestors.some((a) => a.tag === "MessageSearchBar")).toBe(
      true,
    );
    expect(readFileSync(join(HERE, "ChatDateJump.vue"), "utf8")).toContain(
      "Перейти к дате",
    );
  });

  it("leaves the events strip informational, with no controls of its own", () => {
    const strip = find("ChatEventsPanel");
    expect(strip, "the page renders ChatEventsPanel").toBeDefined();
    expect(bindingsOf(strip!.node)).toEqual([]);

    const stripControls = elements("ChatEventsPanel.vue")
      .map((e) => classOf(e.node))
      .filter((name) => /search-toggle|calendar-toggle|panel-link/.test(name));
    expect(stripControls).toEqual([]);
  });
});
