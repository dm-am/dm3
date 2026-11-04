#!/usr/bin/env node
// The frame stand and the API snapshot: two instruments that say whether a
// change was invisible. By default both measure the stack that is already
// running, the one the site on :5173 talks to, and change nothing in it: no
// reset, no rebuild, no seed. A base is comparable only with a run on the same
// data, so a base records a fingerprint of the stand's data, and a diff on
// other data is refused instead of reported as a wall of differences.
//
// --rebuild-stand is the strict mode: the stack is reset, rebuilt from the
// tree and seeded at a pinned epoch and seed, so the same commit measured
// twice gives the same bytes. It destroys the data of the shared stack and
// takes the site down for 10-15 minutes, so it is never the default.
//
// Outside CI on purpose: pixels depend on the fonts and the rasteriser of the
// machine, so a base is only ever compared on the machine that took it.
//
//   node scripts/visual/visual.mjs stand [--rebuild-stand]
//   node scripts/visual/visual.mjs snap <label> [--root DIR] [--theme dark]
//   node scripts/visual/visual.mjs diff <label> [--root DIR] [--theme dark]
//   node scripts/visual/visual.mjs api-snap <label> [--root DIR]
//   node scripts/visual/visual.mjs api-diff <label> [--root DIR]
//
// "stand" checks the running stand and prints its fingerprint. Common
// options: --rev REV (build the client from that commit
// instead of the working tree; with --rebuild-stand, the stack as well),
// --rebuild-stand, --no-build (reuse the last client build), --grep RE (only
// the states whose name matches).
// README.md next to this file says when to use which.

import { spawn } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import { runApiSnapshot, VOID } from "./api.mjs";
import { clearBase, readBase, REBUILT, RUNNING, writeMeta } from "./base.mjs";
import {
  sameData,
  sameStand,
  sourcesUntouched,
  standIdentity,
} from "./intact.mjs";
import { buildClient, DIST, PREVIEW_PORT, startPreview } from "./preview.mjs";
import { rebuildStand, SEED_EPOCH } from "./rebuild.mjs";
import { fail, Refusal } from "./refusal.mjs";
import {
  API,
  awaitFirstPasses,
  checkStand,
  fingerprintChanges,
} from "./stand.mjs";
import { CLIENT, HERE, sourceTree, WORK } from "./tree.mjs";

function parseArguments(argv) {
  const [command, ...rest] = argv;
  let parsed;
  try {
    parsed = parseArgs({
      args: rest,
      allowPositionals: true,
      options: {
        root: { type: "string" },
        theme: { type: "string" },
        grep: { type: "string" },
        rev: { type: "string" },
        "rebuild-stand": { type: "boolean" },
        "no-build": { type: "boolean" },
      },
    });
  } catch (error) {
    fail(error.message);
  }
  const { values, positionals } = parsed;
  if (positionals.length > 1) fail(`unknown argument ${positionals[1]}`);
  return {
    command,
    label: positionals[0],
    root: values.root,
    theme: values.theme,
    grep: values.grep,
    rev: values.rev,
    rebuild: values["rebuild-stand"] ?? false,
    build: !values["no-build"],
  };
}

/**
 * What every run does before it measures: refuses a base it cannot be
 * compared with, rebuilds the stand only on --rebuild-stand, checks the stand
 * it is about to measure, and keeps what identifies that stand, so that the
 * end of the run can tell one stand from two.
 *
 * `built` are the directories of the tree that the run builds what it
 * measures from on the running stand.
 */
async function begin(options, update, base, built) {
  const meta = update ? null : readBase(base, options);
  const started = Date.now();
  const tree = sourceTree(options.rev, options.rebuild);
  if (options.rebuild) await rebuildStand(tree);
  else {
    console.log(
      `visual: measuring the running stand at ${API} as it is: nothing is reset, rebuilt or seeded`,
    );
  }
  const { fingerprint, apiBuiltFrom } = await checkStand();
  if (meta) {
    const changes = fingerprintChanges(meta.fingerprint, fingerprint);
    if (changes.length > 0) {
      fail(
        [
          "the stand does not hold the data the base was taken on:",
          ...changes.map((line) => `  ${line}`),
          "It was reseeded or written to since the base (or the server counts differently now): " +
            "take the base again, then compare.",
        ].join("\n"),
      );
    }
    if ((meta.apiBuiltFrom ?? null) !== apiBuiltFrom) {
      console.log(
        `visual: the base measured the API built on ${meta.apiBuiltFrom ?? "(unknown)"}, this run the one built on ${apiBuiltFrom ?? "(unknown)"}`,
      );
    }
  }
  if (update) clearBase(base);
  const identity = standIdentity();
  const watched = options.rebuild ? ["src"] : built;
  return {
    tree,
    started,
    meta,
    fingerprint,
    apiBuiltFrom,
    stand: options.rebuild ? REBUILT : RUNNING,
    intact: async () =>
      sameStand(identity) &&
      (await sameData(fingerprint)) &&
      sourcesUntouched(started, tree.root, watched),
  };
}

async function frames(options, update) {
  const theme = options.theme === "dark" ? "dark" : "light";
  const root = resolve(
    options.root || process.env.DM_VISUAL_ROOT || join(WORK, "frames"),
  );
  const base = join(root, options.label, theme);
  const report = join(root, `${options.label}.diff`, theme);
  const measurement = await begin(options, update, base, [
    join("src", "DM.Web.Client"),
  ]);
  if (options.build || !existsSync(DIST)) buildClient(measurement.tree.root);
  await awaitFirstPasses(options.rebuild);
  const preview = await startPreview();

  const args = [
    join(CLIENT, "node_modules", "@playwright", "test", "cli.js"),
    "test",
    "-c",
    join(HERE, "frames.config.ts"),
    `--update-snapshots=${update ? "all" : "none"}`,
  ];
  if (options.grep) args.push("--grep", options.grep);

  const status = await new Promise((done) => {
    const child = spawn(process.execPath, args, {
      cwd: CLIENT,
      stdio: "inherit",
      env: {
        ...process.env,
        // The config and the specs live outside the client package, so the
        // client's copy of @playwright/test is found through NODE_PATH: the
        // specs are compiled to CommonJS, which honours it.
        NODE_PATH: join(CLIENT, "node_modules"),
        VISUAL_BASE: base,
        VISUAL_REPORT: report,
        VISUAL_OUTPUT: join(WORK, "output"),
        VISUAL_THEME: theme,
        VISUAL_MODE: update ? "snap" : "diff",
        VISUAL_EPOCH: SEED_EPOCH,
        VISUAL_APP: `http://localhost:${PREVIEW_PORT}`,
        VISUAL_API: API,
        VISUAL_AUTH: join(WORK, "auth"),
      },
    });
    child.on("exit", (code) => done(code ?? 1));
  });
  preview.kill();
  if (!(await measurement.intact())) return VOID;

  if (update && status === 0)
    writeMeta(base, measurement, { theme, grep: options.grep ?? null });
  if (!update) console.log(`report: ${join(report, "report.txt")}`);
  return status;
}

async function api(options, update) {
  const root = resolve(
    options.root || process.env.DM_API_SNAPSHOT_ROOT || join(WORK, "api"),
  );
  const base = join(root, options.label);
  const report = join(root, `${options.label}.diff`);
  const measurement = await begin(options, update, base, []);
  await awaitFirstPasses(options.rebuild);
  const status = await runApiSnapshot({
    routes: join(measurement.tree.root, "artifacts", "openapi-routes.json"),
    api: API,
    base,
    report,
    update,
    grep: options.grep,
    baseStarted: measurement.meta
      ? Date.parse(measurement.meta.startedAt)
      : undefined,
  });
  if (!(await measurement.intact())) return VOID;
  if (update && status === 0)
    writeMeta(base, measurement, { grep: options.grep ?? null });
  return status;
}

async function main(argv) {
  const options = parseArguments(argv);
  const needsLabel = ["snap", "diff", "api-snap", "api-diff"];
  if (needsLabel.includes(options.command) && !options.label)
    fail(`${options.command} needs a label`);

  switch (options.command) {
    case "stand": {
      if (options.rebuild) await rebuildStand(sourceTree(options.rev, true));
      const { fingerprint, apiBuiltFrom } = await checkStand();
      console.log(JSON.stringify({ apiBuiltFrom, fingerprint }, null, 2));
      return 0;
    }
    case "snap":
      return frames(options, true);
    case "diff":
      return frames(options, false);
    case "api-snap":
      return api(options, true);
    case "api-diff":
      return api(options, false);
    default: {
      // The usage is the comment this file opens with.
      const lines = readFileSync(fileURLToPath(import.meta.url), "utf8").split(
        "\n",
      );
      const usage = [];
      for (const line of lines.slice(1)) {
        if (!line.startsWith("//")) break;
        usage.push(line.replace(/^\/\/ ?/, ""));
      }
      console.log(usage.join("\n"));
      return options.command ? 2 : 0;
    }
  }
}

// The exit code is the verdict, so it is set and the process left to end on
// its own: process.exit() with a fetch's connection still open aborts Node on
// Windows (a libuv assertion, exit 127), and so does an unhandled rejection.
try {
  process.exitCode = await main(process.argv.slice(2));
} catch (error) {
  if (error instanceof Refusal) {
    console.error(`visual: ${error.message}`);
    process.exitCode = 2;
  } else {
    console.error(error);
    process.exitCode = 1;
  }
}
