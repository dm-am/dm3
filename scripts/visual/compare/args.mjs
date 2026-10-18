// The command line both comparison tools share: two sides, each a revision,
// the working tree or a directory, and positional pathspecs after them.

import { UsageError, parseSide } from "./git.mjs";

/**
 * `values` are the options that take an argument, `flags` the ones that do
 * not. --base, --base-dir, --head and --head-dir are always understood.
 */
export function readArguments(argv, { values = [], flags = [] } = {}) {
  const taken = new Set([
    "--base",
    "--base-dir",
    "--head",
    "--head-dir",
    ...values,
  ]);
  const options = { paths: [] };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--") {
      options.paths.push(...argv.slice(i + 1));
      break;
    }
    if (taken.has(arg)) {
      if (i + 1 >= argv.length) throw new UsageError(`${arg} needs a value`);
      options[arg.slice(2)] = argv[++i];
    } else if (flags.includes(arg)) {
      options[arg.slice(2)] = true;
    } else if (arg.startsWith("--")) {
      throw new UsageError(`unknown option ${arg}`);
    } else {
      options.paths.push(arg);
    }
  }
  if (options.base !== undefined && options["base-dir"] !== undefined) {
    throw new UsageError("--base and --base-dir name the same side");
  }
  if (options.head !== undefined && options["head-dir"] !== undefined) {
    throw new UsageError("--head and --head-dir name the same side");
  }
  options.baseSide =
    options["base-dir"] !== undefined
      ? parseSide(options["base-dir"], "dir")
      : parseSide(options.base ?? "HEAD", "rev");
  options.headSide =
    options["head-dir"] !== undefined
      ? parseSide(options["head-dir"], "dir")
      : parseSide(options.head, "rev");
  return options;
}

/**
 * --save-base writes the base for later, --baseline reads one written before;
 * neither makes sense with the other, and a saved base replaces the base side.
 */
export function checkBaselineOptions(options) {
  if (options.baseline && options["save-base"]) {
    throw new UsageError("--baseline and --save-base exclude each other");
  }
  if (options.baseline && (options.base ?? options["base-dir"]) !== undefined) {
    throw new UsageError("--baseline replaces the base side; drop --base");
  }
  if (options["base-only"] && !options["save-base"]) {
    throw new UsageError("--base-only stops after --save-base and needs it");
  }
}

/** Runs a tool's main and turns its outcome into the exit code. */
export async function runTool(main) {
  try {
    process.exitCode = (await main()) ? 0 : 1;
  } catch (error) {
    console.error(
      error instanceof UsageError ? `error: ${error.message}` : error,
    );
    process.exitCode = 2;
  }
}
