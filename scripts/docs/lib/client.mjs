// The sources of the client as the other readers need them: every .ts and .vue
// under src/DM.Web.Client/src except the specs and their helpers, each turned
// into syntax trees. A .ts file is one tree. A .vue file is a tree per script block, plus a small
// tree per bound attribute of its template, read in the scope of its script.
import { join } from "node:path";
import { CLIENT, read, rel, walk } from "./repo.mjs";
import { parse, scopeOf, templateAttributes, vueParts } from "./source.mjs";

const SOURCE = join(CLIENT, "src");

/** Helpers only the specs import. */
const TESTING = join(SOURCE, "shared", "lib", "testing");

function isProductSource(path) {
  if (/\.(spec|test)\.ts$/.test(path) || path.startsWith(TESTING)) return false;
  return path.endsWith(".ts") || path.endsWith(".vue");
}

function mergeScopes(scopes) {
  const merged = { constants: new Map(), fields: new Map(), arrows: new Map(), bindings: new Map() };
  for (const scope of scopes) {
    for (const key of ["constants", "fields", "arrows"]) {
      for (const [name, value] of scope[key]) merged[key].set(name, value);
    }
  }
  return merged;
}

function vueFile(ts, path, text) {
  const { scripts, template } = vueParts(text);
  const trees = scripts.map((body, i) => parse(ts, `${path}.${i}.ts`, body));
  const scope = mergeScopes(trees.map((tree) => scopeOf(ts, tree)));
  const units = trees.map((sourceFile) => ({ sourceFile, scope }));
  const { bound, plain } = templateAttributes(template);
  for (const [i, expression] of bound.entries()) {
    units.push({ sourceFile: parse(ts, `${path}.t${i}.ts`, `(${expression});`), scope });
  }
  return { units, plain };
}

/** Every product source of the client, parsed. */
export function readClient(ts) {
  return walk(SOURCE, isProductSource).map((path) => {
    const text = read(path);
    const file = rel(path);
    if (path.endsWith(".vue")) return { file, ...vueFile(ts, path, text) };
    const sourceFile = parse(ts, path, text);
    return { file, units: [{ sourceFile, scope: scopeOf(ts, sourceFile) }], plain: [] };
  });
}
