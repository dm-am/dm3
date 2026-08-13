/**
 * @vitest-environment node
 */

/**
 * The discussion is one section, not one per record.
 *
 * Comments hang off a topic, a game, a blog and a publication, and the four
 * endpoints behind them read the same query — search, authors, period, sort.
 * The pages agreed on none of it. The topic had the filter bar, paging above
 * and below the list and a permalink that resolved; the game and the blog
 * rendered a bare list with a single paging block and no search at all, not
 * because the server could not filter but because those pages never sent the
 * params. Three copies of the comment numbering, two wordings of the empty
 * state, and a failed load answered with a red line and no way to retry.
 *
 * widgets/discussion is that one section. The rule is mechanical and reads the
 * template: rendering <CommentItem> is what makes a file a comments list, and
 * only the section may do it — every page asks the section for one instead.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources it walks. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** @vue/compiler-core NodeTypes — the member this walk reads. */
const ELEMENT = 1;

/** The one file allowed to render a comment itself. */
const SECTION = "widgets/discussion/DiscussionSection.vue";

/** The pages that must ask the section for their discussion. */
const CONNECTED = [
  "pages/game/GameComments.vue",
  "pages/blog/BlogComments.vue",
];

/**
 * Lists still rendering their own, with the reason. The topic is the shape the
 * section was cut from, so it moves last and on its own: its role is to be the
 * reference, not the casualty. The entry goes the day it moves.
 */
const NOT_MOVED_YET: Record<string, string> = {
  "pages/forum/CommentsList.vue":
    "the topic is the reference the section was cut from, and moves last on its own",
};

/**
 * What the exemption above does not cover, and what drifted while it stood: the
 * section shows its skeleton before the first page only, so a refetch keeps the
 * rows the store deliberately holds while revalidating. The forum copy showed it
 * on every load and wiped the list on a change of page, filter or sort. A tag
 * this rule can count is not the thing these two have to agree on.
 */
const SKELETON_CONDITION = /v-if="[^"]*[Ll]oading[^"]*&&[^"]*!s*comments/;

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  tag?: string;
  children?: Node[];
}

const collect = (dir: string, out: string[] = []): string[] => {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (name.endsWith(".vue")) {
      out.push(full);
    }
  }
  return out;
};

const where = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

/** `<CommentItem>` and `<comment-item>` name the same component. */
const normalize = (tag: string): string => tag.replace(/-/g, "").toLowerCase();

/** Every component tag a template renders, normalized. */
function tagsIn(file: string): Set<string> {
  const { descriptor } = parseSfc(readFileSync(file, "utf8"), {
    filename: file,
  });
  const ast = descriptor.template?.ast as unknown as Node | undefined;
  const tags = new Set<string>();
  const visit = (node: Node): void => {
    if (node.type === ELEMENT && node.tag) tags.add(normalize(node.tag));
    for (const child of node.children ?? []) visit(child);
  };
  if (ast) visit(ast);
  return tags;
}

describe("the discussion is one section", () => {
  const templates = collect(CLIENT_SRC).map((file) => ({
    path: where(file),
    tags: tagsIn(file),
  }));

  const renderers = (component: string): string[] =>
    templates
      .filter(({ tags }) => tags.has(normalize(component)))
      .map(({ path }) => path);

  it("reads the templates it is supposed to read", () => {
    // A walk that found nothing would pass every assertion below.
    expect(templates.length).toBeGreaterThan(250);
  });

  it("keeps the section rendering a comment, so the rule is about something", () => {
    expect(renderers("CommentItem")).toContain(SECTION);
  });

  it("renders a comment nowhere else", () => {
    const elsewhere = renderers("CommentItem").filter(
      (file) => file !== SECTION && !(file in NOT_MOVED_YET),
    );
    expect(elsewhere).toEqual([]);
  });

  it("keeps every unmoved list unmoved, so the exception cannot outlive it", () => {
    const own = renderers("CommentItem");
    for (const [file, reason] of Object.entries(NOT_MOVED_YET)) {
      expect(own, `${file}: ${reason}`).toContain(file);
    }
  });

  it("has the game and the blog discussions on the section", () => {
    const users = renderers("DiscussionSection");
    for (const page of CONNECTED) expect(users).toContain(page);
  });

  it("keeps a list on screen while it reloads, section or copy", () => {
    const sources = [SECTION, ...Object.keys(NOT_MOVED_YET)];
    for (const file of sources) {
      const template = readFileSync(join(CLIENT_SRC, file), "utf8");
      expect(SKELETON_CONDITION.test(template), file).toBe(true);
    }
  });
});
