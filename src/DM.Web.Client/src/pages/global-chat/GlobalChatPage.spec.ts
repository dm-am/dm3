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
 * the feed from there. The row is one part of the feed's widget
 * (ChatSearchRow). That is a fact about where the elements are, so it is
 * checked where it can be broken: in the templates.
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

/**
 * The pages that stand under the search row. A correspondence of the
 * messenger stood under it too from 5 to 12 October 2026, and is back to its
 * own look without it ("верни личные сообщения как было").
 */
const PAGES = ["GlobalChatPage.vue"];

/** The row. */
const ROW = "../../widgets/chat-feed/ChatSearchRow.vue";
const row = elements(ROW);

/** The feed the global chat is drawn with. */
const FEED = "../../widgets/chat-feed/ChatFeed.vue";
const feed = elements(FEED);

describe("the chat's search stands above the chat", () => {
  it("reads the page it is supposed to read", () => {
    expect(page.length).toBeGreaterThan(5);
    expect(find("ChatFeed"), "the page renders ChatFeed").toBeDefined();
    expect(feed.some((e) => hasClass(e.node, "chat-feed__frame"))).toBe(true);
  });

  it.each(PAGES)("%s keeps the search row outside the chat frame", (file) => {
    const placed = elements(file).filter((e) => e.node.tag === "ChatSearchRow");
    expect(placed, "the page renders one ChatSearchRow").toHaveLength(1);
    expect(placed[0].ancestors.some((a) => a.tag === "ChatFeed")).toBe(false);
  });

  it("puts the search and the date control on one row", () => {
    const bar = row.find((e) => e.node.tag === "MessageSearchBar");
    expect(bar, "the row renders MessageSearchBar").toBeDefined();
    const jump = row.find((e) => e.node.tag === "ChatDateJump");
    expect(jump, "the row renders ChatDateJump").toBeDefined();
    expect(jump!.ancestors.some((a) => a.tag === "MessageSearchBar")).toBe(
      true,
    );
  });

  it("spells the date control in full where the row has room", () => {
    const control = readFileSync(
      join(HERE, "../../features/chat-date-jump/ui/ChatDateJump.vue"),
      "utf8",
    );
    expect(control).toContain("Перейти к дате");
  });

  it("leaves the events strip informational, with no controls of its own", () => {
    const strip = find("ChatEventsPanel");
    expect(strip, "the page renders ChatEventsPanel").toBeDefined();
    expect(bindingsOf(strip!.node)).toEqual([]);
    // In the frame, at its top: the strip slot of the feed.
    expect(strip!.ancestors.some((a) => a.tag === "ChatFeed")).toBe(true);

    const stripControls = elements("ChatEventsPanel.vue")
      .map((e) => classOf(e.node))
      .filter((name) => /search-toggle|calendar-toggle|panel-link/.test(name));
    expect(stripControls).toEqual([]);
  });
});

/**
 * The failed-load state, checked in the same place and for the same kind of
 * reason: what makes an error look like an error here is which element the
 * feed reaches for. It used to reach for the empty state's own class, which
 * carries neither a background nor a colour, so a dead feed and a refused one
 * were the same picture. The page hands the failure and its retry to the
 * feed; the feed draws the shared red box.
 */
describe("a failed load reads as a failure, not as emptiness", () => {
  it("hands the failure and its retry to the feed", () => {
    const bindings = bindingsOf(find("ChatFeed")!.node);
    expect(bindings).toContain("error");
    expect(bindings).toContain("retry");
  });

  it("draws it in the shared error box", () => {
    const box = feed.find((e) => e.node.tag === "ErrorState");
    expect(box, "the feed renders ErrorState").toBeDefined();
    expect(bindingsOf(box!.node)).toEqual(["else-if", "message", "retry"]);
  });

  it("leaves the empty-state class to the empty state alone", () => {
    const empties = feed.filter((e) => hasClass(e.node, "chat-feed__empty"));
    expect(empties).toHaveLength(1);
    expect(empties[0].node.tag).toBe("secondary-text");
  });
});
