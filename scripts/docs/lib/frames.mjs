// The states of the visual stand (scripts/visual/states.ts) and the roles they
// look through (roles.ts), read by the compiler. A state covers the route its
// address opens; whatever its act() clicks through to afterwards is not read,
// so a state that starts on one page and photographs another counts for the
// first.
import { join } from "node:path";
import { ROOT, read } from "./repo.mjs";
import { parse, propertiesOf, scopeOf, textOf, unwrap, visit } from "./source.mjs";

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

/** The address of a state: a string, a template, or a function of the fixture returning one. */
function addressOf(ts, node, scope) {
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

function stringOf(ts, node) {
  const value = node && unwrap(ts, node);
  return value && ts.isStringLiteral(value) ? value.text : undefined;
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
  const scope = scopeOf(ts, sourceFile);
  const states = [];
  let unread = 0;
  for (const element of exportedArray(ts, sourceFile, "STATES").elements) {
    const state = unwrap(ts, element);
    if (!ts.isObjectLiteralExpression(state)) {
      unread++;
      continue;
    }
    const props = propertiesOf(ts, state);
    const name = stringOf(ts, props.get("name"));
    const address = props.has("url") ? addressOf(ts, props.get("url"), scope) : null;
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
