/**
 * @vitest-environment node
 */

/**
 * Where the event's action is drawn, and where it may not be.
 *
 * The strip above the feed is a summary: one line, the same line for every
 * reader, and the first thing a narrow screen takes away is everything except
 * the title. An action item breaks both halves of that. Its label depends on
 * who is looking, so the line stops being one line the room shares — and the
 * label cannot even be chosen from what the strip is drawn with, because the
 * list endpoint sends a summary with no participants in it. "Участвовать" and
 * "Отменить участие" are indistinguishable until the details arrive, and they
 * arrive with the card.
 *
 * So the control sits in the card. That is a fact about placement rather than
 * about pixels, so it is checked where it can be undone: in the template.
 * onelineCopy.spec.ts holds the other half — that the strip still copies as one
 * string — but it can only see parts that put characters on the screen, and an
 * icon-only control in the strip would slip past it.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse } from "vue/compiler-sfc";

const HERE = dirname(fileURLToPath(import.meta.url));

type Prop = { type: number; name?: string; value?: { content?: string } };
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

const hasClass = (node: Node, name: string): boolean =>
  (
    node.props?.find((p) => p.type === 6 && p.name === "class")?.value
      ?.content ?? ""
  )
    .split(/\s+/)
    .includes(name);

const ACTION = "ChatEventActions";

const panel = elements("ChatEventsPanel.vue");
const inside = (name: string) =>
  panel.filter(
    (e) => hasClass(e.node, name) || e.ancestors.some((a) => hasClass(a, name)),
  );

describe("the action of a chat event", () => {
  it("reads the panel it is supposed to read", () => {
    expect(panel.length).toBeGreaterThan(10);
    // The two places the check is about: the strip's run and the card.
    expect(inside("row-main").length).toBeGreaterThan(1);
    expect(inside("event-overlay").length).toBeGreaterThan(1);
  });

  it("stands in the card, where the participants list lands", () => {
    const drawn = panel.filter((e) => e.node.tag === ACTION);
    expect(drawn).toHaveLength(1);
    expect(
      drawn[0].ancestors.some((a) => hasClass(a, "event-overlay")),
      "the action belongs to the card that carries the participants",
    ).toBe(true);
  });

  it("stays out of the strip, which is one line for every reader", () => {
    expect(inside("row-main").map((e) => e.node.tag)).not.toContain(ACTION);
  });
});
