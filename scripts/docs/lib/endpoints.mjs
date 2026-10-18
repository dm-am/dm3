// The server's endpoints and which of them the client calls.
//
// The endpoints are artifacts/openapi-routes.json, the committed snapshot the
// API integration suite writes from the running application
// (OpenApiContractShould) and keeps equal to it. The per-module documents under
// artifacts/openapi/ hold the same routes but are not committed, so a document
// counted from them would differ between a clean clone and a machine that ran
// the suite.
//
// A call is `Api.get(...)`, `Api.post(...)` and the rest, found by the compiler,
// with its first argument followed as far as source.mjs follows an address. A
// function that hands its own parameter to Api calls (notepadEndpoints(base))
// is a factory: its calls are read once with the parameter left open, and each
// call of the factory fills it in.
//
// The server also writes a few API addresses into its answers (the content of
// an upload, an attachment of a post), and the client opens those through
// apiUrl(...) without spelling the path. Such an address is read from the C#
// text in its one fixed shape, an interpolated string starting with "/v1/"
// with a hole in it ($"/v1/uploads/{id:D}/content"); a route attribute or a
// plain example string has no hole and is not one. It counts as a GET of the
// client only while the client calls apiUrl at all.
import { join } from "node:path";
import { ROOT, compare, read, rel, walk } from "./repo.mjs";
import { bind, choicesOf, PARAM, textOf, unwrap, visit } from "./source.mjs";
import { endpointResolver } from "./matcher.mjs";

const SNAPSHOT = join(ROOT, "artifacts", "openapi-routes.json");

const SERVER_ADDRESS = /\$"(\/v1\/[^"]*\{[^"]*)"/g;

const METHODS = { get: "GET", post: "POST", postFile: "POST", put: "PUT", patch: "PATCH", delete: "DELETE" };

/** Marks the n-th parameter of a factory inside an address read from its body. */
const slot = (index) => `\u0002${index}\u0002`;

export function readEndpoints() {
  return JSON.parse(read(SNAPSHOT)).map((line) => {
    const [method, path] = line.split(" ");
    return { method, path };
  });
}

/** The API addresses the server writes into its answers, each with the file that writes it. */
export function readServerAddresses() {
  const found = [];
  for (const path of walk(join(ROOT, "src"), (p) => p.endsWith(".cs"))) {
    for (const match of read(path).matchAll(SERVER_ADDRESS)) {
      found.push({ path: match[1].replace(/\{[^}]*\}/g, PARAM), file: rel(path) });
    }
  }
  return found;
}

const isApiUrl = (ts, callee) => ts.isIdentifier(callee) && callee.text === "apiUrl";

/** The HTTP method of an `Api.<verb>(...)` call, or undefined for any other call. */
function apiMethod(ts, call) {
  const callee = call.expression;
  if (!ts.isPropertyAccessExpression(callee)) return undefined;
  if (!ts.isIdentifier(callee.expression) || callee.expression.text !== "Api") return undefined;
  return METHODS[callee.name.text];
}

/** The API path a client address stands for: no query, under /v1/. */
function apiPath(text) {
  const address = text.split("?")[0].replace(/^\/+/, "");
  return `/v1/${address}`;
}

function findFactories(ts, client) {
  const factories = new Map();
  const bodies = new Set();
  for (const { units } of client) {
    for (const { sourceFile, scope } of units) {
      for (const statement of sourceFile.statements) {
        if (!ts.isFunctionDeclaration(statement) || !statement.name || !statement.body) continue;
        const names = statement.parameters.map((p) => (ts.isIdentifier(p.name) ? p.name.text : ""));
        const inner = bind(scope, names, slot);
        const calls = [];
        visit(ts, statement.body, (node) => {
          if (!ts.isCallExpression(node)) return;
          const method = apiMethod(ts, node);
          const text = method && textOf(ts, node.arguments[0], inner);
          if (text && text.includes("\u0002")) calls.push({ method, text });
        });
        if (calls.length > 0) {
          factories.set(statement.name.text, calls);
          bodies.add(statement.body);
        }
      }
    }
  }
  return { factories, bodies };
}

function insideAny(node, bodies) {
  for (let current = node.parent; current; current = current.parent) {
    if (bodies.has(current)) return true;
  }
  return false;
}

function factoryCalls(ts, node, calls, scope) {
  const args = node.arguments.map((arg) => textOf(ts, arg, scope));
  const found = [];
  for (const { method, text } of calls) {
    let unresolved = false;
    const filled = text.replace(/\u0002(\d+)\u0002/g, (_, index) => {
      const value = args[Number(index)];
      if (value === null || value === undefined) unresolved = true;
      return value ?? PARAM;
    });
    if (unresolved) return null;
    found.push({ method, path: apiPath(filled) });
  }
  return found;
}

/** Every call the client makes, and the calls whose address it could not read. */
export function readCalls(ts, client) {
  const { factories, bodies } = findFactories(ts, client);
  const calls = [];
  const unread = [];
  const opensServed = new Set();
  for (const { file, units } of client) {
    for (const { sourceFile, scope } of units) {
      visit(ts, sourceFile, (node) => {
        if (!ts.isCallExpression(node) || insideAny(node, bodies)) return;
        const method = apiMethod(ts, node);
        if (method) {
          const texts = choicesOf(ts, node.arguments[0], scope);
          if (texts.length === 0) {
            const argument = node.arguments[0]?.getText() ?? "";
            unread.push({ file, call: `${node.expression.getText()}(${argument})` });
          }
          for (const text of texts) calls.push({ method, path: apiPath(text), file });
          return;
        }
        const callee = unwrap(ts, node.expression);
        if (isApiUrl(ts, callee)) opensServed.add(file);
        if (ts.isIdentifier(callee) && factories.has(callee.text)) {
          const expanded = factoryCalls(ts, node, factories.get(callee.text), scope);
          if (expanded === null) unread.push({ file, call: callee.text });
          else calls.push(...expanded.map((call) => ({ ...call, file })));
        }
      });
    }
  }
  return { calls, unread, opensServed: [...opensServed].sort(compare) };
}

/**
 * The endpoints no call lands on, the ones reached only through an address the
 * server writes, and the calls that land on no endpoint at all.
 */
export function matchEndpoints(endpoints, calls, served) {
  const resolve = endpointResolver(endpoints);
  const called = new Set();
  const unmatched = [];
  for (const call of calls) {
    const endpoint = resolve(call.method, call.path);
    if (endpoint) called.add(endpoint);
    else unmatched.push(call);
  }
  const byServer = new Map();
  for (const address of served) {
    const endpoint = resolve("GET", address.path);
    if (!endpoint || called.has(endpoint)) continue;
    if (!byServer.has(endpoint)) byServer.set(endpoint, new Set());
    byServer.get(endpoint).add(address.file);
  }
  return {
    orphans: endpoints.filter((endpoint) => !called.has(endpoint) && !byServer.has(endpoint)),
    served: [...byServer].map(([endpoint, files]) => ({ endpoint, files: [...files].sort(compare) })),
    unmatched,
  };
}
