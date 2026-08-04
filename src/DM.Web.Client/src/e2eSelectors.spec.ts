/**
 * @vitest-environment node
 */

/**
 * The fourth rule over the Playwright corpus, and the one the other three
 * could not state: a selector has to name something the client renders.
 *
 * `e2eDiscipline.spec.ts` makes every test assert and forbids it to skip
 * itself. Neither notices that the thing asserted about does not exist. The
 * roster spec asked for `.game-tabs`, `.characters-group-title` and
 * `.group-title`; the client writes none of them, so `isVisible()` answered
 * false, the guarded body never ran, and two green tests covered the page
 * whose dead end was the highest finding of their slice. Removing the guards
 * turns that into a red test — but only when someone runs the tier, and the
 * tier needs a server, a database and a browser. This check needs a checkout.
 *
 * So: every class and every id an e2e selector names is looked up in the
 * client's own vocabulary — class attributes, `:class` bindings, and the class
 * selectors of every stylesheet. A name built at run time (`col-${key}`,
 * `sidebar-list-${token}`) contributes its literal head as a prefix, because
 * that is all a reader of the source can know.
 *
 * What it cannot do: tell a class that exists somewhere from a class that
 * exists on the page under test. A selector that passes here can still find
 * nothing at run time. It catches the invented name, which is the one that
 * silently never matches.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import ts from "typescript";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");
const E2E_TESTS = join(CLIENT_ROOT, "e2e", "tests");
const SOURCE = join(CLIENT_ROOT, "src");
const INDEX_HTML = join(CLIENT_ROOT, "index.html");

const filesUnder = (dir: string, endings: string[]): string[] => {
  const found: string[] = [];
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) found.push(...filesUnder(full, endings));
    else if (endings.some((e) => name.endsWith(e))) found.push(full);
  }
  return found;
};

const where = (node: ts.Node): string => {
  const source = node.getSourceFile();
  const { line } = source.getLineAndCharacterOfPosition(node.getStart());
  const path = relative(REPO_ROOT, source.fileName).split("\\").join("/");
  return `${path}:${line + 1}`;
};

const walk = (node: ts.Node, visit: (node: ts.Node) => void): void => {
  visit(node);
  node.forEachChild((child) => walk(child, visit));
};

/** Dotted name of a call target: `page.locator`, `expect`, ... */
const calleeName = (expression: ts.Expression): string | null => {
  if (ts.isIdentifier(expression)) return expression.text;
  if (ts.isPropertyAccessExpression(expression)) {
    const head = calleeName(expression.expression);
    return head === null ? null : `${head}.${expression.name.text}`;
  }
  return null;
};

/** The calls whose first argument Playwright reads as a CSS selector. */
const TAKES_A_SELECTOR = /(^|\.)(locator|waitForSelector)$/;

/**
 * Where a `${...}` stood. A selector finished at run time
 * (`#sidebar-list-${token}`) still names its head, and the head is exactly
 * what the client's own template literals can be read for.
 */
const HOLE = "%";

/**
 * The selector as far as the source states it. A template with holes keeps
 * them; anything else that is not a literal string (a variable, a call) is
 * not readable here at all.
 */
const selectorText = (node: ts.Node): string | null => {
  if (ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node)) {
    return node.text;
  }
  if (ts.isTemplateExpression(node)) {
    return (
      node.head.text +
      node.templateSpans.map((span) => HOLE + span.literal.text).join("")
    );
  }
  return null;
};

/**
 * Playwright's other engines: `text=`, `xpath=`, `internal:*`. Only the CSS
 * ones carry class and id names, and `css=` is the same engine spelled out.
 */
const cssPart = (selector: string): string | null => {
  if (selector.startsWith("css=")) return selector.slice(4);
  if (/^[a-z-]+=|^internal:|^\/\/|^\.\.$/.test(selector)) return null;
  return selector;
};

interface Named {
  /** Whole names, and heads of names finished at run time (trailing HOLE). */
  classes: Set<string>;
  ids: Set<string>;
}

const namesIn = (selector: string): Named => {
  const classes = new Set<string>();
  const ids = new Set<string>();
  const css = cssPart(selector);
  if (css === null) return { classes, ids };
  // Attribute values ([placeholder=".foo"]) are content, not structure.
  const bare = css.replace(/\[[^\]]*\]/g, "");
  const name = new RegExp(`([.#])([A-Za-z][\\w-]*${HOLE}?)`, "g");
  for (const [, kind, text] of bare.matchAll(name)) {
    (kind === "." ? classes : ids).add(text);
  }
  return { classes, ids };
};

/**
 * The client's own vocabulary. Class attributes give their whole value;
 * bound ones (`:class`) give only their quoted literals, because the rest of
 * the expression is code — `{ 'has-unread': hasUnread }` declares one class,
 * not two.
 */
const vocabulary = (): {
  classes: Set<string>;
  ids: Set<string>;
  prefixes: string[];
} => {
  const classes = new Set<string>();
  const ids = new Set<string>();
  const prefixes = new Set<string>();

  const files = [
    ...filesUnder(SOURCE, [".vue", ".sass", ".css", ".ts"]),
    INDEX_HTML,
  ];

  for (const file of files) {
    const text = readFileSync(file, "utf8");

    // class="a b", img-class="avatar", id="email"
    for (const [, kind, value] of text.matchAll(
      /(?:^|\s)(?:[\w-]*-)?(class|id)="([^"{}]*)"/g,
    )) {
      for (const token of value.split(/\s+/)) {
        if (/^[A-Za-z][\w-]*$/.test(token)) {
          (kind === "class" ? classes : ids).add(token);
        }
      }
    }

    // :class="{ 'has-unread': x }", :id="`sidebar-list-${token}`"
    for (const [, kind, value] of text.matchAll(
      /(?:^|\s):(?:[\w-]*-)?(class|id)="([^"]*)"/g,
    )) {
      const into = kind === "class" ? classes : ids;
      for (const [, name] of value.matchAll(/'([A-Za-z][\w-]*)'/g)) {
        into.add(name);
      }
    }

    // Style rules: `.chat-preview`, `#app`. Sass and CSS files are all
    // selectors; a .vue file holds its own after <style>.
    const styles = file.endsWith(".vue")
      ? text.slice(Math.max(0, text.indexOf("<style")))
      : file.endsWith(".sass") || file.endsWith(".css")
        ? text
        : "";
    for (const [, name] of styles.matchAll(/\.([A-Za-z][\w-]*)/g)) {
      classes.add(name);
    }
    for (const [, name] of styles.matchAll(/#([A-Za-z][\w-]*)/g)) {
      ids.add(name);
    }

    // Names finished at run time: `col-${column.key}` can only be known down
    // to its literal head.
    //
    // Two or more segments, because one is not a name: `msg-`, `col-`, `id-`
    // and a dozen others come out of this scan, and a head of one segment lets
    // through every invented name that happens to start with it — `.msg-`
    // anything would have been unchecked. A head of two segments
    // (`sidebar-list-`, `sidebar-toggle-`) is specific enough to be the name it
    // is, and the two the corpus actually addresses are both of that shape.
    for (const [, head] of text.matchAll(/`([A-Za-z][\w-]*-)\$\{/g)) {
      if (head.split("-").filter(Boolean).length >= 2) {
        prefixes.add(head);
      }
    }
  }

  return { classes, ids, prefixes: [...prefixes] };
};

describe("every e2e selector names something the client renders", () => {
  const known = vocabulary();
  const specs = filesUnder(E2E_TESTS, [".ts"]);

  it("read both sides", () => {
    // A vocabulary or a corpus that came back empty would pass everything.
    expect(specs.length).toBeGreaterThan(20);
    expect(known.classes.size).toBeGreaterThan(200);
    expect(known.ids.size).toBeGreaterThan(5);
  });

  it("holds for every class and id a spec asks for", () => {
    /**
     * A whole name has to be in the vocabulary, unless the client only ever
     * builds it at run time. A head (`sidebar-list-` out of
     * `#sidebar-list-${token}`) has to be a head the client builds.
     */
    const holds = (name: string, set: Set<string>): boolean =>
      name.endsWith(HOLE)
        ? known.prefixes.includes(name.slice(0, -HOLE.length))
        : set.has(name) || known.prefixes.some((p) => name.startsWith(p));

    const violations: string[] = [];
    for (const file of specs) {
      const source = ts.createSourceFile(
        file,
        readFileSync(file, "utf8"),
        ts.ScriptTarget.Latest,
        true,
      );
      walk(source, (node) => {
        if (!ts.isCallExpression(node)) return;
        const name = calleeName(node.expression);
        if (name === null || !TAKES_A_SELECTOR.test(name)) return;
        const argument = node.arguments[0];
        if (argument === undefined) return;
        const selector = selectorText(argument);
        if (selector === null) return;

        const named = namesIn(selector);
        for (const cls of named.classes) {
          if (!holds(cls, known.classes)) {
            violations.push(`${where(argument)}: .${cls}`);
          }
        }
        for (const id of named.ids) {
          if (!holds(id, known.ids)) {
            violations.push(`${where(argument)}: #${id}`);
          }
        }
      });
    }
    expect(violations).toEqual([]);
  });
});
