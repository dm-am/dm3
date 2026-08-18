/**
 * @vitest-environment node
 */

/**
 * "Написать рекомендацию" in the actions row of somebody else's profile.
 *
 * Two things are gated here. It stands beside "Написать сообщение", in the
 * branch that only a signed-in reader of another person's profile sees — the
 * row is where a reader looks for what can be done about this participant.
 * And it is drawn on `canEndorse` and nothing else: that flag is the server's
 * own answer (useEndorsementEligibility, tested at runtime next door), because
 * three of the five conditions the POST enforces — probation, a shared game, a
 * recommendation already written for the pair — cannot be known here. A button
 * offered on a locally guessed condition is the site promising more than the
 * server allows.
 *
 * Read out of ProfilePage's template through the SFC parser rather than by
 * mounting the page: reaching the row at runtime takes a loaded profile, four
 * stores and six requests, and the rule is about static markup. Same tree walk
 * as ProfilePage.spec.ts and ProfileGameReviews.spec.ts.
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

interface Prop {
  type: number;
  name: string;
  value?: { content: string };
  arg?: { content: string };
  exp?: { content: string };
}

interface Node {
  type: number;
  tag?: string;
  content?: string;
  props?: Prop[];
  children?: Node[];
}

const source = readFileSync(PAGE, "utf8");

const template = (): Node => {
  const { descriptor } = parseSfc(source, { filename: PAGE });
  const ast = descriptor.template?.ast as unknown as Node | undefined;
  if (!ast) throw new Error("ProfilePage.vue has no template");
  return ast;
};

const elements = (node: Node, out: Node[] = []): Node[] => {
  if (node.type === ELEMENT) out.push(node);
  for (const child of node.children ?? []) elements(child, out);
  return out;
};

const className = (node: Node): string =>
  node.props?.find((p) => p.type === ATTRIBUTE && p.name === "class")?.value
    ?.content ?? "";

const directive = (node: Node, name: string, arg?: string): string | null => {
  const found = node.props?.find(
    (p) =>
      p.type === DIRECTIVE &&
      p.name === name &&
      (arg === undefined || p.arg?.content === arg),
  );
  return found ? (found.exp?.content ?? "") : null;
};

/** All text inside the node, interpolations excluded. */
const deepText = (node: Node): string =>
  (node.children ?? [])
    .map((c) => (c.type === TEXT ? (c.content ?? "") : deepText(c)))
    .join("")
    .trim();

/** The branch of the actions row shown to a signed-in reader of another profile. */
const visitorBranch = (): Node => {
  const row = elements(template()).find((n) => className(n) === "actions");
  if (!row) throw new Error("the actions row (.actions) is gone");
  const branch = (row.children ?? []).find(
    (c) =>
      c.type === ELEMENT &&
      directive(c, "else-if") === "currentUser && !isOwnProfile",
  );
  if (!branch) throw new Error("the actions row has no visitor branch");
  return branch;
};

/** The row's own controls, in source order. */
const controls = (): Node[] =>
  (visitorBranch().children ?? []).filter((c) => c.type === ELEMENT);

describe("the write-recommendation action", () => {
  it("stands next to Написать сообщение in the visitor's actions row", () => {
    const labels = controls().map((c) => deepText(c));

    expect(labels.slice(0, 2)).toEqual([
      "Написать сообщение",
      "Написать рекомендацию",
    ]);
  });

  it("opens the write form for this profile's owner", () => {
    const action = controls()[1];

    expect(action.tag).toBe("router-link");
    expect(directive(action, "bind", "to")).toBe("writeEndorsementLink");
  });

  it("is drawn on the server's answer and on nothing else", () => {
    expect(directive(controls()[1], "if")).toBe("canEndorse");
  });

  it("takes that answer from the shared eligibility question", () => {
    expect(source).toContain('from "./useEndorsementEligibility"');
    expect(source).toMatch(/canCreate:\s*canEndorse/);
  });
});
