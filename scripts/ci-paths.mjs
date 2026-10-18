#!/usr/bin/env node
// Decides which checks a change needs, from the one map in scripts/ci-paths.json.
//
//   node scripts/ci-paths.mjs --diff <range>      the paths of git diff <range>
//   node scripts/ci-paths.mjs --worktree <ref>    the working tree against its
//                                                 merge base with <ref>, untracked
//                                                 files included
//   node scripts/ci-paths.mjs --stdin             one path per line, for a dry run
//   node scripts/ci-paths.mjs --full <reason>     everything, and why
//   ... --summary <file>                          also append the report there
//
// Prints one "<job>=true|false" line per job of the map on stdout, which is the
// shape $GITHUB_OUTPUT takes, and a report on stderr: which groups of paths the
// change touches and, for every job, whether it runs and why. DM_GATES_FULL set
// to anything forces the full set.
//
// Three readers share it: the changes job of .github/workflows/dotnet.yml, the
// pre-push hook and scripts/gates.sh. A second copy of the map anywhere is the
// drift AgentToolingShould.ReadOneMapOfPaths exists to catch.
//
// Whatever the map cannot place runs everything, and so does a diff git cannot
// produce: a check skipped on a guess passes in silence, which is the one
// outcome this file must never produce.
import { appendFileSync, readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const MAP = JSON.parse(
  readFileSync(join(dirname(fileURLToPath(import.meta.url)), "ci-paths.json"), "utf8"),
);

/** A glob of the map as a regular expression: ** crosses directories, * does not. */
const globToRegex = (glob) => {
  let pattern = "";
  for (let at = 0; at < glob.length; at++) {
    const char = glob[at];
    if (glob.startsWith("**/", at)) {
      pattern += "(?:.*/)?";
      at += 2;
    } else if (glob.startsWith("**", at)) {
      pattern += ".*";
      at += 1;
    } else if (char === "*") {
      pattern += "[^/]*";
    } else {
      pattern += char.replace(/[.+?^${}()|[\]\\]/g, "\\$&");
    }
  }
  return new RegExp(`^${pattern}$`);
};

const GROUPS = MAP.groups.map((group) => ({
  name: group.name,
  patterns: group.paths.map(globToRegex),
}));

/** The first group that names the path, in the order the map lists them. */
const groupOf = (path) =>
  GROUPS.find((group) => group.patterns.some((pattern) => pattern.test(path)))?.name ??
  MAP.unmatched;

const git = (...args) =>
  execFileSync("git", args, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

const lines = (text) =>
  text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

/**
 * --no-renames lists a moved file at both ends: with renames on, a file moved
 * from src/ into docs/ shows only its new path and the server never hears of it.
 */
const changedPaths = (options) => {
  if (options.stdin) {
    return lines(readFileSync(0, "utf8"));
  }
  if (options.diff) {
    return lines(git("diff", "--name-only", "--no-renames", options.diff));
  }
  const base = git("merge-base", options.worktree, "HEAD").trim();
  return [
    ...lines(git("diff", "--name-only", "--no-renames", base)),
    ...lines(git("ls-files", "--others", "--exclude-standard")),
  ];
};

/** Which jobs run for the given groups, each with the reason in words. */
const decide = (present, fullReason) => {
  const fullGroups = MAP.full.groups.filter((group) => present.has(group));
  const together = MAP.full.together.filter((set) => set.every((group) => present.has(group)));
  const full =
    fullReason ??
    (fullGroups.length > 0
      ? `затронуто: ${fullGroups.join(", ")}`
      : together.length > 0
        ? `затронуты вместе: ${together.map((set) => set.join(" и ")).join(", ")}`
        : null);

  const decisions = new Map();
  for (const [job, rule] of Object.entries(MAP.jobs)) {
    const hit = rule.groups.filter((group) => present.has(group));
    decisions.set(job, {
      run: full !== null || hit.length > 0,
      why: full !== null ? "полный прогон" : hit.length > 0 ? hit.join(", ") : null,
      rule,
    });
  }
  for (const [job, decision] of decisions) {
    const covering = decision.rule.unless;
    if (decision.run && covering && decisions.get(covering).run) {
      decisions.set(job, { ...decision, run: false, why: `его проверки идут в ${covering}` });
    }
  }
  for (const decision of decisions.values()) {
    decision.why ??=
      decision.rule.groups.length > 0
        ? `не затронуто: ${decision.rule.groups.join(", ")}`
        : "только в полном прогоне";
  }
  return { full, decisions };
};

const report = (source, byGroup, outcome) => {
  const out = ["### Проверки по затронутым путям", "", `Изменения: ${source}`, ""];
  if (outcome.full !== null) {
    out.push(`Полный прогон: ${outcome.full}`, "");
  }
  if (byGroup.size === 0) {
    out.push("Затронутых путей нет.", "");
  } else {
    out.push("| Группа | Файлов | Например |", "|---|---|---|");
    for (const [group, paths] of byGroup) {
      out.push(`| ${group} | ${paths.length} | ${paths.slice(0, 3).join(", ")} |`);
    }
    out.push("");
  }
  out.push("| Задание | Решение | Почему |", "|---|---|---|");
  for (const [job, decision] of outcome.decisions) {
    out.push(`| ${job} | ${decision.run ? "запускается" : "пропущено"} | ${decision.why} |`);
  }
  return out.join("\n") + "\n";
};

const parse = (argv) => {
  const options = {};
  for (let at = 0; at < argv.length; at++) {
    const flag = argv[at];
    if (flag === "--stdin") {
      options.stdin = true;
    } else if (["--diff", "--worktree", "--full", "--summary"].includes(flag) && at + 1 < argv.length) {
      options[flag.slice(2)] = argv[++at];
    } else {
      throw new Error(`unknown argument ${flag}; see the header of scripts/ci-paths.mjs`);
    }
  }
  if (!options.stdin && !options.diff && !options.worktree && !options.full) {
    throw new Error("name the change: --diff, --worktree, --stdin or --full");
  }
  return options;
};

const main = () => {
  const options = parse(process.argv.slice(2));
  let fullReason = options.full ?? (process.env.DM_GATES_FULL ? "задан DM_GATES_FULL" : null);
  let paths = [];
  const source = options.diff ?? (options.worktree ? `рабочее дерево против ${options.worktree}` : "stdin");

  if (!options.full) {
    try {
      paths = changedPaths(options);
    } catch (error) {
      fullReason = `изменения не прочитались (${String(error.message).split("\n")[0]})`;
    }
  }

  const byGroup = new Map();
  for (const path of paths) {
    const group = groupOf(path);
    byGroup.set(group, [...(byGroup.get(group) ?? []), path]);
  }

  const outcome = decide(new Set(byGroup.keys()), fullReason);
  const text = report(options.full ? "не читались" : source, byGroup, outcome);

  process.stderr.write(text);
  if (options.summary) {
    appendFileSync(options.summary, text);
  }
  for (const [job, decision] of outcome.decisions) {
    process.stdout.write(`${job}=${decision.run}\n`);
  }
};

try {
  main();
} catch (error) {
  process.stderr.write(`ci-paths: ${error.message}
`);
  process.exit(2);
}
