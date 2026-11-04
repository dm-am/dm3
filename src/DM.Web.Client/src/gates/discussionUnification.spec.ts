/**
 * @vitest-environment node
 */

/**
 * The discussion is one section, not one per record: comments hang off a
 * topic, a game, a blog and a publication, and widgets/discussion is the one
 * section that renders them, with the same query, paging, numbering, empty
 * state and retry everywhere. Rendering <CommentItem> is what makes a file a
 * comments list, and only the section may do it; the readers that render a
 * comment and are no discussion stand apart (NOT_A_DISCUSSION).
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** @vue/compiler-core NodeTypes — the member this walk reads. */
const ELEMENT = 1;

/** The one file allowed to render a comment itself. */
const SECTION = "widgets/discussion/DiscussionSection.vue";

/** The pages that must ask the section for their discussion. */
const CONNECTED = [
  "pages/game/GameComments.vue",
  "pages/blog/BlogComments.vue",
];

/** Lists still rendering their own, with the reason; an entry goes when its file moves. */
const NOT_MOVED_YET: Record<string, string> = {
  "pages/forum/CommentsList.vue":
    "the topic is the reference the section was cut from, and moves last on its own",
};

/**
 * Files that render a comment and are no discussion, with the reason. They read
 * no query, page nothing and filter nothing, so the section has nothing to give
 * them, and they never move to it: unlike NOT_MOVED_YET, an entry here is no
 * debt. It stays while the file renders a comment.
 */
const NOT_A_DISCUSSION: Record<string, string> = {
  "widgets/ticket-thread/TicketConversation.vue":
    "the conversation of a ticket: its own table, at most a hundred comments that come whole with the ticket, no filter, no paging, and every comment read only",
};

/**
 * The section shows its skeleton before the first page only, so a refetch
 * keeps the rows the store holds while revalidating.
 */
const SKELETON_CONDITION = /v-if="[^"]*[Ll]oading[^"]*&&[^"]*!\s*comments/;

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  tag?: string;
  children?: Node[];
}

const isComponent = (name: string): boolean => name.endsWith(".vue");

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
  const templates = collect(CLIENT_SRC, isComponent).map((file) => ({
    path: asPath(file),
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
      (file) =>
        file !== SECTION &&
        !(file in NOT_MOVED_YET) &&
        !(file in NOT_A_DISCUSSION),
    );
    expect(elsewhere).toEqual([]);
  });

  it("keeps every unmoved list unmoved, so the exception cannot outlive it", () => {
    const own = renderers("CommentItem");
    for (const [file, reason] of Object.entries(NOT_MOVED_YET)) {
      expect(own, `${file}: ${reason}`).toContain(file);
    }
  });

  it("keeps every reader that is no discussion rendering a comment, so the exception cannot outlive it", () => {
    const own = renderers("CommentItem");
    for (const [file, reason] of Object.entries(NOT_A_DISCUSSION)) {
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
