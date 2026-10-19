/**
 * A drawn piece one node a line: an element by its tag, classes and attributes
 * with what the sheets of the drawn components cascade onto it (cascade.ts), a
 * text quoted, a comment as it stands. The scope attributes are left out: they
 * name the file that wrote the element, the declarations say what it brings.
 * Spec-only.
 */
import { isVNode, type VNode } from "vue";
import type { VueWrapper } from "@vue/test-utils";
import { cascaded } from "./cascade";
import { scopedComponentCss } from "./styles";

/** What the build attaches to a single file component. */
interface SingleFileComponent {
  __file?: string;
  __scopeId?: string;
}

/** Every single file component of a rendered tree, its file to its scope id. */
function drawnComponents(vnode: VNode, found: Map<string, string>): void {
  const instance = vnode.component;
  if (instance) {
    const { __file, __scopeId } = instance.type as SingleFileComponent;
    if (__file) found.set(__file, __scopeId ?? "");
    drawnComponents(instance.subTree, found);
  }
  if (Array.isArray(vnode.children)) {
    for (const child of vnode.children) {
      if (isVNode(child)) drawnComponents(child, found);
    }
  }
}

/** The sheets of every component the wrapper draws, each under its scope id. */
function drawnCss(wrapper: VueWrapper): string {
  const found = new Map<string, string>();
  drawnComponents(wrapper.getCurrentComponent().vnode, found);
  return [...found]
    .map(([file, scopeId]) => scopedComponentCss(file, scopeId))
    .join("\n");
}

/**
 * jsdom takes every element for the one under the pointer and pressed: a
 * selector that asks for either is dropped, and the piece is read at rest.
 */
const atRest = (css: string): string =>
  css.replace(/([^{}]+)\{([^{}]*)\}/g, (rule, selectors: string, body) => {
    if (selectors.trim().startsWith("@")) return rule;
    const kept = selectors
      .split(",")
      .filter((one) => !/:(hover|active)\b/.test(one));
    return kept.length > 0 ? `${kept.join(",")}{${body}}` : "";
  });

const SCOPE_ATTRIBUTE = /^data-v-/;

function lines(node: Node, css: string, depth: number): string[] {
  const indent = "  ".repeat(depth);
  // A fragment's anchors are empty texts, which draw nothing.
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent ? [indent + JSON.stringify(node.textContent)] : [];
  }
  if (node.nodeType === Node.COMMENT_NODE) {
    return [`${indent}<!--${node.textContent}-->`];
  }
  const element = node as Element;
  const classes = [...element.classList].map((name) => `.${name}`).join("");
  const attributes = [...element.attributes]
    .filter(({ name }) => name !== "class" && !SCOPE_ATTRIBUTE.test(name))
    .map(({ name, value }) => `[${name}=${JSON.stringify(value)}]`)
    .join("");
  const declared = cascaded(css, element);
  const style = Object.keys(declared)
    .sort()
    .map((property) => `${property}: ${declared[property]}`)
    .join("; ");
  return [
    `${indent}${element.localName}${classes}${attributes}${style ? ` { ${style} }` : ""}`,
    ...[...element.childNodes].flatMap((child) => lines(child, css, depth + 1)),
  ];
}

/** The piece under `selector`, or the whole wrapper, as drawn with its styles. */
export function markupOutline(
  wrapper: VueWrapper,
  selector?: string,
): string[] {
  const piece = selector ? wrapper.get(selector).element : wrapper.element;
  return lines(piece, atRest(drawnCss(wrapper)), 0);
}

/**
 * The role badge of a caption line as markupOutline draws it (UserRoleBadge,
 * variant "caption"): its brackets are the line's own text, at that text's depth.
 */
export function captionBadgeOutline(letter: string, depth: number): string[] {
  return [
    '" ["',
    "span.tooltip-trigger { display: inline }",
    "  b.role-letter { color: var(--accent-green); cursor: help; font-weight: bold }",
    `    ${JSON.stringify(letter)}`,
    "<!--teleport start-->",
    "<!--teleport end-->",
    '"]"',
  ].map((line) => "  ".repeat(depth) + line);
}
