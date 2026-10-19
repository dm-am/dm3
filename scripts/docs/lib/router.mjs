// The route table of the client, read by the compiler from the createRouter()
// call in router.ts and the route modules it spreads in: every record, its
// children flattened to full paths, and the meta a page actually gets
// (vue-router merges the meta of every record on the way down, so a flag on a
// parent is a flag of each child).
import { dirname, join } from "node:path";
import { CLIENT, read } from "./repo.mjs";
import { bind, parse, propertiesOf, scopeOf, stringOf, textOf, unwrap, visit } from "./source.mjs";

export const ROUTER = join(CLIENT, "src", "app", "providers", "router.ts");
/** The folder of the route modules: one part of the site each, spread into router.ts. */
export const ROUTE_MODULES = join(CLIENT, "src", "app", "providers", "routes");

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

/** The `const NAME = [...]` arrays at the top level of a file, by name. */
function arraysOf(ts, sourceFile) {
  const arrays = new Map();
  for (const statement of sourceFile.statements) {
    if (!ts.isVariableStatement(statement)) continue;
    for (const declaration of statement.declarationList.declarations) {
      const value = declaration.initializer && unwrap(ts, declaration.initializer);
      if (ts.isIdentifier(declaration.name) && value && ts.isArrayLiteralExpression(value)) {
        arrays.set(declaration.name.text, value);
      }
    }
  }
  return arrays;
}

/** The names a file imports from a relative module: local name -> the file and the exported name. */
function importsOf(ts, sourceFile, file) {
  const imports = new Map();
  for (const statement of sourceFile.statements) {
    if (!ts.isImportDeclaration(statement)) continue;
    const specifier = statement.moduleSpecifier.text;
    const bindings = statement.importClause?.namedBindings;
    if (!specifier.startsWith(".") || !bindings || !ts.isNamedImports(bindings)) continue;
    for (const element of bindings.elements) {
      imports.set(element.name.text, {
        file: join(dirname(file), `${specifier}.ts`),
        name: (element.propertyName ?? element.name).text,
      });
    }
  }
  return imports;
}

/**
 * The array literal a name of a file stands for: a constant of the file, or one
 * of a module it imports the name from. Each file is parsed once.
 */
function arrayResolver(ts) {
  const modules = new Map();
  const load = (file) => {
    if (!modules.has(file)) {
      const sourceFile = parse(ts, file, read(file));
      modules.set(file, {
        sourceFile,
        arrays: arraysOf(ts, sourceFile),
        imports: importsOf(ts, sourceFile, file),
      });
    }
    return modules.get(file);
  };
  const arrayOf = (file, name) => {
    const module = load(file);
    if (module.arrays.has(name)) return { file, array: module.arrays.get(name) };
    const imported = module.imports.get(name);
    return imported ? arrayOf(imported.file, imported.name) : undefined;
  };
  return { load, arrayOf };
}

/**
 * `...NAME.map((x) => ({ ... }))` over a list of strings: the record the arrow
 * returns and the strings it is made from, or undefined for any other spread.
 */
function mappedRecords(ts, spread, file, arrayOf) {
  const call = unwrap(ts, spread.expression);
  if (!ts.isCallExpression(call) || !ts.isPropertyAccessExpression(call.expression)) return undefined;
  if (call.expression.name.text !== "map") return undefined;
  const list = unwrap(ts, call.expression.expression);
  const found = ts.isIdentifier(list) ? arrayOf(file, list.text) : undefined;
  const arrow = call.arguments[0] && unwrap(ts, call.arguments[0]);
  if (!found || !arrow || !ts.isArrowFunction(arrow)) return undefined;
  const [parameter] = arrow.parameters;
  const record = unwrap(ts, arrow.body);
  if (!parameter || !ts.isIdentifier(parameter.name) || !ts.isObjectLiteralExpression(record)) return undefined;
  const values = found.array.elements.map((element) => stringOf(ts, element));
  if (values.some((value) => value === undefined)) return undefined;
  return { record, parameter: parameter.name.text, values, array: found };
}

/**
 * Every record of the table that a reader can land on: a named record, and a
 * redirect. Records that only lay out their children are walked through and not
 * returned. A spread `...name` is followed to the array the name stands for, and
 * a spread of a `.map` over a list of strings is read once per string.
 */
export function readRoutes(ts) {
  const { load, arrayOf } = arrayResolver(ts);
  const rows = [];
  let unread = 0;
  const walkRecords = (array, file, parentPath, parentMeta) => {
    for (const element of array.elements) {
      if (ts.isSpreadElement(element)) {
        const spread = unwrap(ts, element.expression);
        const found = ts.isIdentifier(spread) ? arrayOf(file, spread.text) : undefined;
        const mapped = found ? undefined : mappedRecords(ts, element, file, arrayOf);
        if (found) walkRecords(found.array, found.file, parentPath, parentMeta);
        else if (mapped) {
          const scope = scopeOf(ts, load(file).sourceFile);
          for (const value of mapped.values) {
            const text = (node) => (node ? textOf(ts, node, bind(scope, [mapped.parameter], value)) ?? undefined : undefined);
            readRecord(mapped.record, file, parentPath, parentMeta, text);
          }
        } else unread++;
        continue;
      }
      readRecord(unwrap(ts, element), file, parentPath, parentMeta, (node) => stringOf(ts, node));
    }
  };
  const readRecord = (record, file, parentPath, parentMeta, text) => {
    if (!ts.isObjectLiteralExpression(record)) {
      unread++;
      return;
    }
    const props = propertiesOf(ts, record);
    const path = joinPath(parentPath, text(props.get("path")) ?? "");
    const meta = { ...parentMeta, ...metaOf(ts, props.get("meta")) };
    const name = text(props.get("name"));
    const redirect = props.has("redirect") ? redirectOf(ts, props.get("redirect")) : undefined;
    const children = props.get("children") && unwrap(ts, props.get("children"));
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
      walkRecords(children, file, path, meta);
    } else if (children) {
      unread++;
    }
  };
  walkRecords(routesArray(ts, load(ROUTER).sourceFile), ROUTER, "", {});
  return { rows, unread };
}
