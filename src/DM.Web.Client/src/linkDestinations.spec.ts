/**
 * @vitest-environment node
 */

/**
 * An anchor in a template names a destination.
 *
 * UI_STANDARDS puts the rule on the control: a clickable action is a
 * <button>, not an <a> without href. An `href="#"` is that anchor with a
 * placeholder where the destination belongs, and the placeholder is not free.
 * It names the address of the page already open, so the browser goes on
 * offering everything a link can do and every one of them lands where the
 * reader already is: the middle button opens a copy of the current page,
 * "copy link address" copies the current URL, the status bar previews it.
 *
 * Both anchors that carried one were something else. One stepped a form back,
 * and a control that acts is a button. The other opened a route through
 * router.push() and was a link all along, with the address kept out of the
 * markup where a new tab and the status bar could not reach it: a table of
 * moderation rows whose titles could not be opened side by side.
 *
 * The check is over destinations rather than over tags, because the
 * destination is what splits the two: a literal href must name one, an anchor
 * with no href at all is the control the standard sends to <button>, and a
 * bound `:href` is an expression this walk cannot evaluate, so it passes
 * unread.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { parse as parseSfc } from "vue/compiler-sfc";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** @vue/compiler-core NodeTypes — the members a parsed template holds. */
const ELEMENT = 1;
const SIMPLE_EXPRESSION = 4;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** A literal attribute, or a `v-bind` whose argument names one. */
interface Prop {
  type: number;
  name: string;
  /** ATTRIBUTE only. */
  value?: { content: string };
  /** DIRECTIVE only: the part after the colon in `:href`. */
  arg?: { type: number; content: string };
}

/** The slice of the template AST this walk reads; the rest is ignored. */
interface Node {
  type: number;
  /** ELEMENT only. */
  tag?: string;
  props?: Prop[];
  children?: Node[];
  loc: { start: { line: number } };
}

/**
 * An address that leads nowhere. A fragment that names a target is not one of
 * these — the skip link at the top of App.vue jumps to #main, and that is a
 * destination — but a lone "#" is the address of the page already open, and
 * `javascript:` is a control wearing an anchor.
 */
const goesNowhere = (address: string): boolean =>
  address === "" || address === "#" || address.startsWith("javascript:");

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

const hrefOf = (node: Node): Prop | undefined =>
  node.props?.find(
    (p) =>
      (p.type === ATTRIBUTE && p.name === "href") ||
      (p.type === DIRECTIVE &&
        p.name === "bind" &&
        p.arg?.type === SIMPLE_EXPRESSION &&
        p.arg.content === "href"),
  );

/** What is wrong with this anchor, in as many words, or nothing. */
const faultOf = (node: Node): string | null => {
  const destination = hrefOf(node);
  if (destination === undefined) {
    return "carries no href, so it is an action and an action is a <button>";
  }
  // A bound href is an expression; the source cannot say where it leads.
  if (destination.type === DIRECTIVE) return null;
  const address = destination.value?.content ?? "";
  if (!goesNowhere(address)) return null;
  return `carries href="${address}", which leads nowhere`;
};

describe("an anchor in a template", () => {
  const templates = new Map<string, Node>();
  for (const file of collect(CLIENT_SRC)) {
    const { descriptor } = parseSfc(readFileSync(file, "utf8"), {
      filename: file,
    });
    const ast = descriptor.template?.ast as unknown as Node | undefined;
    if (ast) templates.set(file, ast);
  }

  const anchors: { at: string; fault: string | null }[] = [];
  for (const [file, ast] of templates) {
    const visit = (node: Node): void => {
      if (node.type === ELEMENT && node.tag === "a") {
        anchors.push({
          at: `${where(file)}:${node.loc.start.line}`,
          fault: faultOf(node),
        });
      }
      for (const child of node.children ?? []) visit(child);
    };
    visit(ast);
  }

  it("is read from a tree that has anchors in it", () => {
    // A walk that found nothing would pass the assertion below in silence.
    expect(templates.size).toBeGreaterThan(250);
    expect(anchors.length).toBeGreaterThan(20);
  });

  it("names a destination, and is a link because of it", () => {
    const offenders = anchors
      .filter((anchor) => anchor.fault !== null)
      .map((anchor) => `${anchor.at}: <a> ${anchor.fault}`);

    expect(offenders).toEqual([]);
  });
});
