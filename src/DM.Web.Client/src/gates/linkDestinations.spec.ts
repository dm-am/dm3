/**
 * @vitest-environment node
 */

/**
 * An anchor in a template names a destination. A clickable action is a
 * <button>, not an <a> without href (UI_STANDARDS), and `href="#"` names the
 * page already open, so a new tab, "copy link address" and the status bar all
 * land where the reader is. A literal href must name a destination, an anchor
 * with no href is the control the standard sends to <button>, and a bound
 * `:href` is an expression this walk cannot evaluate, so it passes unread.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

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

const isComponent = (name: string): boolean => name.endsWith(".vue");

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
  for (const file of collect(CLIENT_SRC, isComponent)) {
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
          at: `${asPath(file)}:${node.loc.start.line}`,
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
