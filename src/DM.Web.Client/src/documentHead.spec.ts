/**
 * @vitest-environment node
 */

/**
 * The static head is the site's identity for everything that never runs the
 * bundle: a crawler, a link preview in a messenger, the tab before the first
 * paint.
 *
 * It cannot be more than that here. There is no server render and no
 * prerender, so one document answers every address, and a link to a game, to a
 * topic and to the root unfolds into the same card. A per-address preview
 * needs a renderer that knows the address — this file does not pretend
 * otherwise, and keeps the address-bound tags out instead of filling them with
 * the root's values.
 *
 * What was fixable is the drift inside the head, and the head had it: the
 * document called the site one thing, og:title called it another, and the
 * description came in two versions differing by a brand prefix. Three names
 * for one site, and nothing to notice a fourth.
 *
 * So the head holds one title, one description, one image, and composes the
 * title by the rule every tab title already follows: the distinguishing
 * segment first, the brand behind the one separator. The brand and the
 * composition come from `formatDocumentTitle` rather than from a copy kept
 * here, so the static document and the router cannot disagree about them — the
 * title of the root route is the one that has to arrive at this same string.
 *
 * index.html also lies outside the copy-rules scan, which walks `src/` only,
 * so the two signs that scan forbids are checked on the head copy here.
 */
import { describe, it, expect } from "vitest";
import { existsSync, readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { formatDocumentTitle } from "./shared/lib/composables/useDocumentTitle";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> client root, where index.html and public/ live side by side
const CLIENT_ROOT = join(HERE, "..");

const indexHtml = readFileSync(join(CLIENT_ROOT, "index.html"), "utf8");

// Escaped, as in shared/lib/copy-rules.spec.ts: a file that checks for a sign
// must not be a place where the sign is spelled.
const EM_DASH = "\u2014";
const YO = "\u0451";
const BRAND = formatDocumentTitle("");

/** Every `<meta>` of the document, read as an attribute map. */
const metas: Record<string, string>[] = [
  ...indexHtml.matchAll(/<meta\b[^>]*>/g),
].map(([tag]) =>
  Object.fromEntries(
    [...tag.matchAll(/([\w:.-]+)="([^"]*)"/g)].map(([, name, value]) => [
      name,
      value,
    ]),
  ),
);

/** Contents of every meta addressed by `name` or by `property`. */
const contentsOf = (attribute: "name" | "property", key: string): string[] =>
  metas
    .filter((meta) => meta[attribute] === key)
    .map((meta) => meta.content ?? "");

const titles = [...indexHtml.matchAll(/<title>([\s\S]*?)<\/title>/g)].map(
  (match) => match[1].trim(),
);

const [title = ""] = titles;
const [description = ""] = contentsOf("name", "description");
const [ogTitle = ""] = contentsOf("property", "og:title");
const [ogDescription = ""] = contentsOf("property", "og:description");
const [ogImage = ""] = contentsOf("property", "og:image");
const [ogSiteName = ""] = contentsOf("property", "og:site_name");

describe("static head of the document", () => {
  it("declares one title, one description and one image", () => {
    const counts: Array<[string, number]> = [
      ["<title>", titles.length],
      ['meta name="description"', contentsOf("name", "description").length],
      ['meta property="og:title"', contentsOf("property", "og:title").length],
      [
        'meta property="og:description"',
        contentsOf("property", "og:description").length,
      ],
      ['meta property="og:image"', contentsOf("property", "og:image").length],
      [
        'meta property="og:site_name"',
        contentsOf("property", "og:site_name").length,
      ],
    ];

    expect(
      counts
        .filter(([, count]) => count !== 1)
        .map(([what, count]) => `${what}: ${count}`),
    ).toEqual([]);
  });

  it("composes the title the way every tab title is composed", () => {
    // og:title is the page segment, <title> is that segment plus the brand,
    // og:site_name is the brand alone: one string, three tags, no second name
    // for the site.
    expect(ogTitle).not.toBe("");
    expect(title).toBe(formatDocumentTitle(ogTitle));
    expect(ogSiteName).toBe(BRAND);
  });

  it("says the same sentence to the search engine and to the preview", () => {
    expect(description).not.toBe("");
    expect(ogDescription).toBe(description);
  });

  it("points the preview at an image that ships", () => {
    expect(
      ogImage,
      "og:image must be a site-root path, so it resolves the same from every address",
    ).toMatch(/^\//);
    expect(existsSync(join(CLIENT_ROOT, "public", ogImage))).toBe(true);
  });

  it("carries no value that belongs to one address", () => {
    // The same head answers every address: og:url and a canonical link name
    // the right page once and the wrong one everywhere else. They belong to
    // the renderer that will know the address, and until it exists their
    // absence is the honest answer.
    const addressBound = [
      ...metas
        .filter(
          (meta) => meta.property === "og:url" || meta.name === "twitter:url",
        )
        .map((meta) => `meta ${meta.property ?? meta.name}`),
      ...(/<link\b[^>]*rel="canonical"/.test(indexHtml)
        ? ['link rel="canonical"']
        : []),
    ];

    expect(addressBound).toEqual([]);
  });

  it("keeps the forbidden signs out of the head copy", () => {
    const forbidden: Array<[string, string]> = [
      [EM_DASH, "em dash"],
      [YO, "the letter yo"],
    ];
    const copy = { title, description, ogTitle, ogDescription, ogSiteName };

    const offenders = Object.entries(copy).flatMap(([where, text]) =>
      forbidden
        .filter(([sign]) => text.includes(sign))
        .map(([, what]) => `${where}: ${what}`),
    );

    expect(offenders).toEqual([]);
  });
});
