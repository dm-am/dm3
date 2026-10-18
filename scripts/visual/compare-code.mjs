#!/usr/bin/env node
// Proves that an edit touched only comments.
//
// Token mode (the default) reads every changed .ts, .js, .json, .vue, .cs,
// .sass and .css file at both sides with its comments taken out and compares
// what is left token by token; compare/tokens.mjs says which comments count
// as code (#__PURE__, @vite-ignore, eslint-disable, @ts-expect-error, ... and
// #pragma) and how each language is read. A file the lexers cannot read, and
// a file that appeared or went away, is a difference: the tool answers "same
// code" only for what it could prove.
//
// "Same code" is about what compiles. The doc comments (///) of
// src/DM.Web.API are also published, in DM.Web.API.xml and through it in the
// API document; the tool lists the files of that project that changed in
// comments only, and for them the XML of the two builds is diffed as well.
//
//   node scripts/visual/compare-code.mjs [-- PATHSPEC...]       HEAD against the working tree
//   node scripts/visual/compare-code.mjs --base REV --head REV [-- PATHSPEC...]
//   node scripts/visual/compare-code.mjs --head-dir DIR [-- PATHSPEC...]
//   node scripts/visual/compare-code.mjs --all [-- PATHSPEC...]  every file, changed or not
//
// --head-dir takes a directory laid out like the repository. Only the files it
// holds are compared, plus, with pathspecs, the files of the base under them
// that it lacks; what git would ignore at the same path in the repository
// (a .env.local, build output) is left out, in build mode too. --verbose
// lists the files that compared equal too.
//
// Build mode builds the client at both sides with the production config and
// compares the output file by file:
//
//   node scripts/visual/compare-code.mjs --build [--base REV|--base-dir DIR] [--head REV|--head-dir DIR]
//   node scripts/visual/compare-code.mjs --build --save-base DIR [--base-only]
//   node scripts/visual/compare-code.mjs --build --baseline DIR
//
// The output is the shipped bundle with two differences, both forced by what
// is being compared. Output names carry no content hash (a hash only restates
// the content, and one changed chunk would rename every chunk importing it).
// Scope ids of scoped styles are renumbered per file, in selectors and in the
// names of scoped keyframes alike, because @vitejs/plugin-vue hashes a
// component's whole source into its id in a production build, comments
// included.
//
// Exit code: 0 the same code, 1 different, 2 the comparison could not be made.

import { join, relative, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import {
  checkBaselineOptions,
  readArguments,
  runTool,
} from "./compare/args.mjs";
import {
  buildSide,
  createRun,
  readOutputs,
  removeRun,
  writeOutputs,
} from "./compare/build.mjs";
import {
  CLIENT,
  REPO,
  UsageError,
  git,
  listDirectory,
  listWorktree,
  readSideFiles,
} from "./compare/git.mjs";
import {
  LexError,
  firstDifference,
  position,
  sourceText,
} from "./compare/lexing.mjs";
import { isSupported, tokensOf } from "./compare/tokens.mjs";

const SHOWN = 20;
// The one project whose doc comments are published (see compare/csharp.mjs).
const PUBLISHED_DOCS = /^src\/DM\.Web\.API\/.*\.cs$/;

/** The files of a revision under the pathspecs (all of them without any). */
function treeFiles(sha, specs) {
  return git(["ls-tree", "-r", "-z", "--name-only", sha, "--", ...specs])
    .split("\0")
    .filter(Boolean);
}

/** Whether a repository path falls under one of the pathspecs. */
function within(path, specs) {
  return (
    specs.length === 0 ||
    specs.some((spec) => {
      const prefix = spec.replace(/\/$/, "");
      return prefix === "." || path === prefix || path.startsWith(`${prefix}/`);
    })
  );
}

/**
 * The pairs of paths to compare. A rename pairs the old path with the new
 * one; otherwise a path is paired with itself, and the side that lacks it
 * reads as an added or removed file.
 */
function changedPairs(options) {
  const { baseSide: base, headSide: head, paths, all } = options;
  if (base.kind !== "rev")
    throw new UsageError("token mode takes its base from a revision");
  const pairs = new Map();
  const add = (basePath, headPath = basePath) =>
    pairs.set(headPath, { basePath, headPath });

  if (head.kind === "dir") {
    for (const path of listDirectory(head.dir))
      if (within(path, paths)) add(path);
    if (paths.length > 0)
      for (const path of treeFiles(base.sha, paths)) add(path);
  } else if (all) {
    const headFiles =
      head.kind === "rev" ? treeFiles(head.sha, paths) : listWorktree(paths);
    for (const path of [...treeFiles(base.sha, paths), ...headFiles]) add(path);
  } else {
    const range = head.kind === "rev" ? [base.sha, head.sha] : [base.sha];
    const fields = git([
      "diff",
      "--no-ext-diff",
      "--name-status",
      "-z",
      "-M",
      ...range,
      "--",
      ...paths,
    ])
      .split("\0")
      .filter(Boolean);
    for (let i = 0; i < fields.length; ) {
      const status = fields[i++];
      if (status.startsWith("R") || status.startsWith("C"))
        add(fields[i++], fields[i++]);
      else add(fields[i++]);
    }
    if (head.kind === "worktree") {
      const untracked = git([
        "ls-files",
        "-z",
        "--others",
        "--exclude-standard",
        "--",
        ...paths,
      ]);
      for (const path of untracked.split("\0").filter(Boolean)) add(path);
    }
  }
  return [...pairs.values()].sort((a, b) => (a.headPath < b.headPath ? -1 : 1));
}

/** The tokens of one side's file; a lexer failure names the side and the place. */
function tokenize(sideName, path, content) {
  try {
    return tokensOf(path, content);
  } catch (error) {
    if (error instanceof LexError) {
      error.message = `${sideName} ${position(sourceText(content), error.at)}: ${error.message}`;
    }
    throw error;
  }
}

function compareTokens(options) {
  const { baseSide: base, headSide: head } = options;
  const pairs = changedPairs(options);
  console.log(
    `compare-code: base ${base.label}, head ${head.label}, ${pairs.length} files`,
  );
  const counts = { same: 0, differ: 0, skipped: 0, failed: 0 };
  const published = [];
  const baseFiles = readSideFiles(
    base,
    pairs.map((pair) => pair.basePath),
  );
  const headFiles = readSideFiles(
    head,
    pairs.map((pair) => pair.headPath),
  );
  for (const { basePath, headPath } of pairs) {
    const before = baseFiles.get(basePath);
    const after = headFiles.get(headPath);
    const label =
      basePath === headPath ? headPath : `${basePath} -> ${headPath}`;
    if (!before && !after) continue;
    const readable = isSupported(basePath) && isSupported(headPath);
    if (options.all && !readable) {
      counts.skipped++;
      continue;
    }
    if (!before || !after) {
      counts.differ++;
      console.log(`  ${before ? "removed" : "added  "} ${label}`);
      continue;
    }
    const identical = sourceText(before) === sourceText(after);
    if (!readable) {
      if (identical) {
        counts.same++;
        continue;
      }
      counts.differ++;
      console.log(
        `  ?       ${label}  (no lexer for this file type, so a change in it cannot be proved comment-only)`,
      );
      continue;
    }
    try {
      // Under --all an identical file is read anyway: that is what checks
      // the lexers against every file of the tree.
      const a =
        identical && !options.all ? null : tokenize("base", basePath, before);
      if (identical) {
        counts.same++;
        if (options.verbose) console.log(`  same    ${label}`);
        continue;
      }
      const difference = firstDifference(a, tokenize("head", headPath, after));
      if (!difference) {
        counts.same++;
        if (options.verbose) console.log(`  same    ${label}  (comments only)`);
        if (PUBLISHED_DOCS.test(headPath)) published.push(label);
        continue;
      }
      counts.differ++;
      console.log(
        `  code    ${label}${difference.treeOnly ? "  (same tokens, different syntax tree)" : ""}`,
      );
      console.log(`          base ${difference.base}`);
      console.log(`          head ${difference.head}`);
    } catch (error) {
      if (!(error instanceof LexError)) throw error;
      counts.failed++;
      console.log(`  error   ${label}: ${error.message}`);
    }
  }
  const skipped =
    counts.skipped > 0
      ? `, ${counts.skipped} of a type the lexers do not read skipped`
      : "";
  console.log(
    `same code: ${counts.same}, different: ${counts.differ}, unreadable: ${counts.failed}${skipped}`,
  );
  if (published.length > 0) {
    console.log(
      `\n${published.length} files of src/DM.Web.API changed in comments only. Their /// text goes into DM.Web.API.xml and the API document, which this tool does not read: diff that XML of the two builds too.`,
    );
    for (const label of published.slice(0, SHOWN)) console.log(`  ${label}`);
    if (published.length > SHOWN)
      console.log(`  ...and ${published.length - SHOWN} more`);
  }
  if (counts.failed > 0)
    throw new UsageError(`${counts.failed} files could not be read; see above`);
  return counts.differ === 0;
}

const TEXT_OUTPUT = /\.(?:js|mjs|css|html|json|svg|txt|xml|webmanifest|map)$/;

async function compareBuilds(options) {
  const { relabelScopeIds } = await import(
    pathToFileURL(
      join(CLIENT, "src", "shared", "lib", "testing", "cssSnapshot.ts"),
    ).href
  );
  const run = createRun();
  try {
    let base;
    let baseLabel;
    if (options.baseline) {
      base = readOutputs(options.baseline);
      baseLabel = `baseline ${options.baseline}`;
    } else {
      base = readOutputs(
        buildSide(options.baseSide, run, "base", { minify: true }),
      );
      baseLabel = options.baseSide.label;
      if (options["save-base"]) {
        writeOutputs(options["save-base"], base);
        console.log(
          `saved the build of ${baseLabel} to ${options["save-base"]}`,
        );
        if (options["base-only"]) return true;
      }
    }
    const head = readOutputs(
      buildSide(options.headSide, run, "head", { minify: true }),
    );
    console.log(
      `compare-code --build: base ${baseLabel}, head ${options.headSide.label}`,
    );
    const read = (path, content) =>
      TEXT_OUTPUT.test(path)
        ? relabelScopeIds(content.toString("utf8"))
        : content.toString("latin1");
    const names = [...new Set([...base.keys(), ...head.keys()])].sort();
    let differ = 0;
    for (const name of names) {
      const a = base.get(name);
      const b = head.get(name);
      if (!a || !b) {
        if (differ++ < SHOWN)
          console.log(`  ${a ? "removed" : "added  "} ${name}`);
        continue;
      }
      const left = read(name, a);
      const right = read(name, b);
      if (left === right) continue;
      if (differ++ >= SHOWN) continue;
      let at = 0;
      while (at < left.length && at < right.length && left[at] === right[at])
        at++;
      const excerpt = (text) =>
        JSON.stringify(text.slice(Math.max(0, at - 60), at + 60));
      console.log(`  changed ${name} at ${position(left, at)}`);
      console.log(`          base ${excerpt(left)}`);
      console.log(`          head ${excerpt(right)}`);
    }
    if (differ > SHOWN) console.log(`  ...and ${differ - SHOWN} more`);
    console.log(
      differ === 0
        ? `same build: ${names.length} files byte for byte equal`
        : `different build: ${differ} of ${names.length} files differ`,
    );
    return differ === 0;
  } finally {
    if (options.keep) console.log(`kept ${run}`);
    else removeRun(run);
  }
}

async function main() {
  const options = readArguments(process.argv.slice(2), {
    values: ["--save-base", "--baseline"],
    flags: ["--build", "--all", "--verbose", "--keep", "--base-only"],
  });
  if (options.build) {
    if (options.paths.length > 0)
      throw new UsageError(
        "build mode compares whole builds and takes no paths",
      );
    checkBaselineOptions(options);
    return compareBuilds(options);
  }
  for (const option of ["baseline", "save-base", "base-only", "keep"]) {
    if (options[option])
      throw new UsageError(`--${option} belongs to build mode`);
  }
  // Pathspecs are given from wherever the tool is run; git runs at the root.
  options.paths = options.paths.map((path) =>
    path.startsWith(":")
      ? path
      : relative(REPO, resolve(path)).split(sep).join("/") || ".",
  );
  return compareTokens(options);
}

await runTool(main);
