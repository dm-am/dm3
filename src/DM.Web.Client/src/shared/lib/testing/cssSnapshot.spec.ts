/**
 * @vitest-environment node
 */

/**
 * What the CSS gate is allowed to forget. A style refactor is accepted as
 * invisible when the snapshots of the two builds are equal, so every
 * normalization below is a class of change the gate lets through unseen: the
 * cases on the "differ" side are the ones it must never absorb, because the
 * cascade reads them.
 */
import { afterAll, describe, expect, it } from "vitest";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import {
  canonicalCss,
  compareSnapshots,
  cssSnapshot,
  relabelScopeIds,
} from "./cssSnapshot";

const same = (a: string, b: string) =>
  expect(canonicalCss(a)).toEqual(canonicalCss(b));
const differ = (a: string, b: string) =>
  expect(canonicalCss(a)).not.toEqual(canonicalCss(b));

describe("a stylesheet as the gate reads it", () => {
  it("does not see whitespace, comments or the optional last semicolon", () => {
    same(
      ".a, .b { color: red; margin: 0 auto }\n@media (max-width: 600px) { .a { padding: 0 } }",
      [
        "/* the card */",
        ".a,.b{",
        "  color : red ;",
        "  margin:0  auto; /* centred */",
        "}",
        "@media (max-width: 600px){.a{padding:0;}}",
      ].join("\n"),
    );
  });

  it("does not see a block that a comment alone kept alive", () => {
    // Sass keeps `.a { /* note */ }` as a rule; without the comment it emits
    // nothing. An @media that held only such a rule goes with it.
    same(".a{/* note */}@media print{.b{/* note */}}.c{x:1}", ".c{x:1}");
    differ(".a{x:1}", ".a{}");
  });

  it("sees the order of declarations", () => {
    // The later declaration wins, so the swap is visible here, and an
    // independent pair is kept in order too: the gate does not decide which
    // pairs interact.
    differ(".a{margin:0;margin-top:4px}", ".a{margin-top:4px;margin:0}");
    differ(".a{color:red;padding:0}", ".a{padding:0;color:red}");
  });

  it("sees the order of rules", () => {
    differ(".a{color:red}.b{color:blue}", ".b{color:blue}.a{color:red}");
  });

  it("sees a changed value", () => {
    differ(".a{color:#c00}", ".a{color:#c01}");
    differ(".a{transition:opacity .2s}", ".a{transition:opacity .3s}");
  });

  it("keeps a space that is a combinator or a separator", () => {
    differ(".a .b{x:1}", ".a.b{x:1}");
    differ("a :hover{x:1}", "a:hover{x:1}");
    differ(".a{margin:0 auto}", ".a{margin:0auto}");
  });

  it("keeps strings and unquoted urls as they are written", () => {
    // Neither "/*" nor ";" inside them is a comment or the end of a statement.
    expect(
      canonicalCss('.a{content:"x /* y */ ; z";background:url(data:a;b/*c*/)}'),
    ).toEqual([
      ".a{",
      'content:"x /* y */ ; z";',
      "background:url(data:a;b/*c*/);",
      "}",
    ]);
    differ('.a{content:"a  b"}', '.a{content:"a b"}');
  });
});

describe("scope ids", () => {
  it("are renumbered, so a component edited elsewhere reads the same", () => {
    expect(
      relabelScopeIds(
        ".a[data-v-1a2b3c4d] .b[data-v-00ff00ff],.c[data-v-1a2b3c4d]",
      ),
    ).toBe(
      relabelScopeIds(
        ".a[data-v-99999999] .b[data-v-12345678],.c[data-v-99999999]",
      ),
    );
  });

  it("are renumbered in the names of a component's keyframes too", () => {
    // Vue gives each @keyframes of a scoped style the suffix -<id>, and the
    // animation that uses it the same name: one id, three places.
    same(
      relabelScopeIds(
        ".a[data-v-1a2b3c4d]{animation:spin-1a2b3c4d 1s}@keyframes spin-1a2b3c4d{to{x:1}}",
      ),
      relabelScopeIds(
        ".a[data-v-99999999]{animation:spin-99999999 1s}@keyframes spin-99999999{to{x:1}}",
      ),
    );
    // A sheet can hold the keyframes of a component and none of its selectors.
    same(
      relabelScopeIds(
        "@keyframes spin-1a2b3c4d{to{x:1}}.b{animation-name:spin-1a2b3c4d}",
      ),
      relabelScopeIds(
        "@keyframes spin-99999999{to{x:1}}.b{animation-name:spin-99999999}",
      ),
    );
  });

  it("are renumbered under :slotted", () => {
    expect(relabelScopeIds(".a[data-v-1a2b3c4d-s]{x:1}")).toBe(
      relabelScopeIds(".a[data-v-99999999-s]{x:1}"),
    );
  });

  it("leave an eight-digit hex that is no scope id alone", () => {
    // Only the ids the sheet uses as scope ids are relabeled: a changed hash
    // in a file name after a dash is a changed file.
    differ(
      relabelScopeIds(".a[data-v-1a2b3c4d]{background:url(/x-deadbeef.png)}"),
      relabelScopeIds(".a[data-v-1a2b3c4d]{background:url(/x-deadbeee.png)}"),
    );
  });

  it("still tell two components apart", () => {
    // The rules of one component moved under another: the selectors are the
    // same text, the elements they reach are not.
    expect(
      relabelScopeIds(".a[data-v-11111111]{x:1}.b[data-v-22222222]{y:2}"),
    ).not.toBe(
      relabelScopeIds(".a[data-v-11111111]{x:1}.b[data-v-11111111]{y:2}"),
    );
    // Keyframes that moved to the other component.
    expect(
      relabelScopeIds(
        ".a[data-v-11111111]{x:1}.b[data-v-22222222]{animation:spin-11111111}",
      ),
    ).not.toBe(
      relabelScopeIds(
        ".a[data-v-11111111]{x:1}.b[data-v-22222222]{animation:spin-22222222}",
      ),
    );
  });
});

describe("the snapshot of a build", () => {
  const root = mkdtempSync(join(tmpdir(), "css-snapshot-spec-"));
  afterAll(() => rmSync(root, { recursive: true, force: true }));

  let builds = 0;
  /** A build output with the given files, for the snapshot to read. */
  const build = (files: Record<string, string>) => {
    const outDir = join(root, `build-${(builds += 1)}`);
    for (const [path, content] of Object.entries(files)) {
      mkdirSync(dirname(join(outDir, path)), { recursive: true });
      writeFileSync(join(outDir, path), content);
    }
    return cssSnapshot(outDir);
  };

  /** A page with the given tags: an href links a sheet, a block is a style. */
  const page = (...tags: string[]) =>
    `<html><head><!-- <link rel="stylesheet" href="/old.css"> --><script>var s = '<style>.x{y:1}</style>';</script>${tags.map((tag) => (tag.startsWith("/") ? `<link rel="stylesheet" crossorigin href="${tag}">` : `<style>${tag}</style>`)).join("")}<link rel="icon" href="/favicon.ico"></head></html>`;
  /** Room imports Table and Paging, and both of them import Icon. */
  const manifest = (css: string[], imports = ["_Table.js", "_Paging.js"]) =>
    JSON.stringify({
      "src/pages/Room.vue": { file: "assets/Room.js", css, imports },
      "_Table.js": {
        file: "assets/Table.js",
        css: ["assets/Table.css"],
        imports: ["_Icon.js", "_util.js"],
      },
      "_Paging.js": {
        file: "assets/Paging.js",
        css: ["assets/Paging.css"],
        imports: ["_Icon.js"],
      },
      "_Icon.js": { file: "assets/Icon.js", css: ["assets/Icon.css"] },
      "_util.js": { file: "assets/util.js" },
    });
  const base = {
    "index.html": page(
      "html{text-size-adjust:100%}",
      "/assets/index.css",
      "/assets/fonts.css",
    ),
    ".vite/manifest.json": manifest(["assets/Room.css", "assets/Panel.css"]),
    "assets/index.css": ".app[data-v-aaaaaaaa]{color:red;padding:0}",
    "assets/fonts.css": "@font-face{font-family:X;src:url(/assets/x.woff2)}",
    "assets/Room.css": ".room[data-v-bbbbbbbb]{margin:0}",
    "assets/Panel.css": ".panel{border:0}",
  };

  it("is equal for a build that differs only in layout and scope ids", () => {
    const head = build({
      ...base,
      "assets/index.css":
        ".app[data-v-cccccccc] {\n  color: red;\n  padding: 0;\n}\n",
      "assets/Room.css": "/* room */\n.room[data-v-dddddddd] { margin: 0 }",
    });
    expect(compareSnapshots(build(base), head)).toEqual([]);
  });

  it("names the sheet and the line where two declarations swapped", () => {
    const head = build({
      ...base,
      "assets/index.css": ".app[data-v-aaaaaaaa]{padding:0;color:red}",
    });
    expect(compareSnapshots(build(base), head)).toEqual([
      {
        section: "sheet assets/index.css",
        change: "changed",
        line: 1,
        before: [".app[data-v-~1]{"],
        base: ["color:red;", "padding:0;", "}"],
        head: ["padding:0;", "color:red;", "}"],
      },
    ]);
  });

  it("sees the order the page links its sheets in", () => {
    const head = build({
      ...base,
      "index.html": page(
        "html{text-size-adjust:100%}",
        "/assets/fonts.css",
        "/assets/index.css",
      ),
    });
    expect(compareSnapshots(build(base), head).map((d) => d.section)).toEqual([
      "page index.html",
    ]);
  });

  it("sees a style the page holds, and where it stands", () => {
    const changed = build({
      ...base,
      "index.html": page(
        "html{text-size-adjust:none}",
        "/assets/index.css",
        "/assets/fonts.css",
      ),
    });
    const moved = build({
      ...base,
      "index.html": page(
        "/assets/index.css",
        "/assets/fonts.css",
        "html{text-size-adjust:100%}",
      ),
    });
    for (const head of [changed, moved]) {
      expect(compareSnapshots(build(base), head).map((d) => d.section)).toEqual(
        ["page index.html"],
      );
    }
    expect(build(base)).toContain("<style>");
  });

  it("does not read a tag in a comment or a script as one the page has", () => {
    expect(
      build(base).filter(
        (line) => line.includes("old.css") || line.includes(".x{"),
      ),
    ).toEqual([]);
  });

  it("sees the order a chunk brings its sheets in", () => {
    const head = build({
      ...base,
      ".vite/manifest.json": manifest(["assets/Panel.css", "assets/Room.css"]),
    });
    expect(compareSnapshots(build(base), head).map((d) => d.section)).toEqual([
      "chunk assets/Room.js",
    ]);
  });

  it("sees the order the chunks a chunk imports bring their sheets in", () => {
    // Loading Room puts the sheets of what it imports first, depth first and
    // each once, then its own: Icon (through Table), Table, Paging, Room,
    // Panel. With the imports the other way round Paging comes before Table.
    expect(build(base).join("\n")).toContain(
      [
        "### chunk assets/Room.js",
        "assets/Icon.css",
        "assets/Table.css",
        "assets/Paging.css",
        "assets/Room.css",
        "assets/Panel.css",
      ].join("\n"),
    );
    const head = build({
      ...base,
      ".vite/manifest.json": manifest(
        ["assets/Room.css", "assets/Panel.css"],
        ["_Paging.js", "_Table.js"],
      ),
    });
    expect(compareSnapshots(build(base), head).map((d) => d.section)).toEqual([
      "chunk assets/Room.js",
    ]);
  });

  it("sees a sheet that appeared or went away", () => {
    const { "assets/Panel.css": _panel, ...rest } = base;
    const head = build({ ...rest, "assets/Extra.css": ".x{y:1}" });
    expect(
      compareSnapshots(build(base), head).map(({ section, change }) => [
        section,
        change,
      ]),
    ).toEqual([
      ["sheet assets/Extra.css", "added"],
      ["sheet assets/Panel.css", "removed"],
    ]);
  });
});
