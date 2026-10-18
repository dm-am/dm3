/**
 * @vitest-environment node
 */

/**
 * The message toolbar is drawn from one file.
 *
 * Two pages render the toolbar over a chat message - the messenger and the
 * global chat - and they write the same class names. Once, those class names
 * were styled in one page's scoped block, which reaches that page only: the
 * other toolbar rendered with no rules at all, and nothing went red, because a
 * missing rule is not a failing assertion anywhere else.
 *
 * So this holds the arrangement that cannot go that way again: the rules live
 * in the partial, both pages pull them in, and neither page keeps a copy. A
 * second copy is the same defect in slower motion - the two toolbars drift
 * apart one edit at a time, and the screen is the only place it shows.
 *
 * Read off the compiled stylesheet rather than off the source text. A source
 * scan can only recognise the shapes it was taught, and the shape it was
 * taught was a class name at the head of a line or after a comma: a page could
 * reach the same buttons through `.chat-view .toolbar-btn` or `&:hover
 * .toolbar-btn` and walk past the gate. Compiled, a descendant selector is a
 * rule like any other, so the question becomes a comparison - the rules a page
 * emits on these class names against the rules the mixin emits, and nothing of
 * the page's own in between.
 *
 * This checks the wiring, not the pixels. That the partial says what the
 * toolbar should look like is a matter for the eye on a running stand; that
 * both pages say it from the same place is a matter of fact, and is this.
 */
import { describe, expect, it } from "vitest";
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  componentCss,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/** The one file that styles the toolbar. */
const PARTIAL = resolve(CLIENT_SRC, "assets/styles/_MessageToolbar.sass");

/** How a page pulls the partial in, and what it includes from it. */
const USE = '@use "@/assets/styles/MessageToolbar" as *';
const INCLUDE = "+message-toolbar";

/**
 * Where the toolbar is rendered: the feed the global chat and the messenger
 * are both drawn with.
 */
const PAGES = ["widgets/chat-feed/ChatFeed.vue"];

/**
 * The toolbar's class names, as the template spells them. The feed positions
 * the toolbar inside its frame in both chats.
 */
const CLASSES = ["toolbar-btn", "msg-toolbar"];

/**
 * A selector that reaches a toolbar element - anywhere in the selector, with
 * whatever modifier suffix follows the name (.toolbar-btn-warn is the same
 * set of buttons).
 */
const TOOLBAR = new RegExp(String.raw`\.(${CLASSES.join("|")})[\w-]*(?![\w-])`);

/** The rules a compiled stylesheet spends on the toolbar's classes. */
const toolbarRules = (css: string): string[] =>
  styleRules(css)
    .filter(({ selector }) => TOOLBAR.test(selector))
    .map(
      ({ selector, declarations }) =>
        `${selector} { ${Object.entries(declarations)
          .map(([property, value]) => `${property}: ${value}`)
          .join("; ")} }`,
    )
    .sort();

describe("the message toolbar", () => {
  // What the partial alone emits: the whole of what a page may carry on these
  // class names, and the yardstick each page below is held to.
  const fromMixin = toolbarRules(compileSass(`${USE}\n${INCLUDE}`));

  it("is styled by a partial of its own", () => {
    expect(existsSync(PARTIAL)).toBe(true);
    expect(readFileSync(PARTIAL, "utf8")).toContain(`=${INCLUDE.slice(1)}`);

    // A yardstick of nothing would let every page pass by emitting nothing
    // either, so the partial has to be seen styling both names first.
    for (const name of CLASSES) {
      expect(
        fromMixin.filter((rule) => rule.startsWith(`.${name}`)),
        name,
      ).not.toEqual([]);
    }
  });

  it.each(PAGES)("is pulled into %s from there", (page) => {
    const sass = sassBlocks(resolve(CLIENT_SRC, page));
    expect(sass).toContain(USE);
    expect(sass).toMatch(
      new RegExp(String.raw`^\s*\+message-toolbar\s*$`, "m"),
    );
  });

  it.each(PAGES)("is styled from nowhere else in %s", (page) => {
    // Every rule the page emits on a toolbar class against every rule the
    // mixin emits. A copy, an override, or a selector of the page's own
    // reaching in from outside is a rule the mixin does not have, and the two
    // lists part.
    expect(toolbarRules(componentCss(resolve(CLIENT_SRC, page)))).toEqual(
      fromMixin,
    );
  });

  /**
   * The detector, on the shapes the previous one could not tell apart. Without
   * this, a pattern that matches nothing would make every page above pass by
   * comparing one empty list with another.
   */
  it("sees a toolbar rule wherever the class name stands in the selector", () => {
    const css = compileSass(
      [
        ".chat-view",
        "  .toolbar-btn",
        "    width: 1px",
        "  &:hover .msg-toolbar",
        "    top: 0",
        "  .sentinel",
        "    top: 0",
      ].join("\n"),
    );

    expect(toolbarRules(css)).toEqual([
      ".chat-view .toolbar-btn { width: 1px }",
      ".chat-view:hover .msg-toolbar { top: 0 }",
    ]);
  });
});
