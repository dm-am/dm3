/**
 * Measures the client and scripts/ against the limits of sizeRatchet.js with ESLint's own
 * rules through eslint.config.js, each wrapped to hear what it reports and name
 * the node stably rather than by line number.
 */
import { ESLint, type Rule, type SourceCode } from "eslint";
import { builtinRules } from "eslint/use-at-your-own-risk";
import vue from "eslint-plugin-vue";
import { relative } from "node:path";

interface FunctionSize {
  lines?: number;
  complexity?: number;
  depth?: number;
}
type Block = "script" | "template" | "style";
export interface FileSize {
  lines?: number;
  /** Nesting outside any function: the top level of a module or a script block. */
  depth?: number;
  blocks?: Partial<Record<Block, number>>;
  functions?: Record<string, FunctionSize>;
}
export type SizeRegister = Record<string, FileSize>;

export interface Limits {
  lines: number;
  functionLines: number;
  complexity: number;
  depth: number;
  blocks: Record<Block, number>;
}

type Node = Rule.Node & { type: string; [key: string]: unknown };

const FUNCTIONS = new Set([
  "FunctionDeclaration",
  "FunctionExpression",
  "ArrowFunctionExpression",
]);
/** Expressions that wrap a value without changing what it is. */
const TRANSPARENT = new Set([
  "TSAsExpression",
  "TSSatisfiesExpression",
  "TSNonNullExpression",
  "TSTypeAssertion",
  "TSInstantiationExpression",
  "AwaitExpression",
  "ChainExpression",
]);
const SUITE = /^(describe|it|test|bench)(\.|\(|$)/;
const HOOK = /(^|\.)(beforeEach|afterEach|beforeAll|afterAll)$/;

const isFunction = (node: Node) => FUNCTIONS.has(node.type);

const textOf = (node: Node, code: SourceCode, max = 60) =>
  code.getText(node).replace(/\s+/g, " ").slice(0, max);

/** A declared name without its type annotation, which is not part of the name. */
const bindingName = (id: Node, code: SourceCode) =>
  id.type === "Identifier" ? (id.name as string) : textOf(id, code);

function keyName(owner: Node, code: SourceCode): string {
  const key = owner.key as Node;
  if (key.type === "PrivateIdentifier") return `#${key.name as string}`;
  if (!owner.computed && key.type === "Identifier") return key.name as string;
  if (key.type === "Literal") return String(key.value);
  return `[${textOf(key, code)}]`;
}

function className(classBody: Node): string {
  const declaration = classBody.parent as Node;
  return ((declaration.id as Node | null)?.name as string) ?? "class";
}

function calleeName(callee: Node): string {
  if (callee.type === "Identifier") return callee.name as string;
  if (callee.type === "ThisExpression") return "this";
  if (callee.type === "MemberExpression") {
    const property = callee.property as Node;
    const name = callee.computed ? "[]" : (property.name as string);
    return `${calleeName(callee.object as Node)}.${name}`;
  }
  if (callee.type === "CallExpression") {
    return `${calleeName(callee.callee as Node)}()`;
  }
  const inner = (callee.expression ?? callee.argument) as Node | undefined;
  if (TRANSPARENT.has(callee.type) && inner) return calleeName(inner);
  return "(expression)";
}

/** Steps up through the wrappers that do not change what a value is. */
function container(node: Node): { outer: Node; parent: Node | null } {
  let outer = node;
  let parent = node.parent as Node | null;
  while (parent && TRANSPARENT.has(parent.type)) {
    outer = parent;
    parent = parent.parent as Node | null;
  }
  return { outer, parent };
}

/** The variable, property or assignment a call's result is stored in. */
function storedIn(call: Node, code: SourceCode): string | null {
  const { outer, parent } = container(call);
  if (!parent) return null;
  if (parent.type === "VariableDeclarator" && parent.init === outer) {
    return bindingName(parent.id as Node, code);
  }
  if (
    (parent.type === "Property" || parent.type === "PropertyDefinition") &&
    parent.value === outer
  ) {
    return keyName(parent, code);
  }
  if (parent.type === "AssignmentExpression" && parent.right === outer) {
    return textOf(parent.left as Node, code);
  }
  return null;
}

/** The first string a call is handed: the title of a suite, a case or a hook. */
function titleOf(args: Node[], code: SourceCode): string {
  const title = args.find(
    (arg) =>
      (arg.type === "Literal" && typeof arg.value === "string") ||
      arg.type === "TemplateLiteral",
  );
  if (!title) return "";
  const text =
    title.type === "Literal" ? String(title.value) : textOf(title, code, 80);
  return ` "${text.replace(/\s+/g, " ").slice(0, 80)}"`;
}

/** A callback is named by the call it is handed to. */
function callbackName(call: Node, outer: Node, code: SourceCode): string {
  const callee = calleeName(call.callee as Node);
  const args = call.arguments as Node[];
  const title = titleOf(args, code);
  if (SUITE.test(callee) || HOOK.test(callee)) return callee + title;
  // The name survives an edit elsewhere in the file: what the result is stored
  // in, else the call's other first argument, and the position among several
  // callbacks of one call only when there are several.
  const at =
    args.filter(isFunction).length > 1 ? `@${args.indexOf(outer)}` : "";
  const owner = storedIn(call, code);
  if (owner) return `${callee}:${owner}${at}`;
  if (title) return callee + title + at;
  const [first] = args;
  const hint =
    first && first !== outer && !isFunction(first)
      ? `(${textOf(first, code, 40)})`
      : "";
  return callee + hint + at;
}

const accessorPrefix = (member: Node) =>
  member.kind === "get" || member.kind === "set" ? `${member.kind} ` : "";

/** The name a class member or an object property gives the function it holds. */
function memberName(node: Node, code: SourceCode): string | null {
  const parent = node.parent as Node;
  if (parent.type === "MethodDefinition") {
    const member =
      parent.kind === "constructor" ? "constructor" : keyName(parent, code);
    return `${className(parent.parent as Node)}.${accessorPrefix(parent)}${member}`;
  }
  if (parent.value !== node) return null;
  if (parent.type === "PropertyDefinition") {
    return `${className(parent.parent as Node)}.${keyName(parent, code)}`;
  }
  if (parent.type === "Property") {
    return accessorPrefix(parent) + keyName(parent, code);
  }
  return null;
}

/** The name of whatever a function is handed to or stored in. */
function holderName(node: Node, code: SourceCode): string {
  const { outer, parent: holder } = container(node);
  switch (holder?.type) {
    case "VariableDeclarator":
      if (holder.init === outer) return bindingName(holder.id as Node, code);
      break;
    case "Property":
      if (holder.value === outer) return keyName(holder, code);
      break;
    case "AssignmentExpression":
      if (holder.right === outer) return textOf(holder.left as Node, code);
      break;
    case "ExportDefaultDeclaration":
      return "export default";
    case "CallExpression":
    case "NewExpression":
      if ((holder.arguments as Node[]).includes(outer)) {
        return callbackName(holder, outer, code);
      }
      break;
  }
  return "(anonymous)";
}

/** What one function (or class member body) is called on its own. */
function ownName(node: Node, code: SourceCode): string {
  if (node.type === "StaticBlock") {
    return `${className(node.parent as Node)}.static`;
  }
  if (node.type === "FunctionDeclaration") {
    return ((node.id as Node | null)?.name as string) ?? "default";
  }
  return memberName(node, code) ?? holderName(node, code);
}

/** The units the complexity rule may blame: functions and class initializers. */
function isUnit(node: Node): boolean {
  if (isFunction(node) || node.type === "StaticBlock") return true;
  const parent = node.parent as Node | null;
  return parent?.type === "PropertyDefinition" && parent.value === node;
}

const keysByProgram = new WeakMap<object, Map<Node, string>>();

/**
 * Every unit of a file keyed by the names around it; one chain is told apart
 * by order over all units, so a key holds when a sibling crosses a limit.
 */
function unitKeys(code: SourceCode): Map<Node, string> {
  const cached = keysByProgram.get(code.ast);
  if (cached) return cached;
  const keys = new Map<Node, string>();
  const seen = new Map<string, number>();
  const visit = (node: Node, chain: string[]) => {
    let inner = chain;
    if (isUnit(node)) {
      inner = [...chain, ownName(node, code)];
      const base = inner.join(" > ");
      const count = (seen.get(base) ?? 0) + 1;
      seen.set(base, count);
      keys.set(node, count === 1 ? base : `${base}#${count}`);
    }
    for (const field of code.visitorKeys[node.type] ?? []) {
      const child = node[field] as Node | Node[] | null | undefined;
      for (const each of Array.isArray(child) ? child : [child]) {
        if (each && typeof each.type === "string") visit(each, inner);
      }
    }
  };
  visit(code.ast as unknown as Node, []);
  keysByProgram.set(code.ast, keys);
  return keys;
}

interface Reading {
  measure: "lines" | "block" | "functionLines" | "complexity" | "depth";
  value: number;
  name?: string;
}

/**
 * The rule as it is, with its reports turned into readings: the size it found
 * and, for a function, the key of the function it blamed.
 */
interface Report {
  node?: Node;
  loc?: Rule.Node["loc"];
  data?: Record<string, unknown>;
}

function listen(
  rule: Rule.RuleModule,
  read: (report: Report, code: SourceCode) => Reading,
): Rule.RuleModule {
  return {
    meta: { ...rule.meta, messages: undefined, docs: undefined },
    create(context) {
      const relay = Object.create(context, {
        report: {
          value: (report: Report) => {
            const loc = report.loc ?? report.node?.loc;
            context.report({
              loc: loc as NonNullable<Rule.Node["loc"]>,
              message: JSON.stringify(read(report, context.sourceCode)),
            });
          },
        },
      }) as Rule.RuleContext;
      return rule.create(relay);
    },
  };
}

/** max-lines-per-function blames the method or property around a function. */
function unitOf(node: Node): Node {
  const parent = node.type;
  return parent === "MethodDefinition" || parent === "Property"
    ? (node.value as Node)
    : node;
}

function core(name: string): Rule.RuleModule {
  const rule = builtinRules.get(name);
  if (!rule) throw new Error(`ESLint no longer ships ${name}`);
  return rule;
}

/** The function max-depth counts a nested statement in, or none at the top level. */
function enclosingUnit(node: Node): Node | null {
  let at = node.parent as Node | null;
  while (at && !isFunction(at) && at.type !== "StaticBlock") {
    at = at.parent as Node | null;
  }
  return at;
}

const PLUGIN = "dm-size";
const rules: Record<string, Rule.RuleModule> = {
  lines: listen(core("max-lines"), ({ data }) => ({
    measure: "lines",
    value: Number(data?.actual),
  })),
  blocks: listen(
    (vue.rules as Record<string, Rule.RuleModule>)["max-lines-per-block"],
    ({ node, data }) => ({
      measure: "block",
      value: Number(data?.lineCount),
      name: (node as Node).name as string,
    }),
  ),
  "function-lines": listen(
    core("max-lines-per-function"),
    ({ node, data }, code) => ({
      measure: "functionLines",
      value: Number(data?.lineCount),
      name: unitKeys(code).get(unitOf(node as Node)),
    }),
  ),
  complexity: listen(core("complexity"), ({ node, data }, code) => ({
    measure: "complexity",
    value: Number(data?.complexity),
    name: unitKeys(code).get(node as Node),
  })),
  depth: listen(core("max-depth"), ({ node, data }, code) => {
    const unit = enclosingUnit(node as Node);
    return {
      measure: "depth",
      value: Number(data?.depth),
      name: unit ? unitKeys(code).get(unit) : undefined,
    };
  }),
};

/**
 * A linter that runs only the measuring rules, on the project's own config.
 * Given the client's config file, it measures a folder outside the client, such
 * as scripts/ from the repository root, by the same limits.
 */
export function measurer(
  cwd: string,
  limits: Limits,
  functionScope: string[],
  configFile?: string,
): ESLint {
  return new ESLint({
    cwd,
    ...(configFile ? { overrideConfigFile: configFile } : {}),
    cache: false,
    ruleFilter: ({ ruleId }) => ruleId.startsWith(`${PLUGIN}/`),
    overrideConfig: [
      {
        plugins: { [PLUGIN]: { rules } },
        rules: {
          [`${PLUGIN}/lines`]: ["error", { max: limits.lines }],
          [`${PLUGIN}/complexity`]: ["error", limits.complexity],
          [`${PLUGIN}/depth`]: ["error", limits.depth],
        },
      },
      {
        files: functionScope,
        rules: {
          [`${PLUGIN}/function-lines`]: [
            "error",
            { max: limits.functionLines },
          ],
        },
      },
      {
        files: ["**/*.vue"],
        rules: { [`${PLUGIN}/blocks`]: ["error", limits.blocks] },
      },
    ],
  });
}

function record(entry: FileSize, reading: Reading, line: number) {
  // max-depth reports every statement past the limit; the deepest one is the size.
  if (reading.measure === "depth") {
    const at =
      reading.name === undefined
        ? entry
        : ((entry.functions ??= {})[reading.name] ??= {});
    at.depth = Math.max(at.depth ?? 0, reading.value);
  } else if (reading.measure === "lines") {
    entry.lines = reading.value;
  } else if (reading.measure === "block") {
    (entry.blocks ??= {})[reading.name as Block] = reading.value;
  } else {
    const name = reading.name ?? `(unnamed at line ${line})`;
    const size = ((entry.functions ??= {})[name] ??= {});
    if (reading.measure === "functionLines") size.lines = reading.value;
    else size.complexity = reading.value;
  }
}

export interface Measurement {
  /** What is past a limit, in the shape of the register. */
  register: SizeRegister;
  /** Every file the linter read, from the client folder. */
  files: string[];
}

/**
 * Every file, block and function past its limit, with its size, keyed by path
 * from the folder the linter runs in.
 */
export async function measure(
  linter: ESLint,
  baseDir: string,
  patterns: string[] = ["."],
): Promise<Measurement> {
  const register: SizeRegister = {};
  const files: string[] = [];
  for (const result of await linter.lintFiles(patterns)) {
    const path = relative(baseDir, result.filePath).replace(/\\/g, "/");
    files.push(path);
    for (const message of result.messages) {
      if (!message.ruleId?.startsWith(`${PLUGIN}/`)) {
        throw new Error(`${path} could not be measured: ${message.message}`);
      }
      const reading = JSON.parse(message.message) as Reading;
      record((register[path] ??= {}), reading, message.line);
    }
  }
  return { register, files };
}

const sizes = (entry: FileSize | undefined) => {
  const flat = new Map<string, number>();
  if (entry?.lines) flat.set("lines", entry.lines);
  if (entry?.depth) flat.set("depth", entry.depth);
  for (const [block, value] of Object.entries(entry?.blocks ?? {})) {
    flat.set(`<${block}> lines`, value);
  }
  for (const [name, size] of Object.entries(entry?.functions ?? {})) {
    if (size.lines) flat.set(`function "${name}" lines`, size.lines);
    if (size.complexity) {
      flat.set(`function "${name}" complexity`, size.complexity);
    }
    if (size.depth) flat.set(`function "${name}" depth`, size.depth);
  }
  return flat;
};

/** How to bring a measure back under its limit, printed with each failure. */
export const HOW_TO_CUT =
  "cut by meaning, each part named after what it does, never by size alone";

/**
 * Where the register and the tree disagree, one line per measure: grown past
 * its record, shrunk below it, past a limit with no record, or recorded with
 * nothing past the limit any more.
 */
export function disagreements(
  recorded: SizeRegister,
  measured: SizeRegister,
): string[] {
  const out: string[] = [];
  const paths = new Set([...Object.keys(recorded), ...Object.keys(measured)]);
  for (const path of [...paths].sort()) {
    const was = sizes(recorded[path]);
    const now = sizes(measured[path]);
    for (const what of new Set([...was.keys(), ...now.keys()])) {
      const record = was.get(what);
      const size = now.get(what);
      if (record === size) continue;
      if (record === undefined) {
        out.push(
          `${path}: ${what} ${size} is past the limit and has no record: split the code (a new record needs the owner's word); ${HOW_TO_CUT}`,
        );
      } else if (size === undefined) {
        out.push(
          `${path}: ${what} is back within the limit: drop the record of ${record}`,
        );
      } else if (size > record) {
        out.push(
          `${path}: ${what} grew from ${record} to ${size}; ${HOW_TO_CUT}`,
        );
      } else {
        out.push(
          `${path}: ${what} shrank from ${record} to ${size}: lower the record to ${size}`,
        );
      }
    }
  }
  return out;
}
