/**
 * @vitest-environment node
 */

/**
 * The body of a sidebar block is a <ul> (SidebarBlock), so every child a block
 * hands to its default slot has to be an <li>. Most of them handed it bare
 * <div>s instead and one handed it a bare sentence — nesting no <ul> may hold,
 * which the browser repairs on its own terms and which the next row written
 * from the same template inherits.
 *
 * Read from the source, not from a mount. A mount holds whichever branch its
 * fixture reached — the loaded list, never the failure row — and reaching the
 * rest costs every block its own stores, routes and stubs, so a mounted check
 * ends up covering the blocks somebody wrote fixtures for and leaving the rest
 * free. The template names every child a block CAN render, all branches at
 * once, and it names them for every block in this directory, including the
 * ones added after this file.
 */
import { describe, it, expect } from "vitest";
import { existsSync, readFileSync, readdirSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse } from "vue/compiler-sfc";

const HERE = dirname(fileURLToPath(import.meta.url));

// Node kinds of the parsed template. The numbers are @vue/compiler-core's
// NodeTypes, which vue/compiler-sfc does not re-export.
const ELEMENT = 1;
const TEXT = 2;
const COMMENT = 3;
const INTERPOLATION = 5;
const DIRECTIVE = 7;

/** As much of the parser's AST as this file reads. */
interface AstNode {
  type: number;
  tag?: string;
  content?: string;
  children?: AstNode[];
  props?: { type: number; name?: string; arg?: { content?: string } }[];
}

/**
 * Components that stand for a list item: each renders an <li> and nothing
 * else, so it may sit in a block's list directly. The ones that live in this
 * directory are checked below rather than trusted; the action strips live in
 * the features layer and render one <li> per action in their "strip" variant,
 * which is their default and the only one the sidebar asks for.
 */
const LIST_ITEM_COMPONENTS = [
  "SidebarSkeleton",
  "SidebarSectionTitle",
  "SidebarForwardLink",
  "SidebarGameLink",
  "BlogLink",
  "GameRoomLink",
  "GameStatusButtons",
  "GameJoinActions",
  "BlogStatusButtons",
  "BlogJoinActions",
];

/**
 * Renders somewhere else entirely: the dialog goes into <body> through a
 * Teleport, so where it stands in the template says nothing about the list.
 */
const TELEPORTED = ["ConfirmDialog"];

function templateAst(source: string): AstNode {
  const ast = parse(source).descriptor.template?.ast;
  if (!ast) throw new Error("the file has no <template> block");
  return ast as unknown as AstNode;
}

/** The slot a <template> fills, if it fills one. */
function slotName(node: AstNode): string | null {
  const slot = (node.props ?? []).find(
    (prop) => prop.type === DIRECTIVE && prop.name === "slot",
  );
  return slot ? (slot.arg?.content ?? "default") : null;
}

/** Every use of a component in a template, however deeply it sits. */
function usesOf(node: AstNode, tag: string): AstNode[] {
  const self = node.type === ELEMENT && node.tag === tag ? [node] : [];
  return [...self, ...(node.children ?? []).flatMap((c) => usesOf(c, tag))];
}

/**
 * What ends up as a direct child of the block's <ul>. A <template> carrying
 * v-if / v-else / v-for is not a node of its own at render time, so its
 * children are the list's children; the heading slot is not in the list at all.
 */
function listChildren(node: AstNode): AstNode[] {
  return (node.children ?? []).flatMap((child) => {
    if (child.type !== ELEMENT || child.tag !== "template") return [child];
    return slotName(child) === "title" ? [] : listChildren(child);
  });
}

/** The rows a wrapper hands to the shared shell through its #item slot. */
function itemSlotChildren(node: AstNode): AstNode[] {
  return (node.children ?? [])
    .filter(
      (child) =>
        child.type === ELEMENT &&
        child.tag === "template" &&
        slotName(child) === "item",
    )
    .flatMap((slot) => slot.children ?? []);
}

/** Names what a child is, when it is something a <ul> may not hold. */
function stray(node: AstNode): string | null {
  switch (node.type) {
    case COMMENT:
      return null;
    case TEXT: {
      const text = (node.content ?? "").trim();
      return text ? `the text "${text}"` : null;
    }
    case INTERPOLATION:
      return "an interpolation";
    case ELEMENT: {
      const tag = node.tag ?? "";
      const known =
        tag === "li" ||
        // An outlet is whatever the caller passes, and the callers of the one
        // shell that has one are checked below.
        tag === "slot" ||
        LIST_ITEM_COMPONENTS.includes(tag) ||
        TELEPORTED.includes(tag);
      return known ? null : `<${tag}>`;
    }
    default:
      return null;
  }
}

const FILES = readdirSync(HERE)
  .filter((name) => name.endsWith(".vue"))
  .map((name) => ({ name, source: readFileSync(join(HERE, name), "utf8") }));

/** The blocks that build their own list, and the ones that fill the shell. */
const BLOCKS = FILES.filter(({ source }) => source.includes("<SidebarBlock"));
const WRAPPERS = FILES.filter(({ source }) =>
  source.includes("<SidebarEntityList"),
);

describe("sidebar block nesting", () => {
  it("finds the blocks to check", () => {
    // A move or a rename must fail here rather than leave every assertion
    // below passing over an empty list.
    expect(BLOCKS.length).toBeGreaterThan(10);
    expect(WRAPPERS.length).toBeGreaterThan(3);
  });

  for (const { name, source } of BLOCKS) {
    it(`puts nothing but list items into the list of ${name}`, () => {
      const found = usesOf(templateAst(source), "SidebarBlock");
      expect(found.length).toBeGreaterThan(0);
      const strays = found
        .flatMap(listChildren)
        .map(stray)
        .filter((what): what is string => what !== null);
      expect(strays).toEqual([]);
    });
  }

  for (const { name, source } of WRAPPERS) {
    it(`hands the shell a list item for a row in ${name}`, () => {
      const found = usesOf(templateAst(source), "SidebarEntityList");
      expect(found.length).toBeGreaterThan(0);
      const rows = found.flatMap(itemSlotChildren);
      expect(rows.length).toBeGreaterThan(0);
      const strays = rows
        .map(stray)
        .filter((what): what is string => what !== null);
      expect(strays).toEqual([]);
    });
  }

  for (const component of LIST_ITEM_COMPONENTS) {
    const file = join(HERE, `${component}.vue`);
    if (!existsSync(file)) continue;
    it(`${component} is a list item`, () => {
      const root = templateAst(readFileSync(file, "utf8"));
      const elements = (root.children ?? []).filter(
        (child) => child.type === ELEMENT,
      );
      expect(elements.map((element) => element.tag)).toEqual(["li"]);
    });
  }
});
