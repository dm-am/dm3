/**
 * @vitest-environment node
 */

/**
 * The two game-review counters of the profile.
 *
 * They are the pair the profile was missing: "Рейтинг" and "Оценено чужих
 * постов" count ratings of single posts, and there was nothing at all about
 * reviews of whole games. The routes the two counters open are asserted in
 * router.spec.ts, which is the layer allowed to hold the route table — a
 * counter without a route is a link to nowhere, so both halves are gated.
 *
 * The counters are read out of ProfilePage's template through the SFC parser
 * rather than by mounting the page: reaching the panel at runtime takes a
 * loaded profile, four stores and six requests, and both rules — the wording
 * and the link-only-when-nonzero guard — are about static markup. Same tree
 * walk as ProfilePage.spec.ts.
 */
import { describe, expect, it } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

const PAGE = join(dirname(fileURLToPath(import.meta.url)), "ProfilePage.vue");

/** @vue/compiler-core NodeTypes — the members this walk reads. */
const ELEMENT = 1;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** A literal attribute, or a directive with an argument and an expression. */
interface Prop {
  type: number;
  name: string;
  /** ATTRIBUTE only. */
  value?: { content: string };
  /** DIRECTIVE only: the bound prop name in `:to="…"`. */
  arg?: { content: string };
  /** DIRECTIVE only. */
  exp?: { content: string };
}

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  /** ELEMENT only. */
  tag?: string;
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

/** A static attribute's value, "" when the element carries none. */
const attribute = (node: Node, name: string): string =>
  node.props?.find((p) => p.type === ATTRIBUTE && p.name === name)?.value
    ?.content ?? "";

/** A `v-bind` expression by the prop it binds, "" when unbound. */
const bound = (node: Node, name: string): string =>
  node.props?.find(
    (p) => p.type === DIRECTIVE && p.name === "bind" && p.arg?.content === name,
  )?.exp?.content ?? "";

/** Every `<StatLine>` in the page, keyed by its static label. */
const statLines = (): Map<string, Node> => {
  const found = new Map<string, Node>();
  for (const node of elements(template())) {
    if (node.tag !== "StatLine") continue;
    const label = attribute(node, "label");
    if (label) found.set(label, node);
  }
  return found;
};

describe("the profile's game-review counters", () => {
  const lines = statLines();

  it.each([
    ["Получено рецензий на игры", "gameReviewsReceived"],
    ["Написано рецензий на игры", "gameReviewsGiven"],
  ])('draws "%s" off %s', (label, source) => {
    const line = lines.get(label);
    expect(line, `no StatLine labelled "${label}"`).toBeDefined();
    expect(bound(line!, "value")).toBe(source);
  });

  it.each([
    ["Получено рецензий на игры", "gameReviewsReceived", "receivedGameReviews"],
    ["Написано рецензий на игры", "gameReviewsGiven", "givenGameReviews"],
  ])(
    'links "%s" only when there is something to open',
    (label, count, link) => {
      // A zero that is a link takes the reader to an empty page and says the
      // profile has something it does not; the endorsement pair above already
      // guards on the count, and these two follow it.
      const to = bound(lines.get(label)!, "to").replace(/\s+/g, " ");

      expect(to).toContain(`${count} > 0`);
      expect(to).toContain(`${link}Link`);
      expect(to).toContain("undefined");
    },
  );

  it("keeps the game wording apart from the post wording", () => {
    // The profile already counts post ratings under "Рейтинг" and "Оценено
    // чужих постов". Four counters about reviews, and a reader has to be able
    // to tell which two are about games.
    for (const label of [
      "Рейтинг",
      "Оценено чужих постов",
      "Получено рецензий на игры",
      "Написано рецензий на игры",
    ]) {
      expect(lines.has(label), `no StatLine labelled "${label}"`).toBe(true);
    }
  });
});
