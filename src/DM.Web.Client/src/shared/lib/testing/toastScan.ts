/**
 * What a toast can say, read out of the source without running it.
 *
 * Two gates read every plate the site raises: toastWidth.spec.ts measures each
 * line against the box, toastRegistry.spec.ts holds every success plate to a
 * written reason. Both ask the same question of a call - what can it put on
 * screen - and each used to carry its own copy of the scan that answers it, so
 * a blind spot fixed in one stayed open in the other. This is the one scan.
 *
 * WHERE THE LINE IS WRITTEN. A call does not always spell its sentence out. It
 * may hand over a constant, declared at the top of the file or imported from a
 * module of shared refusals; a table read by key; a computed that puts the noun
 * of the screen into a template; a prop, whose words are written by every
 * screen that draws the component. The scan follows the argument to those
 * words, and an empty answer means it found none - which each gate fails,
 * because a call nobody can read is a plate nobody checks.
 *
 * Spec-only, like styles.ts: it reads sources off disk.
 */
import { readdirSync, readFileSync } from "node:fs";
import { dirname, posix, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/** The directory the "@/" alias stands for. */
const CLIENT_SRC = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");

/** The four ways a toast is raised. */
export const TOAST_KINDS = ["success", "error", "info", "warning"];

/** Copy, as opposed to a key, a path or a class name. */
export const CYRILLIC = /[а-яА-Я]/;

/**
 * A source tree, by paths from src with "/" between the parts. The disk is
 * one; each canary writes a small one inline.
 */
export interface Tree {
  files: string[];
  read: (file: string) => string;
}

/**
 * The site's own sources: every .ts and .vue under src but the specs and the
 * spec-only helpers beside this file, which never reach the reader's screen.
 */
export const disk = (): Tree => {
  const found: string[] = [];
  const walk = (dir: string) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = resolve(dir, entry.name);
      if (entry.isDirectory()) {
        if (entry.name !== "node_modules") walk(full);
      } else if (
        (entry.name.endsWith(".ts") || entry.name.endsWith(".vue")) &&
        !entry.name.endsWith(".spec.ts")
      )
        found.push(relative(CLIENT_SRC, full).split("\\").join("/"));
    }
  };
  walk(CLIENT_SRC);
  return {
    files: found.filter((file) => !file.startsWith("shared/lib/testing/")),
    read: (file) => readFileSync(resolve(CLIENT_SRC, file), "utf8"),
  };
};

export const treeOf = (sources: Record<string, string>): Tree => ({
  files: Object.keys(sources),
  read: (file) => sources[file],
});

/**
 * The names a file calls a toast by, each with the kind it raises, for the
 * kinds asked about.
 *
 * Three forms are in use, and the renamed one matters most: a file that takes
 * `success` as `toastSuccess` raises the same plate, and a scan that missed it
 * would report an empty site. The others are the whole composable kept in a
 * constant and the composable called on the spot.
 */
const callers = (source: string, kinds: string[]): [string, string][] => {
  const names = new Map<string, string>(
    kinds.map((kind) => [`useToast().${kind}`, kind]),
  );

  for (const [, held] of source.matchAll(
    /const\s*\{([^}]*)\}\s*=\s*useToast\(\)/g,
  )) {
    for (const part of held.split(",")) {
      const [kind, alias] = part.split(":").map((piece) => piece.trim());
      if (kinds.includes(kind)) names.set(alias || kind, kind);
    }
  }

  for (const [, held] of source.matchAll(/const\s+(\w+)\s*=\s*useToast\(\)/g)) {
    for (const kind of kinds) names.set(`${held}.${kind}`, kind);
  }

  return [...names];
};

/** A quoted string, in any of the three ways this codebase writes one. */
const LITERAL = /"([^"\\\n]*)"|'([^'\\\n]*)'|`([^`\\]*)`/g;

/**
 * The source of the expression that starts at `from`, up to the comma that
 * ends the argument or the parenthesis that ends the call. A declaration also
 * ends at its semicolon: without it `const kind = action.value;` would run on
 * through the rest of the function.
 *
 * Quotes are skipped whole, because a message is free to hold a comma or a
 * bracket and the scan must not stop inside one.
 */
const expressionAt = (
  source: string,
  from: number,
  statement = false,
): string => {
  let depth = 0;
  for (let i = from; i < source.length; i++) {
    const c = source[i];
    if (c === '"' || c === "'" || c === "`") {
      const close = source.indexOf(c, i + 1);
      if (close < 0) return source.slice(from);
      i = close;
    } else if (c === "(" || c === "[" || c === "{") depth++;
    else if (c === ")" || c === "]" || c === "}") {
      if (depth === 0) return source.slice(from, i);
      depth--;
    } else if ((c === "," || (statement && c === ";")) && depth === 0)
      return source.slice(from, i);
  }
  return source.slice(from);
};

/** Every Russian string an expression spells out. */
const spelled = (expression: string): string[] =>
  [...expression.matchAll(LITERAL)]
    .map(([, double, single, tick]) => double ?? single ?? tick ?? "")
    .filter((text) => CYRILLIC.test(text));

/** A place a file raises a plate, and the argument that holds its words. */
export interface Site {
  line: number;
  kind: string;
  argument: string;
  /**
   * How the plate is raised. A call of notifyFailure says its sentence on its
   * own only where the server named nothing, and on a general refusal it is the
   * head of the interceptor's plate, "…: ошибка сервера" (generalPlate.ts), so
   * a gate measures it both ways.
   */
  via: "toast" | "notifyFailure";
  /**
   * notifyFailure only: the third argument as the source writes it, the labels
   * of the fields the request sent, which a refused field is named by.
   */
  labels?: string;
}

/**
 * Every place a file raises a plate of the kinds asked about, all four unless
 * said otherwise.
 */
export const sites = (source: string, kinds = TOAST_KINDS): Site[] => {
  const lineOf = (index: number) => source.slice(0, index).split("\n").length;
  const out: Site[] = [];
  for (const [name, kind] of callers(source, kinds)) {
    // Built by concatenation: a template literal cannot hold \w or \s.
    const call = new RegExp(
      "(?:^|[^.\\w])" + name.replace(/[.()]/g, "\\$&") + "\\s*\\(\\s*",
      "gs",
    );
    for (const match of source.matchAll(call)) {
      const at = match.index + match[0].length;
      out.push({
        line: lineOf(at),
        kind,
        argument: expressionAt(source, at),
        via: "toast",
      });
    }
  }
  if (!kinds.includes("error")) return out;

  // notifyFailure raises a plate too, and its sentence is the SECOND
  // argument: the fallback that stands when the server sent no title. Over a
  // hundred refusals reach the screen this way, and none of them was measured
  // while the scan read only the toast helpers.
  for (const match of source.matchAll(/(?:^|[^.\w])notifyFailure\s*\(\s*/g)) {
    // The helper's own declaration names its parameters, not a sentence.
    const before = source.slice(Math.max(0, match.index - 12), match.index + 1);
    if (/function\s*$/.test(before)) continue;
    const start = match.index + match[0].length;
    const first = expressionAt(source, start);
    if (source[start + first.length] !== ",") continue;
    const secondAt = start + first.length + 1;
    const second = expressionAt(source, secondAt);
    const labels =
      source[secondAt + second.length] === ","
        ? expressionAt(source, secondAt + second.length + 1).trim()
        : "";
    out.push({
      line: lineOf(start),
      kind: "error",
      argument: second.trim(),
      via: "notifyFailure",
      ...(labels ? { labels } : {}),
    });
  }
  return out;
};

/** A name the code keeps a sentence under: STATUS_FAILED, LETTER_NOT_SENT. */
const CONSTANT = /^[A-Z][A-Z0-9_]*$/;

/** A name as it reads in a template: loadErrorText as load-error-text. */
const kebab = (name: string): string =>
  name.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase()).replace(/^-/, "");

/**
 * An expression without the conditions of its choices: in `on ? A : B` the
 * name `on` picks a sentence and says none, and a prop followed there is a
 * flag the screens bind to a boolean, which has no words to find. Read on
 * an expression whose strings are already blanked out, so a "?" inside a
 * sentence is no choice.
 */
const withoutConditions = (bare: string): string => {
  let out = bare;
  let depth = 0;
  let start = 0;
  for (let i = 0; i < bare.length; i++) {
    const c = bare[i];
    if (c === "(" || c === "[" || c === "{") depth++;
    else if (c === ")" || c === "]" || c === "}") depth--;
    else if (depth !== 0) continue;
    else if (c === ":") start = i + 1;
    else if (
      c === "?" &&
      bare[i + 1] !== "." &&
      bare[i + 1] !== "?" &&
      bare[i - 1] !== "?"
    ) {
      out = out.slice(0, start) + " ".repeat(i - start) + out.slice(i);
      start = i + 1;
    }
  }
  return out;
};

/**
 * What an argument can say, followed to where its words are written.
 *
 * The argument's own strings come first. When it spells none, every name in it
 * is followed: a constant of the file, or an import traced through the index
 * files to the module that declares it; the declaration is read the same way,
 * so a table gives all its values and a computed gives the template inside it.
 * A prop is read at every screen that draws the component - a written
 * attribute is the value, a bound one an expression followed in that screen -
 * and a prop dropped into a template gives one sentence for each value.
 *
 * Past the call the scan keeps to the value. In a declaration it reads the
 * strings, the props and the names of CONSTANTS, and nothing else: followed
 * further, the key of FAILED[kind] would lead through the computed that
 * chooses the action into everything that computed reads, and a sentence
 * found there would be counted as a plate it is not. The names of CONSTANTS
 * are read beside the strings too, so a choice between a sentence and
 * ANOTHER_SENTENCE gives both. An empty answer means the argument was not
 * followed to anything.
 */
export const scanOf = (tree: Tree) => {
  const present = new Set(tree.files);
  const sources = new Map<string, string>();
  const read = (file: string): string => {
    let source = sources.get(file);
    if (source === undefined) sources.set(file, (source = tree.read(file)));
    return source;
  };
  /** Screens that draw a component without a prop one of its plates reads. */
  const gaps: string[] = [];

  /** The file an import path names: the @ alias or a relative path. */
  const moduleFile = (from: string, spec: string): string | undefined => {
    let base: string;
    if (spec.startsWith("@/")) base = spec.slice(2);
    else if (spec.startsWith("."))
      base = posix.normalize(posix.join(posix.dirname(from), spec));
    else return undefined;
    return [base, `${base}.ts`, `${base}.vue`, `${base}/index.ts`].find(
      (candidate) => present.has(candidate),
    );
  };

  /** The expression a name is declared with, through imports and re-exports. */
  const declarationOf = (
    file: string,
    name: string,
    seen = new Set<string>(),
  ): { file: string; expression: string } | undefined => {
    if (seen.has(`${file}#${name}`)) return undefined;
    seen.add(`${file}#${name}`);
    const source = read(file);

    const own = new RegExp(
      "(?:^|[^.\\w$])const\\s+" + name.replace(/\$/g, "\\$") + "\\b[^=]*=\\s*",
    ).exec(source);
    if (own)
      return {
        file,
        expression: expressionAt(source, own.index + own[0].length, true),
      };

    // An import and a named re-export read alike: the name on the right of
    // "as" is the one this file knows, the one on the left is the source's.
    for (const [, list, spec] of source.matchAll(
      /(?:import|export)\s*(?:type\s*)?\{([^}]*)\}\s*from\s*["']([^"']+)["']/g,
    )) {
      for (const part of list.split(",")) {
        const [original, alias] = part
          .replace(/^\s*type\s+/, "")
          .split(/\s+as\s+/)
          .map((piece) => piece.trim());
        if ((alias || original) !== name) continue;
        const target = moduleFile(file, spec);
        return target ? declarationOf(target, original, seen) : undefined;
      }
    }

    for (const [, spec] of source.matchAll(
      /export\s*\*\s*from\s*["']([^"']+)["']/g,
    )) {
      const target = moduleFile(file, spec);
      const found = target ? declarationOf(target, name, seen) : undefined;
      if (found) return found;
    }
    return undefined;
  };

  /** The values every screen that draws a component hands one of its props. */
  const propValues = (
    component: string,
    prop: string,
    hop: number,
  ): string[] => {
    if (!component.endsWith(".vue")) return [];
    const name = posix.basename(component, ".vue");
    const tag = new RegExp(`<(?:${name}|${kebab(name)})(?=[\\s/>])`, "g");
    const attribute = `(?:${prop}|${kebab(prop)})`;
    const out: string[] = [];
    for (const file of tree.files) {
      if (file === component || !file.endsWith(".vue")) continue;
      const source = read(file);
      for (const match of source.matchAll(tag)) {
        // The opening tag ends at the first ">" outside a quoted value.
        let end = match.index + match[0].length;
        for (let quote = ""; end < source.length; end++) {
          const c = source[end];
          if (quote) {
            if (c === quote) quote = "";
          } else if (c === '"' || c === "'") quote = c;
          else if (c === ">") break;
        }
        const opening = source.slice(match.index, end);
        const written = new RegExp(`\\s${attribute}\\s*=\\s*"([^"]*)"`).exec(
          opening,
        );
        const bound = new RegExp(
          `\\s(?::|v-bind:)${attribute}\\s*=\\s*"([^"]*)"`,
        ).exec(opening);
        if (written) out.push(written[1]);
        else if (bound) {
          const values = valuesOf(bound[1], file, hop);
          if (values.length) out.push(...values);
          else
            gaps.push(
              `${file}: <${name} :${kebab(prop)}="${bound[1]}"> is not followed to its words`,
            );
        } else gaps.push(`${file}: <${name}> is drawn without ${kebab(prop)}`);
      }
    }
    return out;
  };

  /** A sentence that drops a prop in, once for each value it is handed. */
  const expandProps = (text: string, file: string, hop: number): string[] => {
    const found = /\$\{\s*props\.([A-Za-z_$][\w$]*)\s*\}/.exec(text);
    if (!found) return [text];
    const values = [...new Set(propValues(file, found[1], hop))];
    if (!values.length) return [text];
    const head = text.slice(0, found.index);
    const tails = expandProps(
      text.slice(found.index + found[0].length),
      file,
      hop,
    );
    return values.flatMap((value) => tails.map((tail) => head + value + tail));
  };

  const memo = new Map<string, string[]>();

  const valuesOf = (expression: string, file: string, hop = 0): string[] => {
    const key = `${file}\0${hop}\0${expression}`;
    const cached = memo.get(key);
    if (cached) return cached;
    // A loop of references closes on an empty answer.
    memo.set(key, []);

    const out = spelled(expression).flatMap((text) =>
      expandProps(text, file, hop),
    );
    // At the call every name is followed, whatever the argument spells beside
    // it: `flag ? "Коротко" : message` says both, and a scan that stopped at
    // the first sentence it saw measured the short one and let the other pass
    // unread. Past the call a sentence ends the reading of props, which there
    // are as often the switch between two sentences as a third one.
    const direct = hop > 0 && out.length > 0;
    const bare = withoutConditions(expression.replace(LITERAL, '""'));
    for (const [, head, tail] of bare.matchAll(
      /(?<![.\w$])([A-Za-z_$][\w$]*)((?:\s*\??\.\s*[A-Za-z_$][\w$]*)*)/g,
    )) {
      if (head === "props") {
        const prop = /[A-Za-z_$][\w$]*/.exec(tail)?.[0];
        if (prop && !direct) out.push(...propValues(file, prop, hop));
        continue;
      }
      if (hop > 2 || (hop > 0 && !CONSTANT.test(head))) continue;
      const declared = declarationOf(file, head);
      if (declared)
        out.push(...valuesOf(declared.expression, declared.file, hop + 1));
    }

    memo.set(key, out);
    return out;
  };

  /** Every value a file writes into one field of its objects. */
  const fieldValues = (file: string, field: string): string[] => {
    const source = read(file);
    return [
      ...source.matchAll(new RegExp(`(?<![\\w$.])${field}\\s*:\\s*`, "g")),
    ].flatMap((match) =>
      valuesOf(
        expressionAt(source, match.index + match[0].length, true),
        file,
        1,
      ),
    );
  };

  return { read, gaps, valuesOf, fieldValues };
};

export type Scan = ReturnType<typeof scanOf>;
