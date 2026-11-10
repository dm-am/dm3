// The states of the visual stand (scripts/visual/states.ts and the sections
// under scripts/visual/states/) and the roles they
// look through (roles.ts), read by the compiler. A state covers the route its
// address opens; whatever its act() clicks through to afterwards is not read,
// so a state that starts on one page and photographs another counts for the
// first.
import { join } from "node:path";
import { ROOT, read, walk } from "./repo.mjs";
import {
  parse,
  propertiesOf,
  scopeOf,
  stringOf,
  textOf,
  unwrap,
  visit,
} from "./source.mjs";

const VISUAL = join(ROOT, "scripts", "visual");

function exportedArray(ts, sourceFile, name) {
  let array;
  visit(ts, sourceFile, (node) => {
    if (
      array === undefined &&
      ts.isVariableDeclaration(node) &&
      ts.isIdentifier(node.name) &&
      node.name.text === name &&
      node.initializer
    ) {
      const value = unwrap(ts, node.initializer);
      if (ts.isArrayLiteralExpression(value)) array = value;
    }
  });
  if (!array) throw new Error(`scripts/visual: no array ${name} found`);
  return array;
}

/**
 * Every array the section files export, by name, and what their addresses may
 * refer to: the constants of all of them, shared.ts among them.
 */
function readSections(ts) {
  const arrays = new Map();
  const scope = {
    constants: new Map(),
    fields: new Map(),
    arrows: new Map(),
    bindings: new Map(),
  };
  for (const path of walk(join(VISUAL, "states"), (p) => p.endsWith(".ts"))) {
    const sourceFile = parse(ts, path, read(path));
    const own = scopeOf(ts, sourceFile);
    for (const key of ["constants", "fields", "arrows"]) {
      for (const [name, value] of own[key]) scope[key].set(name, value);
    }
    visit(ts, sourceFile, (node) => {
      if (
        ts.isVariableDeclaration(node) &&
        ts.isIdentifier(node.name) &&
        node.initializer
      ) {
        const value = unwrap(ts, node.initializer);
        if (ts.isArrayLiteralExpression(value))
          arrays.set(node.name.text, value);
      }
    });
  }
  return { arrays, scope };
}

/** The elements of STATES with each spread of a section file replaced by that section's elements. */
function flatten(ts, array, arrays) {
  return array.elements.flatMap((element) =>
    ts.isSpreadElement(element) &&
    ts.isIdentifier(element.expression) &&
    arrays.has(element.expression.text)
      ? flatten(ts, arrays.get(element.expression.text), arrays)
      : [element],
  );
}

/** The address of a state: a string, a template, or a function of the fixture returning one. */
function stateAddress(ts, node, scope) {
  const value = unwrap(ts, node);
  if (value && ts.isArrowFunction(value)) {
    // The fixture is read through its fields (f.playedGame), which no scope
    // knows, so each of them stands as PARAM.
    if (!ts.isBlock(value.body)) return textOf(ts, value.body, scope);
    const returned = value.body.statements.find((s) => ts.isReturnStatement(s));
    return returned ? textOf(ts, returned.expression, scope) : null;
  }
  return textOf(ts, value, scope);
}

/** Names of the roles, in the order roles.ts lists them. */
function readRoles(ts) {
  const path = join(VISUAL, "roles.ts");
  const sourceFile = parse(ts, path, read(path));
  return exportedArray(ts, sourceFile, "ROLES")
    .elements.map((element) => unwrap(ts, element))
    .filter((element) => ts.isObjectLiteralExpression(element))
    .map((element) => stringOf(ts, propertiesOf(ts, element).get("name")))
    .filter((name) => name !== undefined);
}

/**
 * Every state with its role and the route its address resolves to. An entry of
 * the array that is not an object literal (a spread, a call) is not read and is
 * counted, so the document can say how many it skipped.
 */
export function readFrames(ts, resolve) {
  const path = join(VISUAL, "states.ts");
  const sourceFile = parse(ts, path, read(path));
  const sections = readSections(ts);
  const own = scopeOf(ts, sourceFile);
  const scope = {
    ...own,
    constants: new Map([...sections.scope.constants, ...own.constants]),
    fields: new Map([...sections.scope.fields, ...own.fields]),
    arrows: new Map([...sections.scope.arrows, ...own.arrows]),
  };
  const states = [];
  let unread = 0;
  for (const element of flatten(
    ts,
    exportedArray(ts, sourceFile, "STATES"),
    sections.arrays,
  )) {
    const state = unwrap(ts, element);
    if (!ts.isObjectLiteralExpression(state)) {
      unread++;
      continue;
    }
    const props = propertiesOf(ts, state);
    const name = stringOf(ts, props.get("name"));
    const address = props.has("url")
      ? stateAddress(ts, props.get("url"), scope)
      : null;
    if (name === undefined || address === null) {
      unread++;
      continue;
    }
    states.push({
      name,
      role: stringOf(ts, props.get("role")) ?? "?",
      route: resolve(address),
    });
  }
  return { states, roles: readRoles(ts), unread };
}
