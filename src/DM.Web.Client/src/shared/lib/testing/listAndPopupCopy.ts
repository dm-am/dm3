/**
 * What a reader meets in a list item and in anything that pops up, read from
 * templates and scripts (sentencePeriods.spec.ts): every <li>; everything in a
 * window, a popup role or a Tooltip's content; a component drawn in a popup or
 * made of one; a popup's bound attribute followed into the script; and every
 * line a toast can say. Spec-only.
 */
import { parse as parseSfc } from "vue/compiler-sfc";
import { CYRILLIC, scanOf, sites, type Tree } from "./toastScan";
import { stripComments } from "./comments";
import {
  ATTRIBUTE,
  DIRECTIVE,
  ELEMENT,
  INTERPOLATION as INTERPOLATED,
  TEXT,
} from "./clipboardText";

/** A value dropped into a line: `{{ … }}` in a template, `${…}` in a literal. */
const INTERPOLATION = /\{\{[\s\S]*?\}\}|\$\{[^}]*\}/g;

/** A quoted string as spelled; one with a backslash is left out, not unescaped. */
const QUOTED = new RegExp(
  ['"([^"\\\\\\n]*)"', "'([^'\\\\\\n]*)'", "`([^`\\\\]*)`"].join("|"),
  "g",
);

/** The windows of the site, by the tag a template draws them with. */
const POPUP_TAGS = new Set(["Dialog", "ConfirmDialog", "AuthDialog"]);

/**
 * A home-made popup says what it is by its role: the pinned-topics window, the
 * subscription panel, the drawer, a drop-down menu or list, a tooltip.
 */
const POPUP_ROLES = new Set([
  "dialog",
  "alertdialog",
  "menu",
  "listbox",
  "tooltip",
]);

/** The hint that opens on hover: its text is a popup, its default slot is not. */
const TOOLTIP = "Tooltip";

/**
 * Bindings that carry no words: whether the window is open, what its button
 * runs, how it is laid out. Everything else a popup is handed is followed.
 */
const SILENT_BINDINGS = new Set([
  "show",
  "model-value",
  "modelValue",
  "action",
  "loading",
  "disabled",
  "key",
  "class",
  "style",
  "editor",
  "compact",
  "inline",
  "dirty",
  "auto",
]);

/** One line of copy, with the whitespace of the formatter squeezed out. */
const flatten = (text: string): string => text.replace(/\s+/g, " ").trim();

/** Every Russian string a fragment of source spells out. */
const spelled = (source: string): string[] =>
  [...source.matchAll(QUOTED)]
    .map(([, double, single, tick]) =>
      flatten((double ?? single ?? tick ?? "").replace(INTERPOLATION, " ")),
    )
    .filter((text) => CYRILLIC.test(text));

/** The `<script>` of a single-file component, with its comments taken out. */
const script = (source: string): string => {
  const opens = source.indexOf("<script");
  const closes = source.indexOf("</script>");
  if (opens < 0 || closes < 0) return "";
  return stripComments(source.slice(source.indexOf(">", opens) + 1, closes));
};

/** The slice of the template AST this walk reads; the rest is ignored. */
interface TemplateNode {
  type: number;
  /** ELEMENT only. */
  tag?: string;
  /** TEXT only. */
  content?: string;
  /** INTERPOLATED only: the expression as written, braces included. */
  loc?: { source: string };
  props?: {
    type: number;
    name: string;
    value?: { content: string };
    arg?: { content: string };
    exp?: { content: string };
  }[];
  children?: TemplateNode[];
}

/**
 * Tags that run inside a line rather than make one: the line they stand in
 * is read whole, with their words in it.
 */
const INLINE = new Set([
  "a",
  "b",
  "br",
  "code",
  "em",
  "i",
  "router-link",
  "RouterLink",
  "s",
  "small",
  "span",
  "strong",
  "u",
]);

/** The words of an element, a nested list aside: its items are lines of their own. */
const wordsOf = (node: TemplateNode): string => {
  if (node.type === TEXT) return node.content ?? "";
  if (node.type === INTERPOLATED) return " ";
  if (node.type === ELEMENT && node.tag !== "ul" && node.tag !== "ol")
    return (node.children ?? []).map(wordsOf).join("");
  return "";
};

/** PascalCase for a tag written either way: confirm-dialog is ConfirmDialog. */
const pascal = (tag: string): string =>
  tag.replace(/(^|-)([a-z])/g, (_, __, c: string) => c.toUpperCase());

const attribute = (node: TemplateNode, name: string): string | undefined =>
  node.props?.find((prop) => prop.type === ATTRIBUTE && prop.name === name)
    ?.value?.content;

const isPopup = (node: TemplateNode): boolean =>
  node.type === ELEMENT &&
  (POPUP_TAGS.has(pascal(node.tag ?? "")) ||
    POPUP_ROLES.has(attribute(node, "role") ?? ""));

/** The slot a `<template #name>` fills, or undefined for any other node. */
const slotOf = (node: TemplateNode): string | undefined =>
  node.tag === "template"
    ? node.props?.find(
        (prop) => prop.type === DIRECTIVE && prop.name === "slot",
      )?.arg?.content
    : undefined;

/** What one template says in the places the rule speaks about. */
interface Read {
  /** Lines of copy, as the reader meets them. */
  lines: string[];
  /** Expressions a popup is handed, to be followed into the script. */
  bound: string[];
  /** Components drawn inside a popup, by the tag they are drawn with. */
  drawn: string[];
  /** Whether the template is nothing but a popup. */
  whole: boolean;
}

/**
 * A node's own line: a paragraph, an item, or words with inline tags only; an
 * inline tag holds one only where nothing around it did.
 */
const lineOf = (node: TemplateNode, covered: boolean): string | undefined => {
  const children = node.children ?? [];
  const inline = INLINE.has(node.tag ?? "");
  const holds =
    node.tag === "p" ||
    node.tag === "li" ||
    ((!inline || !covered) &&
      children.some((c) => c.type === TEXT && CYRILLIC.test(c.content ?? "")) &&
      children.every((c) => c.type !== ELEMENT || INLINE.has(c.tag ?? "")));
  return holds ? flatten(wordsOf(node)) : undefined;
};

/** The copy of a node's own attributes: a plain one is the line. */
const attributes = (read: Read, node: TemplateNode, popupRoot: boolean) => {
  for (const prop of node.props ?? []) {
    if (prop.type === ATTRIBUTE) {
      const value = prop.value?.content ?? "";
      if (CYRILLIC.test(value) && prop.name !== "role")
        read.lines.push(flatten(value));
    } else if (prop.type === DIRECTIVE && prop.name === "bind" && prop.exp) {
      read.lines.push(...spelled(prop.exp.content));
      const name = prop.arg?.content ?? "";
      if (popupRoot && !SILENT_BINDINGS.has(name))
        read.bound.push(prop.exp.content);
    }
  }
};

/** Keeps what stands in a list item or a popup; `inside`: the template is popup content already. */
const readTemplate = (source: string, inside = false): Read => {
  const read: Read = { lines: [], bound: [], drawn: [], whole: false };
  const { descriptor } = parseSfc(source, { filename: "component.vue" });
  const root = descriptor.template?.ast as unknown as TemplateNode | undefined;
  if (!root) return read;

  const elements = (root.children ?? []).filter(
    (node) => node.type === ELEMENT,
  );
  read.whole = elements.length > 0 && elements.every(isPopup);

  const visit = (node: TemplateNode, popup: boolean, covered = false): void => {
    if (node.type === INTERPOLATED) {
      if (popup) read.lines.push(...spelled(node.loc?.source ?? ""));
      return;
    }
    if (node.type !== ELEMENT) {
      (node.children ?? []).forEach((child) => visit(child, popup, covered));
      return;
    }

    const opens = !popup && isPopup(node);
    const here = popup || opens;
    const tag = node.tag ?? "";
    let line: string | undefined;

    if (here) {
      attributes(read, node, opens);
      line = lineOf(node, covered);
      if (line) read.lines.push(line);
      if (/^[A-Z]/.test(tag) || tag.includes("-")) read.drawn.push(pascal(tag));
    } else if (tag === "li") {
      line = lineOf(node, covered);
      if (line) read.lines.push(line);
      for (const held of (node.children ?? []).filter(
        (c) => c.type === INTERPOLATED,
      ))
        read.lines.push(...spelled(held.loc?.source ?? ""));
    }
    const under = covered || line !== undefined;

    if (!here && pascal(tag) === TOOLTIP) {
      // The text is what pops up; the default slot is the control it hangs
      // on, and stays on the page.
      const text = attribute(node, "text");
      if (text && CYRILLIC.test(text)) read.lines.push(flatten(text));
      for (const prop of node.props ?? [])
        if (prop.type === DIRECTIVE && prop.arg?.content === "text" && prop.exp)
          read.bound.push(prop.exp.content);
      for (const child of node.children ?? [])
        visit(child, slotOf(child) === "content");
      return;
    }

    (node.children ?? []).forEach((child) => visit(child, here, under));
  };

  visit(root, inside);
  return read;
};

/**
 * The last name of each member chain in an expression: `message` of
 * `pending?.confirm?.message ?? ''`, the field whose values the popup reads.
 */
const members = (expression: string): string[] =>
  [
    ...expression
      .replace(QUOTED, '""')
      .matchAll(/(?:\?\.|\.)\s*([A-Za-z_$][\w$]*)(?![\w$]|\s*(?:\.|\?\.|\())/g),
  ]
    .map(([, name]) => name)
    .filter((name) => name !== "value" && name !== "length");

type Scan = ReturnType<typeof scanOf>;

/** The components a tag can stand for, by the name of their file. */
const componentsByName = (tree: Tree): Map<string, string[]> => {
  const components = new Map<string, string[]>();
  for (const file of tree.files.filter((f) => f.endsWith(".vue"))) {
    const name = file.slice(file.lastIndexOf("/") + 1, -".vue".length);
    components.set(name, [...(components.get(name) ?? []), file]);
  }
  return components;
};

/** Popup content as far as it is known: the files inside, and every template's read. */
interface PopupContent {
  inside: Set<string>;
  reads: Map<string, Read>;
}

/** The files of the components a template draws inside a popup. */
const drawnFiles = (
  read: Read,
  whole: boolean,
  components: Map<string, string[]>,
): string[] =>
  read.drawn
    .filter((tag) => whole || tag !== "")
    .flatMap((tag) => components.get(tag) ?? []);

/** One reading of every template; whether a file joined the popup content. */
const readOnce = (
  tree: Tree,
  scan: Scan,
  components: Map<string, string[]>,
  { inside, reads }: PopupContent,
): boolean => {
  let grew = false;
  for (const file of tree.files.filter((f) => f.endsWith(".vue"))) {
    const read = readTemplate(scan.read(file), inside.has(file));
    reads.set(file, read);
    const whole = inside.has(file) || read.whole;
    if (read.whole && !inside.has(file)) {
      inside.add(file);
      grew = true;
    }
    for (const drawn of drawnFiles(read, whole, components))
      if (!inside.has(drawn)) {
        inside.add(drawn);
        grew = true;
      }
  }
  return grew;
};

/**
 * Popup content, found template by template until nothing new turns up: a
 * component drawn inside a window is part of that window, and so is whatever
 * it draws in turn.
 */
const popupContent = (tree: Tree, scan: Scan): PopupContent => {
  const components = componentsByName(tree);
  const content: PopupContent = { inside: new Set(), reads: new Map() };
  let grew = true;
  while (grew) grew = readOnce(tree, scan, components, content);
  return content;
};

/** Every line of a tree the rule has something to say about, by file. */
export const copyOf = (tree: Tree): Map<string, string[]> => {
  const scan = scanOf(tree);
  const out = new Map<string, string[]>();
  const add = (file: string, lines: string[]) =>
    out.set(file, [...(out.get(file) ?? []), ...lines]);

  const { inside, reads } = popupContent(tree, scan);

  for (const [file, read] of reads) {
    add(file, read.lines);
    // A popup's copy that its template only names.
    for (const expression of read.bound) {
      add(file, scan.valuesOf(expression, file));
      for (const field of members(expression))
        add(file, scan.fieldValues(file, field));
    }
    // A component that is a window whole: its script is that window's copy.
    if (inside.has(file)) add(file, spelled(script(scan.read(file))));
  }

  // The plates. The same scan the toast gates measure them with, so a line
  // is read here wherever its words are written.
  for (const file of tree.files)
    for (const site of sites(scan.read(file)))
      add(file, scan.valuesOf(site.argument, file));

  return out;
};
