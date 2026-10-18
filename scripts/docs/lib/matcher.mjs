// Which route an address opens. The syntax of a route path is vue-router's and
// strictly regular (static segments, `:param`, `:param(regexp)` with an optional
// `?`, `*` or `+`), so it is compiled to a regular expression here; when several
// routes take an address, the one with more static segments wins, which is the
// order vue-router ranks them in.

const SEGMENT = /^:(\w+)(?:\(((?:[^()]|\([^()]*\))*)\))?([?*+])?$/;

function escape(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** One segment of a route path: its pattern and its rank (static first, a splat last). */
function compileSegment(segment) {
  const param = SEGMENT.exec(segment);
  if (!param) return { pattern: `/${escape(segment)}`, rank: 4 };
  const [, , custom, modifier] = param;
  const body = custom ? `(?:${custom})` : "[^/]+";
  if (modifier === "*") return { pattern: `(?:/${body}(?:/${body})*)?`, rank: 0 };
  if (modifier === "+") return { pattern: `/${body}(?:/${body})*`, rank: 0 };
  if (modifier === "?") return { pattern: `(?:/${body})?`, rank: 1 };
  return { pattern: `/${body}`, rank: custom ? 3 : 2 };
}

export function compileRoutePath(path) {
  const segments = path.split("/").filter((s) => s !== "");
  const compiled = segments.map(compileSegment);
  const source = compiled.map((c) => c.pattern).join("") || "";
  return {
    regex: new RegExp(`^${source}/?$`),
    // The root is one static segment for the ranking: "/" outranks a splat.
    score: compiled.length > 0 ? compiled.map((c) => c.rank) : [4],
  };
}

function better(a, b) {
  for (let i = 0; i < Math.min(a.length, b.length); i++) {
    if (a[i] !== b[i]) return a[i] > b[i];
  }
  return a.length > b.length;
}

/** The address part of a link: no query, no fragment, no trailing slash. */
export function addressOf(text) {
  const cut = text.search(/[?#]/);
  const path = cut === -1 ? text : text.slice(0, cut);
  return path === "" ? "/" : path;
}

/**
 * A resolver over the rows of the route table. An unknown part of the address
 * (PARAM) fills a parameter but never a static segment, so a link built from a
 * variable is credited to a route only when the rest of it says which.
 */
export function routeResolver(rows) {
  const compiled = rows.map((row) => ({ row, ...compileRoutePath(row.path) }));
  return (text) => {
    const address = addressOf(text);
    if (!address.startsWith("/")) return undefined;
    let best;
    for (const candidate of compiled) {
      if (!candidate.regex.test(address)) continue;
      if (!best || better(candidate.score, best.score)) best = candidate;
    }
    return best?.row;
  };
}

/** Server routes in the snapshot spelling (`/v1/games/{id}`), the same way round. */
export function endpointResolver(endpoints) {
  const compiled = endpoints.map((endpoint) => {
    const segments = endpoint.path.split("/").filter((s) => s !== "");
    const pattern = segments
      .map((s) => (/^\{[^}]*\}$/.test(s) ? "/[^/]+" : `/${escape(s)}`))
      .join("");
    return {
      endpoint,
      regex: new RegExp(`^${pattern}$`),
      score: segments.map((s) => (s.startsWith("{") ? 2 : 4)),
    };
  });
  return (method, path) => {
    let best;
    for (const candidate of compiled) {
      if (candidate.endpoint.method !== method || !candidate.regex.test(path)) continue;
      if (!best || better(candidate.score, best.score)) best = candidate;
    }
    return best?.endpoint;
  };
}

