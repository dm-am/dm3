/**
 * @vitest-environment node
 */

/**
 * Everything that decides how long the first paint waits for the font is
 * outside the bundle: the @font-face block, the document head, and the two
 * nginx hops the file travels.
 *
 * Without `font-display` the engine holds the text it is about to paint for up
 * to three seconds and then paints it — a visitor on a slow first load watches
 * an empty page frame. Without a preload the request for the font leaves only
 * after the stylesheet has been fetched and parsed, a full round trip behind.
 * And without the type in gzip_types the file goes over the wire as it is:
 * TrueType carries no compression of its own, and this family is a megabyte of
 * it.
 *
 * The faces are still TrueType — converting them to WOFF2 is a binary step and
 * not something a source check can assert. What it can assert is that the
 * delivery around them does not quietly go back to what it was.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "fs";
import { basename, dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// src -> DM.Web.Client -> src -> repository root
const CLIENT_ROOT = resolve(HERE, "..");
const REPO_ROOT = resolve(CLIENT_ROOT, "..", "..");

const fontsSass = readFileSync(join(HERE, "assets/styles/Fonts.sass"), "utf8");
const indexHtml = readFileSync(join(CLIENT_ROOT, "index.html"), "utf8");

/** Every @font-face block of the stylesheet, its own body only. */
const allFaces = (): string[] =>
  fontsSass
    .split("@font-face")
    .slice(1)
    .map((body) => body.split(/\n(?=\S)/)[0]);

/** The family a block declares. */
const familyOf = (body: string): string =>
  /font-family:\s*"([^"]+)"/.exec(body)?.[1] ?? "";

/** The faces of PT Sans itself, the files the page waits for. */
const faces = (): string[] =>
  allFaces().filter((body) => familyOf(body) === "PT Sans");

/** The family the first frame is drawn in until PT Sans arrives. */
const FALLBACK = "PT Sans Fallback";
const fallbackFaces = (): string[] =>
  allFaces().filter((body) => familyOf(body) === FALLBACK);

/** "400 normal": the weight and the style a block answers for. */
const slotOf = (body: string): string =>
  `${/font-weight:\s*(\d+)/.exec(body)?.[1]} ${/font-style:\s*(\w+)/.exec(body)?.[1]}`;

/** File name of the face a block points at. */
const faceFile = (body: string): string => {
  const url = /url\("([^"]+)"\)/.exec(body);
  if (!url) throw new Error(`a @font-face block declares no src: ${body}`);
  return basename(url[1]);
};

/** Every <link rel="preload"> of the document, as raw tag text. */
const preloads = (): string[] =>
  [...indexHtml.matchAll(/<link\b[^>]*>/g)]
    .map((found) => found[0])
    .filter((tag) => /rel="preload"/.test(tag));

/** The gzip_types directive of an nginx config, as one string. */
const gzipTypes = (file: string): string => {
  const directive = /gzip_types([^;]*);/.exec(readFileSync(file, "utf8"));
  if (!directive) throw new Error(`${file} declares no gzip_types`);
  return directive[1];
};

describe("font delivery", () => {
  it("reads the four faces it is about to judge", () => {
    // An empty list would let every assertion below pass by checking nothing.
    expect(faces().length).toBe(4);
  });

  it("gives every face a display strategy", () => {
    const offenders = faces()
      .filter((body) => !/font-display:\s*\S+/.test(body))
      .map(
        (body) =>
          `${faceFile(body)}: no font-display, so the engine blocks the text for up to three seconds`,
      );
    expect(offenders).toEqual([]);
  });

  it("asks for the faces the page frame is set in, and only those", () => {
    // Regular and bold: Reset.sass puts the family on the root elements, so
    // both are on the critical path of every address. The italics are content,
    // not frame, and preloading them would compete with the two that are.
    const critical = faces()
      .filter((body) => /font-style:\s*normal/.test(body))
      .map(faceFile);
    const asked = preloads();

    expect(asked.length).toBe(critical.length);
    for (const file of critical) {
      const tag = asked.find((one) => one.includes(file));
      expect(tag, `index.html does not preload ${file}`).toBeDefined();
      // as="font" picks the right priority and the right cache bucket;
      // crossorigin is not optional — a font is fetched in anonymous CORS mode,
      // and a preload without it warms an entry @font-face will never reuse.
      expect(tag).toContain('as="font"');
      expect(tag).toContain("crossorigin");
    }
  });

  /**
   * The first frame is drawn in the next family of the stack, and the
   * system's sans-serif set the headings of the site 14 percent wider than PT
   * Sans: a heading of one line took two until the file landed. The fallback
   * family is the system's Arial scaled to PT Sans (Fonts.sass), one face for
   * every face of PT Sans, so no weight or style is drawn from an unfitted
   * face or synthesised from another one.
   */
  it("fits a fallback face to every face of PT Sans", () => {
    const fitted = fallbackFaces();

    expect(fitted.map(slotOf).sort()).toEqual(faces().map(slotOf).sort());
    for (const body of fitted) {
      // Installed faces only: a file of its own would be one more thing the
      // first frame waits for.
      expect(body, slotOf(body)).toMatch(/src:\s*local\(/);
      expect(body, slotOf(body)).not.toContain("url(");
      for (const descriptor of [
        "size-adjust",
        "ascent-override",
        "descent-override",
        "line-gap-override",
      ]) {
        expect(body, `${slotOf(body)}: ${descriptor}`).toMatch(
          new RegExp(`${descriptor}:\\s*\\S`),
        );
      }
    }
  });

  /**
   * A fitted face nobody names is never drawn. The family stands between PT
   * Sans and the generic family in every stack of the stylesheets that sets
   * PT Sans by name.
   */
  it("names the fallback family in every stack that names PT Sans", () => {
    const stylesDir = join(HERE, "assets/styles");
    const stacks = readdirSync(stylesDir)
      .filter((file) => file.endsWith(".sass"))
      .flatMap((file) =>
        readFileSync(join(stylesDir, file), "utf8")
          .split("\n")
          // A stack, not the name of a face: "PT Sans" followed by more.
          .filter((line) => /^\s*font-family:\s*"PT Sans",/.test(line))
          .map((line) => `${file}: ${line.trim()}`),
      );

    expect(stacks.length, "no stack names PT Sans").toBeGreaterThan(0);
    const unfitted = stacks.filter(
      (line) => !line.endsWith(`"PT Sans", "${FALLBACK}", sans-serif`),
    );
    expect(unfitted).toEqual([]);
  });

  it("compresses the files on both hops", () => {
    const hops = [
      join(CLIENT_ROOT, "nginx.conf"),
      join(REPO_ROOT, "docker/nginx/nginx.conf"),
    ];
    const offenders = hops
      .filter((file) => !gzipTypes(file).includes("font/ttf"))
      .map((file) => `${file}: gzip_types omits font/ttf`);
    expect(offenders).toEqual([]);
  });

  /**
   * gzip_types matches the response's content type, and nginx derives that from
   * mime.types, where the stock file names woff and woff2 and stops. A .ttf
   * therefore left as application/octet-stream, no gzip_types entry could ever
   * match it, and the megabyte the entry above was added for kept travelling
   * raw with both configurations reading as though it did not. Measured in the
   * image: `nginx:1.27-alpine` answers a .ttf with Content-Length 278612 and no
   * Content-Encoding until the mapping is declared.
   */
  it("gives the font a type gzip_types can match, on both hops", () => {
    const hops = [
      join(CLIENT_ROOT, "nginx.conf"),
      join(REPO_ROOT, "docker/nginx/nginx.conf"),
    ];
    const offenders = hops
      .filter(
        (file) =>
          !/types\s*\{[^}]*\bfont\/ttf\s+ttf\s*;/s.test(
            readFileSync(file, "utf8"),
          ),
      )
      .map(
        (file) =>
          `${file}: no types block maps ttf, so the file is served as ` +
          `application/octet-stream and gzip_types font/ttf matches nothing`,
      );
    expect(offenders).toEqual([]);
  });
});
