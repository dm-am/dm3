/**
 * @vitest-environment node
 */

/**
 * The compact layout used to start each row 46px in from the left edge while
 * the full layout starts its avatar at zero: the 80px time gutter was
 * right-aligned and led with a 16px icon slot, so everything in it was pressed
 * against the right edge and the hole stayed on the left, in the chat, in the
 * messenger and in game rooms alike.
 *
 * The cure is one order and one alignment: the time comes first in its group
 * everywhere — as it always did in the full layout — and the compact gutter is
 * left-aligned, which puts the time on the exact x where the full layout puts
 * the avatar. The gutter stays 80px and the content margin stays 88px, so the
 * column the two layouts share does not move.
 *
 * Read from the source, not from a rendering: the two halves live in a
 * template and in a scoped style block, and no single rendered surface shows
 * both at once.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse } from "vue/compiler-sfc";

const HERE = dirname(fileURLToPath(import.meta.url));
const FILE = join(HERE, "ChatMessage.vue");

type Node = {
  type: number;
  tag?: string;
  props?: { type: number; name?: string; value?: { content?: string } }[];
  children?: Node[];
};

const source = readFileSync(FILE, "utf8");
const { descriptor } = parse(source, { filename: "ChatMessage.vue" });

const classOf = (node: Node): string =>
  node.props?.find((p) => p.type === 6 && p.name === "class")?.value?.content ??
  "";

const hasClass = (node: Node, name: string): boolean =>
  classOf(node).split(/\s+/).includes(name);

function allElements(root: Node | undefined): Node[] {
  const found: Node[] = [];
  const visit = (node: Node): void => {
    for (const child of node.children ?? []) {
      if (child.type !== 1) continue;
      found.push(child);
      visit(child);
    }
  };
  if (root) visit(root);
  return found;
}

/**
 * One rule of an indented stylesheet: every line indented under the selector,
 * up to the next line that starts at column zero.
 */
function ruleBody(style: string, selector: string): string {
  const lines = style.split("\n");
  const start = lines.findIndex((line) => line.trim() === selector);
  if (start < 0) return "";
  const body: string[] = [];
  for (let i = start + 1; i < lines.length; i++) {
    if (lines[i].trim() === "") continue;
    if (!/^\s/.test(lines[i])) break;
    body.push(lines[i]);
  }
  return body.join("\n");
}

const timeGroups = allElements(descriptor.template?.ast as Node | undefined)
  .filter((node) => hasClass(node, "msg-time-group"))
  .map((node) => (node.children ?? []).filter((child) => child.type === 1));

const compact = ruleBody(
  descriptor.styles[0]?.content ?? "",
  ".chat-message.compact",
);

describe("the compact chat row starts where the full row starts", () => {
  it("reads the layouts it is supposed to read", () => {
    // Deleted, deleted-expanded, continuation and normal, compact and full.
    expect(timeGroups.length).toBeGreaterThan(3);
    expect(compact).toContain(".msg-time-group");
  });

  it("leads every time group with the time itself", () => {
    const leaders = timeGroups.map((children) => classOf(children[0]));
    expect(leaders.filter((name) => name !== "msg-time")).toEqual([]);
  });

  it("aligns the compact gutter to the left, never to the right", () => {
    expect(compact).toMatch(/text-align:\s*left/);
    expect(compact).not.toMatch(/text-align:\s*right/);
  });
});
