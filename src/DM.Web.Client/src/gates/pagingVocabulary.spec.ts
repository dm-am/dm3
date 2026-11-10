/**
 * @vitest-environment node
 */

/**
 * No request leaves the client paging by a name the API retired. The API pages
 * every offset list with skip/take (API_DESIGN.md, "Словарь query-параметров"),
 * and a caller that sends `number`, `size`, `page`, `offset` or `pageSize` gets
 * a 200 and the first page forever, since an unknown query parameter is
 * ignored. Read off the tree, the only side of the wire the C# check cannot
 * reach. The page number stays the client's own address vocabulary (`?number=2`
 * is a bookmarkable URL); what is checked is the request, not the route.
 *
 * The query argument is resolved to the keys that can reach it: an object
 * literal contributes its keys and spreads, a variable its initialiser and
 * every `x.key =` written before the call, a call what its body returns. It
 * cannot follow a dynamic key, an imported constant object, or a module
 * boundary other than a called builder.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { CLIENT_SRC, collect, asPath } from "@/shared/lib/testing/sourceTree";

/** Paging names the API does not bind. */
const RETIRED = ["number", "size", "page", "pageSize", "offset"];

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

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

const SOURCES: Module[] = collect(CLIENT_SRC, isSource).map((file) => {
  const code = readFileSync(file, "utf8");
  return {
    path: asPath(file),
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

/** Reads a span of a module one level further down, as the expression around it is read. */
type Reread = (from: number, to: number) => string[];

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
  const reread: Reread = (a, b) =>
    keysOf(mod, a, b, at, floor, depth + 1, seen);

  // `condition ? a : b` — both branches are the request on some run.
  const branches = conditionalBranches(masked, from);
  if (branches) return branches.flatMap(([a, b]) => reread(a, b));

  if (text.startsWith("(") && matchBracket(masked, lead) === tail - 1) {
    return reread(start + 1, from + tail - 1);
  }

  if (text.startsWith("{")) {
    return objectKeys(raw, masked.slice(lead + 1, tail - 1), start + 1, reread);
  }

  const call = /^(?:this\.)?([A-Za-z_$][\w$]*)\s*\(/.exec(text);
  if (call) return returnedKeys(mod, call[1], depth, seen);

  return nameKeys(mod, text, at, floor, depth, seen);
}

/** The two branches of `condition ? a : b` in a span starting at `from`, or null. */
function conditionalBranches(
  masked: string,
  from: number,
): [[number, number], [number, number]] | null {
  const question = splitTopLevel(masked, "?");
  if (question.length < 2) return null;
  const branches = splitTopLevel(masked.slice(question[0][1] + 1), ":");
  if (branches.length < 2) return null;
  const base = from + question[0][1] + 1;
  return [
    [base + branches[0][0], base + branches[0][1]],
    [base + branches[1][0], base + branches[1][1]],
  ];
}

/** An object literal: its own keys, and those of every spread, `inner` read from `innerStart`. */
function objectKeys(
  raw: string,
  inner: string,
  innerStart: number,
  reread: Reread,
): string[] {
  const keys = literalKeys(raw);
  for (const [a, b] of splitTopLevel(inner, ",")) {
    const spread = inner.slice(a, b).indexOf("...");
    if (spread < 0) continue;
    keys.push(...reread(innerStart + a + spread + 3, innerStart + b));
  }
  return keys;
}

/** A call: what the body of the function it calls returns, in the first module that has one. */
function returnedKeys(
  mod: Module,
  name: string,
  depth: number,
  seen: Set<string>,
): string[] {
  const keys: string[] = [];
  for (const target of [mod, ...SOURCES]) {
    const bodies = functionBodies(target, name);
    if (bodies.length === 0) continue;
    for (const [bodyStart, bodyEnd] of bodies) {
      const token = `${target.path}:body:${bodyStart}`;
      if (seen.has(token)) continue;
      const inner = new Set(seen).add(token);
      for (const [exprStart, exprEnd] of returnedSpans(
        target,
        bodyStart,
        bodyEnd,
      )) {
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

/** The expression of every `return` in a function body, as spans. */
function returnedSpans(
  target: Module,
  bodyStart: number,
  bodyEnd: number,
): [number, number][] {
  const spans: [number, number][] = [];
  const returns = /\breturn\b/g;
  const body = target.masked.slice(bodyStart, bodyEnd);
  let statement: RegExpExecArray | null;
  while ((statement = returns.exec(body)) !== null) {
    const exprStart = bodyStart + statement.index + statement[0].length;
    spans.push([exprStart, statementEnd(target.masked, exprStart, bodyEnd)]);
  }
  return spans;
}

/** Where a statement starting at `from` ends: a top-level `;` or line break, or `to`. */
function statementEnd(masked: string, from: number, to: number): number {
  let end = from;
  let nesting = 0;
  while (end < to) {
    const ch = masked[end];
    if ("([{".includes(ch)) nesting++;
    else if (")]}".includes(ch)) nesting--;
    else if (nesting === 0 && (ch === ";" || ch === "\n")) break;
    end++;
  }
  return end;
}

/** The declaration of `name` nearest above `at`: inside `floor` if there is one there, else anywhere above. */
function nearestDeclaration(
  mod: Module,
  name: string,
  at: number,
  floor: number,
): number {
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
  return nearest;
}

/** A name: the keys of its initialiser, and of every `x.key =` written before `at`. */
function nameKeys(
  mod: Module,
  text: string,
  at: number,
  floor: number,
  depth: number,
  seen: Set<string>,
): string[] {
  const name = text.replace(/\.value$/, "");
  if (!IDENTIFIER.test(name)) return [];

  const nearest = nearestDeclaration(mod, name, at, floor);
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
  // A corpus, or a reading of the requests, that came back empty would pass
  // both rules below.
  it("reads the requests of the client and the keys they send", () => {
    expect(SOURCES.length).toBeGreaterThan(500);
    const calls = SOURCES.flatMap((mod) =>
      apiGetCalls(mod).map((call) => ({ mod, call })),
    );
    expect(calls.length).toBeGreaterThan(100);

    const sent = new Set(
      calls.flatMap(({ mod, call: { end, args } }) =>
        args.length < 2
          ? []
          : keysOf(mod, args[1][0], args[1][1], end, 0, 0, new Set()),
      ),
    );
    expect([...sent]).toEqual(expect.arrayContaining(["take", "skip"]));
  });

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
    // `Api.get(\`uploads?number=${n}\`)` is the same request with the
    // parameters spelled by hand. Read off the path argument of the call only:
    // `?number=` in a permalink or an address is this application's own.
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
