// Which pages the site links to, and which ones a letter or a notification of
// the server leads to.
//
// A link of the client is either a route name or an address. A name is the
// value of a `name:` property, which is how RouterLink :to, router.push and the
// navigation configs spell a location. An address is any string or template in
// the code that starts with "/", resolved against the route table, and every
// `href="/..."` written inside a string. Both come from the compiler, the plain
// `to="/..."` and `href="/..."` attributes of a template from the attribute
// reader of source.mjs. The router itself and the specs are not links, and
// neither is a page linking to itself: that link is only seen once the page is
// open.
//
// The server builds the addresses of its letters and notifications in three
// fixed shapes: SiteLink("..."), ConfirmationLink("...", token) and
// new Destination("Field", "/path/{id}"). Those are read from the C# text by
// regular expressions; a fourth shape would not be seen.
import { join } from "node:path";
import { CLIENT, ROOT, read, rel, walk } from "./repo.mjs";
import { PARAM, choicesOf, textOf, visit } from "./source.mjs";
import { ROUTER } from "./router.mjs";

const ROUTER_FILE = rel(ROUTER);

/** The client file a lazy component specifier points at. */
export function componentFile(component) {
  if (!component || !component.startsWith("@/")) return undefined;
  return rel(join(CLIENT, "src", component.slice(2)));
}

function isImportSpecifier(ts, node) {
  const parent = node.parent;
  return (
    parent &&
    (ts.isImportDeclaration(parent) ||
      ts.isExportDeclaration(parent) ||
      (ts.isCallExpression(parent) && parent.expression.kind === ts.SyntaxKind.ImportKeyword))
  );
}

function isLiteral(ts, node) {
  return ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node) || ts.isTemplateExpression(node);
}

/** Route names and addresses one file refers to. */
function referencesOf(ts, file) {
  const names = new Set();
  const addresses = new Set();
  const take = (text) => {
    if (text.startsWith("/")) addresses.add(text);
    for (const match of text.matchAll(/href=["'](\/[^"']*)["']/g)) addresses.add(match[1]);
  };
  for (const { sourceFile, scope } of file.units) {
    visit(ts, sourceFile, (node) => {
      if (ts.isPropertyAssignment(node) && node.name.getText().replace(/["']/g, "") === "name") {
        for (const value of choicesOf(ts, node.initializer, scope)) names.add(value);
      }
      if (isLiteral(ts, node) && !isImportSpecifier(ts, node)) {
        const text = textOf(ts, node, scope);
        if (text !== null) take(text);
      }
    });
  }
  for (const { name, value } of file.plain) {
    if (name === "to" || name === "href") take(value);
  }
  return { names, addresses };
}

/** The row an address lands on, a redirect followed to its named target. */
function landing(address, resolve, byName) {
  const row = resolve(address);
  if (!row) return undefined;
  if (row.redirect?.name) return byName.get(row.redirect.name);
  return row;
}

const isCatchAll = (row) => row.path.includes("(.*)");

/** For every row: the client files that link to it. */
export function readClientLinks(ts, client, rows, resolve) {
  const byName = new Map(rows.filter((row) => row.name).map((row) => [row.name, row]));
  const from = new Map(rows.map((row) => [row, new Set()]));
  for (const file of client) {
    if (file.file === ROUTER_FILE) continue;
    const { names, addresses } = referencesOf(ts, file);
    const credit = (row) => {
      if (!row || isCatchAll(row) || componentFile(row.component) === file.file) return;
      from.get(row).add(file.file);
    };
    for (const name of names) credit(byName.get(name));
    for (const address of addresses) credit(landing(address, resolve, byName));
  }
  return from;
}

const SERVER_SHAPES = [
  { kind: "письмо", pattern: /\b(?:SiteLink|ConfirmationLink)\(\s*"([^"]*)"/g },
  { kind: "уведомление", pattern: /\bnew Destination\(\s*"[^"]*"\s*,\s*"([^"]*)"\s*\)/g },
];

/** For every row: whether a letter or a notification of the server leads to it. */
export function readServerLinks(rows, resolve) {
  const byName = new Map(rows.filter((row) => row.name).map((row) => [row.name, row]));
  const from = new Map(rows.map((row) => [row, new Set()]));
  for (const path of walk(join(ROOT, "src"), (p) => p.endsWith(".cs"))) {
    const text = read(path);
    for (const { kind, pattern } of SERVER_SHAPES) {
      for (const match of text.matchAll(pattern)) {
        const address = `/${match[1].replace(/^\//, "")}`.replace(/\{[^}]*\}/g, PARAM);
        const row = landing(address, resolve, byName);
        if (row && !isCatchAll(row)) from.get(row).add(kind);
      }
    }
  }
  return from;
}
