#!/usr/bin/env node
// Both directions of the comment comparison: every edit below that touches
// only comments must compare equal, and every edit that touches code, or a
// comment some tool reads, must not. Then the command itself, against copies
// of real files. Run: node scripts/visual/compare-code.test.mjs
//
// A lexer that drops too much passes the first half and fails the second;
// one that keeps comments as code fails the first. Each half alone would
// pass a broken lexer.

import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { git, readBlobs } from "./compare/git.mjs";
import { LexError, firstDifference } from "./compare/lexing.mjs";
import { tokensOf } from "./compare/tokens.mjs";

const HERE = dirname(fileURLToPath(import.meta.url));

// [file name, before, after, why]
const SAME = [
  [
    "a.ts",
    "const a = 1; // why\nconst b = 2;",
    "const a = 1;\nconst b = 2;",
    "line comment",
  ],
  [
    "a.ts",
    "/** Doc. */\nexport function f(x: number) { return x; }",
    "export function f(x: number) { return x; }",
    "JSDoc",
  ],
  ["a.ts", "f(a, /* inline */ b);", "f(a, b);", "comment inside an expression"],
  [
    "a.ts",
    "const t = `${/* c */ x}y`;",
    "const t = `${x}y`;",
    "comment inside a template hole",
  ],
  [
    "a.ts",
    "const r = /\\/\\//g; // c",
    "const r = /\\/\\//g;",
    "regex that holds //",
  ],
  [
    "a.ts",
    "// we keep eslint happy here\nconst a = 1;",
    "const a = 1;",
    "prose that mentions a directive",
  ],
  [
    "a.ts",
    "if (a) {\n  // nothing\n}",
    "if (a) {\n}",
    "comment alone in a block",
  ],
  [
    "a.ts",
    "const a = 1;\r\n",
    "\uFEFFconst a = 1;\n",
    "line ends and byte order mark",
  ],
  [
    "a.mjs",
    "import x from 'y'; // c\nexport default x;",
    "import x from 'y';\nexport default x;",
    "JavaScript",
  ],
  [
    "t.json",
    '{\n  // why\n  "a": 1\n}',
    '{\n  "a": 1\n}',
    "JSON with comments",
  ],
  [
    "c.vue",
    '<script setup lang="ts">\n// why\nconst a = 1;\n</script>\n<template>\n  <!-- block -->\n  <p>{{ a }}</p>\n</template>',
    '<script setup lang="ts">\nconst a = 1;\n</script>\n<template>\n  <p>{{ a }}</p>\n</template>',
    "script and template comments",
  ],
  [
    "c.vue",
    "<template><p>one <!-- c --> two</p><b>x</b> <!-- d --> <i>y</i></template>",
    "<template><p>one  two</p><b>x</b>  <i>y</i></template>",
    "template comments between inline content",
  ],
  [
    "c.vue",
    '<template><p /></template>\n<style lang="sass" scoped>\n// note\n.a\n  color: red // why\n  /* loud\n     continued */\n  padding: 0\n</style>',
    '<template><p /></template>\n<style lang="sass" scoped>\n.a\n  color: red\n  padding: 0\n</style>',
    "Sass comments in a style block",
  ],
  [
    "s.sass",
    "//\n  indented silent comment\n.a\n  margin: 0  auto\n\n",
    ".a\n  margin: 0 auto",
    "Sass comment block and spacing",
  ],
  [
    "s.sass",
    ".a\n  background: url(//cdn.x/y.png) // c",
    ".a\n  background: url(//cdn.x/y.png)",
    "Sass url that holds //",
  ],
  [
    "t.css",
    ":root {\n  /* why */\n  --x: 1px;\n}",
    ":root{--x:1px}",
    "CSS comment and layout",
  ],
  [
    "a.cs",
    "/// <summary>Doc.</summary>\npublic int A; // why\n/* block */",
    "public int A;",
    "C# comments and XML doc",
  ],
  ["a.cs", "#region Things\nint a;\n#endregion", "int a;", "regions"],
  [
    "a.cs",
    'var u = "http://x"; // c',
    'var u = "http://x";',
    "C# string that holds //",
  ],
  [
    "a.cs",
    'var p = @"C:\\dir\\"; // c',
    'var p = @"C:\\dir\\";',
    "verbatim string ending in a backslash",
  ],
  [
    "a.cs",
    'var s = $"{a} {b:N2} {(c ? "x" : "y")}"; // c',
    'var s = $"{a} {b:N2} {(c ? "x" : "y")}";',
    "interpolated string",
  ],
  [
    "a.cs",
    'var s = """\n  // not a comment\n  """; // c',
    'var s = """\n  // not a comment\n  """;',
    "raw string",
  ],
  [
    "a.cs",
    'var s = $$"""{{x}} {y} "{{z}}" """; /* c */',
    'var s = $$"""{{x}} {y} "{{z}}" """;',
    "interpolated raw string",
  ],
  [
    "a.cs",
    "var c = '\"'; // c\nvar d = '\\''; // d",
    "var c = '\"';\nvar d = '\\'';",
    "character literals",
  ],
  [
    "a.cs",
    "#pragma warning disable CA1822 // why\nint a;",
    "#pragma warning disable CA1822\nint a;",
    "comment after a pragma",
  ],
];

const DIFFER = [
  ["a.ts", "const a = 1;", "const a = 2;", "a changed value"],
  ["a.ts", "f(a, b);", "f(b, a);", "swapped arguments"],
  [
    "a.ts",
    "function f() {\n  return\n  1;\n}",
    "function f() {\n  return 1;\n}",
    "a line break that ends a return",
  ],
  [
    "a.ts",
    'const a = "// x";',
    'const a = "";',
    "comment-like text inside a string",
  ],
  ["a.ts", "const a = 'x';", 'const a = "x";', "quote style"],
  [
    "a.ts",
    "const v = /* #__PURE__ */ make();",
    "const v = make();",
    "bundler annotation",
  ],
  [
    "a.ts",
    "// eslint-disable-next-line no-console\nconsole.log(1);",
    "console.log(1);",
    "eslint directive",
  ],
  [
    "a.ts",
    "// @ts-expect-error: wrong on purpose\nconst a: number = '';",
    "const a: number = '';",
    "TypeScript directive",
  ],
  [
    "a.ts",
    "await import(/* @vite-ignore */ url);",
    "await import(url);",
    "vite directive",
  ],
  [
    "a.ts",
    "/** @vitest-environment node */\nimport 'x';",
    "import 'x';",
    "test environment pragma",
  ],
  ["a.ts", "/* v8 ignore next */\nf();", "f();", "coverage pragma"],
  ["a.mjs", "#!/usr/bin/env node\nf();", "f();", "shebang"],
  ["t.json", '{ "a": 1 }', '{ "a": 2 }', "JSON value"],
  [
    "c.vue",
    "<template><p>one</p></template>",
    "<template><p>two</p></template>",
    "template text",
  ],
  [
    "c.vue",
    '<template>\n  <!-- eslint-disable-next-line vue/no-v-html -->\n  <p v-html="h" />\n</template>',
    '<template>\n  <p v-html="h" />\n</template>',
    "eslint directive in a template",
  ],
  [
    "c.vue",
    '<template><p /></template>\n<style lang="sass" scoped>\n.a\n  x: 1\n</style>',
    '<template><p /></template>\n<style lang="sass">\n.a\n  x: 1\n</style>',
    "scoped dropped",
  ],
  [
    "c.vue",
    '<template><p /></template>\n<style lang="sass">\n.a\n  color: red\n  padding: 0\n</style>',
    '<template><p /></template>\n<style lang="sass">\n.a\n  padding: 0\n  color: red\n</style>',
    "declarations swapped",
  ],
  [
    "s.sass",
    ".a\n  .b\n    x: 1",
    ".a\n  .b\n  x: 1",
    "indentation, which is nesting",
  ],
  [
    "s.sass",
    '.a\n  content: "a  b"',
    '.a\n  content: "a b"',
    "spacing inside a Sass string",
  ],
  [
    "s.sass",
    ".a\n  background: url(//cdn.x/a.png)",
    ".a\n  background: url(//cdn.x/b.png)",
    "the address in a Sass url",
  ],
  [
    "a.cs",
    'var u = "http://a";',
    'var u = "http://b";',
    "C# string that holds //",
  ],
  [
    "a.cs",
    'var p = @"C:\\a\\"; int x;',
    'var p = @"C:\\b\\"; int x;',
    "verbatim string",
  ],
  [
    "a.cs",
    'var s = $"{a}//{b}";',
    'var s = $"{a}//{c}";',
    "interpolated string that holds //",
  ],
  [
    "a.cs",
    'var s = $"{f("//")}"; int a = 1;',
    'var s = $"{f("//")}"; int a = 2;',
    "code after a string nested in a hole",
  ],
  ["t.css", ".a .b{x:1}", ".a.b{x:1}", "descendant combinator"],
  ["t.css", "/*! keep */\n.a{x:1}", ".a{x:1}", "CSS legal comment"],
  [
    "s.sass",
    "/*! keep\n  more */\n.a\n  x: 1",
    ".a\n  x: 1",
    "Sass legal comment",
  ],
  [
    "s.sass",
    ".a\n  x: 1 /*! keep */",
    ".a\n  x: 1",
    "Sass legal comment inside a line",
  ],
  [
    "c.vue",
    '<template><p /></template>\n<style lang="sass">\n/*! keep */\n.a\n  x: 1\n</style>',
    '<template><p /></template>\n<style lang="sass">\n.a\n  x: 1\n</style>',
    "legal comment in a Sass style block",
  ],
  ["a.cs", "int a = 1;", "int a = 2;", "C# value"],
  ["a.cs", "#pragma warning disable CA1822\nint a;", "int a;", "pragma"],
  ["a.cs", "#nullable enable\nint a;", "int a;", "nullable directive"],
  ["a.cs", "// <auto-generated />\nint a;", "int a;", "generated-code marker"],
  [
    "a.cs",
    'var s = """\n  // not a comment\n  """;',
    'var s = """\n  """;',
    "content of a raw string",
  ],
  [
    "a.cs",
    'var s = $"{a /* x */}";',
    'var s = $"{a}";',
    "comment inside an interpolation hole stays (conservative)",
  ],
  ["a.cs", "x = a >= b;", "x = a > = b;", "an operator split by a space"],
];

// Input the lexers must refuse rather than read past.
const REFUSE = [
  ["a.cs", 'var s = "unterminated;\nint a;', "unterminated string"],
  ["a.cs", "int a = `b`;", "a character C# has no token for"],
  ["a.ts", "const = ;", "a syntax error"],
];

let failures = 0;
const report = (ok, line) => {
  console.log(`${ok ? "ok  " : "BAD "} ${line}`);
  if (!ok) failures++;
};

const check = (expected, [name, before, after, why]) => {
  try {
    const found = firstDifference(
      tokensOf(name, Buffer.from(before)),
      tokensOf(name, Buffer.from(after)),
    );
    report(
      (found !== null) === (expected === "differ"),
      `${expected.padEnd(7)} ${name}: ${why}`,
    );
  } catch (error) {
    report(
      false,
      `${expected.padEnd(7)} ${name}: ${why} (threw: ${error.message})`,
    );
  }
};

for (const entry of SAME) check("same", entry);
for (const entry of DIFFER) check("differ", entry);
for (const [name, text, why] of REFUSE) {
  let ok = false;
  try {
    tokensOf(name, Buffer.from(text));
  } catch (error) {
    ok = error instanceof LexError;
  }
  report(ok, `refuse  ${name}: ${why}`);
}

// The command end to end: copies of real files, laid out like the
// repository, with comments stripped, with a value changed, next to a file
// git ignores, and with a published doc comment changed.
const real = "src/DM.Web.Client/vite.config.ts";
const [api] = git([
  "grep",
  "-l",
  "-e",
  "/// <summary>",
  "HEAD",
  "--",
  "src/DM.Web.API/*.cs",
])
  .split("\n")
  .filter(Boolean)
  .map((line) => line.slice("HEAD:".length));
const blobs = readBlobs([`HEAD:${real}`, `HEAD:${api}`]);
const original = blobs.get(`HEAD:${real}`).toString("utf8");
const apiOriginal = blobs.get(`HEAD:${api}`).toString("utf8");
const scratch = mkdtempSync(join(tmpdir(), "compare-code-test-"));
try {
  /** Runs the command against a side that holds the given files. */
  const run = (files) => {
    const dir = mkdtempSync(join(scratch, "side-"));
    for (const [path, content] of Object.entries(files)) {
      mkdirSync(join(dir, dirname(path)), { recursive: true });
      writeFileSync(join(dir, path), content);
    }
    return spawnSync(
      process.execPath,
      [join(HERE, "compare-code.mjs"), "--head-dir", dir],
      { encoding: "utf8" },
    );
  };
  const uncommented = original.replace(/^\s*\/\/.*\n/gm, "");
  report(
    uncommented !== original && run({ [real]: uncommented }).status === 0,
    `command exit 0  ${real} without its line comments`,
  );
  report(
    run({ [real]: original.replace("port: 5173", "port: 5175") }).status === 1,
    `command exit 1  ${real} with its port changed`,
  );
  report(
    run({
      [real]: original,
      "src/DM.Web.Client/.env.local": "VITE_API_HOST=http://elsewhere\n",
    }).status === 0,
    `command exit 0  ${real} next to a .env.local, which git ignores`,
  );
  const redocumented = apiOriginal.replace(
    "/// <summary>",
    "/// <summary>Changed.",
  );
  const apiRun = run({ [api]: redocumented });
  report(
    redocumented !== apiOriginal &&
      apiRun.status === 0 &&
      apiRun.stdout.includes("DM.Web.API.xml") &&
      apiRun.stdout.includes(api),
    `command exit 0  ${api} with a doc comment changed, named for the XML diff`,
  );
} finally {
  rmSync(scratch, { recursive: true, force: true });
}

console.log(failures === 0 ? "all cases hold" : `${failures} cases failed`);
process.exitCode = failures === 0 ? 0 : 1;
