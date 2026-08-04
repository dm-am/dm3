/**
 * @vitest-environment node
 */

/**
 * No request leaves the client paging by a name the API retired.
 *
 * The API pages every offset list with skip/take (API_DESIGN.md, "Словарь
 * query-параметров"). A caller that sends `number`/`size` — or `page`, `offset`,
 * `pageSize` — gets a 200 and the first page forever, because an unknown query
 * parameter is ignored rather than refused. That is not a hypothetical: it is
 * what /v1/uploads did after the endpoint moved to skip/take and one caller
 * did not, and what the profile's endorsement list did until this check learned
 * to follow a variable. Both survived type-checking, review and the
 * architecture test that reads the retired names off the API assembly — none of
 * those can see a client sending a string.
 *
 * Read off the tree because that is the only side of the wire the C# check
 * cannot reach. Restricted to paging on purpose: `status` is retired on the two
 * module lists and correct on polls and tickets, so a blanket list of retired
 * words here would be wrong in both directions. Paging has one right answer
 * everywhere.
 *
 * The page NUMBER stays the client's own vocabulary: `?number=2` in the address
 * bar is a bookmarkable URL of this application, and the filters keep writing
 * it. What is checked is the request, not the route.
 *
 * Which is why the first fact does not read the call site literally. Of the
 * query arguments in the tree only a third are object literals; every paged
 * list builds its query in a variable or in a builder — `toSkipTake`,
 * `toCommentsQueryParams`, `buildTopicsParams`, `buildEndorsementParams` — and
 * a check that reads the argument text sees a name and nothing else. So the
 * argument is resolved to the keys that can reach it: an object literal
 * contributes its keys and its spreads, a variable contributes its initialiser
 * and every `x.key =` written before the call, a call contributes what its
 * body returns. The boundary is what it cannot follow: a query assembled behind
 * a dynamic key (`params[name] = …`), through an imported constant object, or
 * across a module boundary other than a called builder.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = resolve(dirname(fileURLToPath(import.meta.url)));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** Paging names the API does not bind. */
const RETIRED = ["number", "size", "page", "pageSize", "offset"];

function collectFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collectFiles(full, out);
    } else if (
      (full.endsWith(".ts") || full.endsWith(".vue")) &&
      !full.endsWith(".spec.ts") &&
      !full.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

/**
 * The same text with comments and the insides of strings blanked out, every
 * index preserved.
 *
 * Structure — brackets, declarations, assignments — is read off this copy, so
 * that a brace in a template literal or the word `const` in a comment cannot
 * move a boundary. Keys are read off the original text at the same offsets.
 */
function mask(code: string): string {
  const out = code.split("");
  const blank = (from: number, to: number): void => {
    for (let k = from; k < to && k < out.length; k++) {
      if (out[k] !== "\n") out[k] = " ";
    }
  };

  let i = 0;
  while (i < code.length) {
    const ch = code[i];
    const next = code[i + 1];
    if (ch === "/" && next === "/") {
      const end = code.indexOf("\n", i);
      const stop = end < 0 ? code.length : end;
      blank(i, stop);
      i = stop;
    } else if (ch === "/" && next === "*") {
      const end = code.indexOf("*/", i + 2);
      const stop = end < 0 ? code.length : end + 2;
      blank(i, stop);
      i = stop;
    } else if (ch === '"' || ch === "'" || ch === "`") {
      let j = i + 1;
      while (j < code.length) {
        if (code[j] === "\\") j += 2;
        else if (code[j] === ch) break;
        else j++;
      }
      blank(i + 1, j);
      i = j + 1;
    } else i++;
  }
  return out.join("");
}

/** Spans of `text` between top-level occurrences of a separator. */
function splitTopLevel(text: string, separator: string): [number, number][] {
  const spans: [number, number][] = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if ("([{".includes(ch)) depth++;
    else if (")]}".includes(ch)) depth--;
    else if (ch === separator && depth === 0) {
      spans.push([start, i]);
      start = i + 1;
    }
  }
  spans.push([start, text.length]);
  return spans;
}

/** Index of the bracket closing the one at `open`. */
function matchBracket(masked: string, open: number): number {
  const closing: Record<string, string> = { "(": ")", "{": "}", "[": "]" };
  const close = closing[masked[open]];
  let depth = 0;
  for (let i = open; i < masked.length; i++) {
    if (masked[i] === masked[open]) depth++;
    else if (masked[i] === close) {
      depth--;
      if (depth === 0) return i;
    }
  }
  return masked.length;
}

/** Literal keys of an object literal, quoted or bare, shorthand included. */
function literalKeys(objectText: string): string[] {
  const keys: string[] = [];
  const key = /[{,]\s*(?:"([\w$]+)"|'([\w$]+)'|([\w$]+))\s*[:,}]/g;
  let match: RegExpExecArray | null;
  while ((match = key.exec(objectText)) !== null) {
    keys.push(match[1] ?? match[2] ?? match[3]);
  }
  return keys;
}

type Module = { path: string; code: string; masked: string };

const SOURCES: Module[] = collectFiles(CLIENT_SRC).map((file) => {
  const code = readFileSync(file, "utf8");
  return {
    path: relative(CLIENT_SRC, file).split("\\").join("/"),
    code,
    masked: mask(code),
  };
});

/** Bodies of every function or method of this name in a module, as spans. */
function functionBodies(mod: Module, name: string): [number, number][] {
  const bodies: [number, number][] = [];
  const declaration = new RegExp(`\\b${name}\\s*(?:<[^>]*>)?\\s*\\(`, "g");
  let match: RegExpExecArray | null;
  while ((match = declaration.exec(mod.masked)) !== null) {
    const open = match.index + match[0].length - 1;
    const close = matchBracket(mod.masked, open);
    const brace = mod.masked.indexOf("{", close);
    if (brace < 0) continue;
    // A call, not a declaration: something other than a return type stands
    // between the parameter list and the next block.
    if (/[;=)]/.test(mod.masked.slice(close + 1, brace))) continue;
    bodies.push([brace, matchBracket(mod.masked, brace)]);
  }
  return bodies;
}

/** Span of the initialiser of the declaration starting at `declaration`. */
function initialiser(
  mod: Module,
  declaration: number,
): [number, number] | null {
  const assignment = /=(?![=>])/g;
  assignment.lastIndex = declaration;
  const found = assignment.exec(mod.masked);
  if (!found) return null;

  const from = found.index + 1;
  let depth = 0;
  for (let i = from; i < mod.masked.length; i++) {
    const ch = mod.masked[i];
    if ("([{".includes(ch)) depth++;
    else if (")]}".includes(ch)) {
      if (depth === 0) return [from, i];
      depth--;
    } else if (depth === 0 && (ch === ";" || ch === "\n")) return [from, i];
  }
  return [from, mod.masked.length];
}

const IDENTIFIER = /^[A-Za-z_$][\w$]*$/;

/**
 * Keys that can reach the object an expression denotes.
 *
 * `at` is the point the expression is read at — a name is resolved to the
 * declaration nearest above it, and only assignments written before that point
 * count. `floor` keeps the search inside the enclosing function body when there
 * is one.
 */
function keysOf(
  mod: Module,
  from: number,
  to: number,
  at: number,
  floor: number,
  depth: number,
  seen: Set<string>,
): string[] {
  if (depth > 5) return [];

  const raw = mod.code.slice(from, to);
  const masked = mod.masked.slice(from, to);
  const text = masked.trim();
  if (!text || text === "undefined" || text === "null") return [];

  const lead = masked.length - masked.trimStart().length;
  const start = from + lead;
  const tail = masked.trimEnd().length;

  // `condition ? a : b` — both branches are the request on some run.
  const question = splitTopLevel(masked, "?");
  if (question.length > 1) {
    const branches = splitTopLevel(masked.slice(question[0][1] + 1), ":");
    if (branches.length > 1) {
      const base = from + question[0][1] + 1;
      return [
        ...keysOf(
          mod,
          base + branches[0][0],
          base + branches[0][1],
          at,
          floor,
          depth + 1,
          seen,
        ),
        ...keysOf(
          mod,
          base + branches[1][0],
          base + branches[1][1],
          at,
          floor,
          depth + 1,
          seen,
        ),
      ];
    }
  }

  if (text.startsWith("(") && matchBracket(masked, lead) === tail - 1) {
    return keysOf(mod, start + 1, from + tail - 1, at, floor, depth + 1, seen);
  }

  if (text.startsWith("{")) {
    const keys = literalKeys(raw);
    const inner = masked.slice(lead + 1, tail - 1);
    for (const [a, b] of splitTopLevel(inner, ",")) {
      const spread = inner.slice(a, b).indexOf("...");
      if (spread < 0) continue;
      const base = start + 1 + a + spread + 3;
      keys.push(
        ...keysOf(mod, base, start + 1 + b, at, floor, depth + 1, seen),
      );
    }
    return keys;
  }

  const call = /^(?:this\.)?([A-Za-z_$][\w$]*)\s*\(/.exec(text);
  if (call) {
    const keys: string[] = [];
    for (const target of [mod, ...SOURCES]) {
      const bodies = functionBodies(target, call[1]);
      if (bodies.length === 0) continue;
      for (const [bodyStart, bodyEnd] of bodies) {
        const token = `${target.path}:body:${bodyStart}`;
        if (seen.has(token)) continue;
        const inner = new Set(seen).add(token);

        const returns = /\breturn\b/g;
        const body = target.masked.slice(bodyStart, bodyEnd);
        let statement: RegExpExecArray | null;
        while ((statement = returns.exec(body)) !== null) {
          const exprStart = bodyStart + statement.index + statement[0].length;
          let exprEnd = exprStart;
          let nesting = 0;
          while (exprEnd < bodyEnd) {
            const ch = target.masked[exprEnd];
            if ("([{".includes(ch)) nesting++;
            else if (")]}".includes(ch)) nesting--;
            else if (nesting === 0 && (ch === ";" || ch === "\n")) break;
            exprEnd++;
          }
          keys.push(
            ...keysOf(
              target,
              exprStart,
              exprEnd,
              exprEnd,
              bodyStart,
              depth + 1,
              new Set(inner),
            ),
          );
        }
      }
      break;
    }
    return keys;
  }

  const name = text.replace(/\.value$/, "");
  if (!IDENTIFIER.test(name)) return [];

  const declaration = new RegExp(`\\b(?:const|let|var)\\s+${name}\\b`, "g");
  let nearest = -1;
  let found: RegExpExecArray | null;
  while ((found = declaration.exec(mod.masked)) !== null) {
    if (found.index >= at) break;
    if (found.index >= floor) nearest = found.index;
  }
  if (nearest < 0) {
    declaration.lastIndex = 0;
    while ((found = declaration.exec(mod.masked)) !== null) {
      if (found.index >= at) break;
      nearest = found.index;
    }
  }
  if (nearest < 0) return [];

  const token = `${mod.path}:name:${nearest}`;
  if (seen.has(token)) return [];
  const inner = new Set(seen).add(token);

  const keys: string[] = [];
  const init = initialiser(mod, nearest);
  if (init && init[0] < at) {
    keys.push(
      ...keysOf(mod, init[0], init[1], init[0], floor, depth + 1, inner),
    );
  }

  const assignment = new RegExp(
    `\\b${name}(?:\\.value)?\\s*(?:\\.\\s*([\\w$]+)|\\[\\s*["']([\\w$]+)["']\\s*\\])\\s*=(?![=>])`,
    "g",
  );
  const region = mod.masked.slice(nearest, at);
  let written: RegExpExecArray | null;
  while ((written = assignment.exec(region)) !== null) {
    keys.push(written[1] ?? written[2]);
  }
  return keys;
}

/** Every `Api.get(...)` in a module: where it starts, ends, and its arguments. */
function apiGetCalls(mod: Module): { end: number; args: [number, number][] }[] {
  const found: { end: number; args: [number, number][] }[] = [];
  const call = /\bApi\.get\s*(?:<[\s\S]*?>)?\s*\(/g;
  let match: RegExpExecArray | null;
  while ((match = call.exec(mod.masked)) !== null) {
    const open = match.index + match[0].length - 1;
    const close = matchBracket(mod.masked, open);
    const args = splitTopLevel(mod.masked.slice(open + 1, close), ",").map(
      ([a, b]) => [open + 1 + a, open + 1 + b] as [number, number],
    );
    found.push({ end: close, args });
  }
  return found;
}

describe("paging on the wire", () => {
  it("is asked for by no name the API retired", () => {
    const offences: string[] = [];

    for (const mod of SOURCES) {
      for (const { end, args } of apiGetCalls(mod)) {
        if (args.length < 2) continue;
        const [from, to] = args[1];
        const keys = new Set(keysOf(mod, from, to, end, 0, 0, new Set()));
        for (const key of keys) {
          if (!RETIRED.includes(key)) continue;
          const argument = mod.code.slice(from, to).replace(/\s+/g, " ").trim();
          offences.push(`${mod.path} sends '${key}' (query: ${argument})`);
        }
      }
    }

    expect(offences).toEqual([]);
  });

  it("is not smuggled into the path either", () => {
    // `Api.get(\`uploads?number=${n}\`)` would be the same request with the
    // parameters spelled by hand, and the key check above cannot see it.
    //
    // Read off the path argument of the call, not off every string in the file.
    // The page NUMBER is this application's own address vocabulary: the topic
    // list writes `${path}?number=${page}`, the warn dialog carries the reader's
    // `?number=` into a permalink, and two comments spell "?number=" out in
    // prose. None of those is a request, and a check that reads them is a check
    // that has to be argued with rather than obeyed.
    const offences: string[] = [];
    const inPath = new RegExp(`[?&](${RETIRED.join("|")})=`);

    for (const mod of SOURCES) {
      for (const { args } of apiGetCalls(mod)) {
        if (args.length === 0) continue;
        const argument = mod.code.slice(args[0][0], args[0][1]);
        const hit = inPath.exec(argument);
        if (hit) {
          offences.push(
            `${mod.path} requests ${argument.trim()} with '${hit[1]}'`,
          );
        }
      }
    }

    expect(offences).toEqual([]);
  });
});
