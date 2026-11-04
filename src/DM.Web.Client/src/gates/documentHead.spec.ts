/**
 * @vitest-environment node
 */

/**
 * The static head is the site's identity for everything that never runs the
 * bundle: a crawler, a link preview in a messenger, the tab before the first
 * paint. With no server render one document answers every address, so the
 * address-bound tags stay out. The head holds one title, one description and
 * one image, and composes the title by `formatDocumentTitle`, the rule every
 * tab title follows, so the static document and the router cannot disagree.
 * index.html lies outside the copy-rules scan of `src/`, so the two signs that
 * scan forbids are checked on the head copy here.
 */
import { describe, it, expect } from "vitest";
import { existsSync, readFileSync } from "fs";
import { join } from "path";
import { formatDocumentTitle } from "../shared/lib/composables/useDocumentTitle";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

// src -> client root, where index.html and public/ live side by side
const CLIENT_ROOT = join(CLIENT_SRC, "..");

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
    // The same head answers every address, so og:url and a canonical link
    // belong to a renderer that knows the address.
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
