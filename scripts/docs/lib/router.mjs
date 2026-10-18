// The route table of the client, read by the compiler from the createRouter()
// call in router.ts: every record, its children flattened to full paths, and the
// meta a page actually gets (vue-router merges the meta of every record on the
// way down, so a flag on a parent is a flag of each child).
import { join } from "node:path";
import { CLIENT, read } from "./repo.mjs";
import { parse, propertiesOf, unwrap, visit } from "./source.mjs";

export const ROUTER = join(CLIENT, "src", "app", "providers", "router.ts");

function stringOf(ts, node) {
  const value = node && unwrap(ts, node);
  if (value && (ts.isStringLiteral(value) || ts.isNoSubstitutionTemplateLiteral(value))) {
    return value.text;
  }
  return undefined;
}

/** A meta value as the table shows it: a literal, or "expr" for anything computed. */
function metaOf(ts, node) {
  const meta = {};
  const value = node && unwrap(ts, node);
  if (!value || !ts.isObjectLiteralExpression(value)) return meta;
  for (const [key, initializer] of propertiesOf(ts, value)) {
    const init = unwrap(ts, initializer);
    if (init.kind === ts.SyntaxKind.TrueKeyword) meta[key] = true;
    else if (init.kind === ts.SyntaxKind.FalseKeyword) meta[key] = false;
    else meta[key] = stringOf(ts, init) ?? "expr";
  }
  return meta;
}

/** The module a lazy `() => import("...")` loads, or the name a component is imported as. */
function componentOf(ts, node) {
  const value = node && unwrap(ts, node);
  if (!value) return undefined;
  if (ts.isIdentifier(value)) return value.text;
  let specifier;
  visit(ts, value, (inner) => {
    if (
      specifier === undefined &&
      ts.isCallExpression(inner) &&
      inner.expression.kind === ts.SyntaxKind.ImportKeyword
    ) {
      specifier = stringOf(ts, inner.arguments[0]);
    }
  });
  return specifier;
}

/**
 * Where a redirect leads: the route name its location object carries, also when
 * the location is returned by a function. A redirect to an imported value is
 * computed elsewhere and is named by that value.
 */
function redirectOf(ts, node) {
  const value = node && unwrap(ts, node);
  if (!value) return undefined;
  if (ts.isIdentifier(value)) return { computed: value.text };
  const literal = stringOf(ts, value);
  if (literal !== undefined) return { path: literal };
  let name;
  visit(ts, value, (inner) => {
    if (name === undefined && ts.isPropertyAssignment(inner) && inner.name.getText() === "name") {
      name = stringOf(ts, inner.initializer);
    }
  });
  return name === undefined ? { computed: "function" } : { name };
}

function joinPath(parent, path) {
  if (path.startsWith("/")) return path;
  if (path === "") return parent;
  return parent.endsWith("/") ? parent + path : `${parent}/${path}`;
}

/** The array handed to createRouter() as `routes`. */
function routesArray(ts, sourceFile) {
  let routes;
  visit(ts, sourceFile, (node) => {
    if (
      routes === undefined &&
      ts.isCallExpression(node) &&
      ts.isIdentifier(node.expression) &&
      node.expression.text === "createRouter"
    ) {
      const options = unwrap(ts, node.arguments[0]);
      const array = options && propertiesOf(ts, options).get("routes");
      if (array && ts.isArrayLiteralExpression(array)) routes = array;
    }
  });
  if (!routes) throw new Error("router.ts: no createRouter({ routes: [...] }) found");
  return routes;
}

/**
 * Every record of the table that a reader can land on: a named record, and a
 * redirect. Records that only lay out their children are walked through and not
 * returned.
 */
export function readRoutes(ts) {
  const sourceFile = parse(ts, ROUTER, read(ROUTER));
  const rows = [];
  let unread = 0;
  const walkRecords = (array, parentPath, parentMeta) => {
    for (const element of array.elements) {
      const record = unwrap(ts, element);
      if (!ts.isObjectLiteralExpression(record)) {
        unread++;
        continue;
      }
      const props = propertiesOf(ts, record);
      const path = joinPath(parentPath, stringOf(ts, props.get("path")) ?? "");
      const meta = { ...parentMeta, ...metaOf(ts, props.get("meta")) };
      const name = stringOf(ts, props.get("name"));
      const redirect = props.has("redirect") ? redirectOf(ts, props.get("redirect")) : undefined;
      const children = props.get("children");
      if (name !== undefined || redirect !== undefined) {
        rows.push({
          name,
          path,
          meta,
          redirect,
          component: componentOf(ts, props.get("component")),
          order: rows.length,
        });
      }
      if (children && ts.isArrayLiteralExpression(children)) {
        walkRecords(children, path, meta);
      }
    }
  };
  walkRecords(routesArray(ts, sourceFile), "", {});
  return { rows, unread };
}
