// The repository read at a revision or as it stands on disk, without touching
// the index, the working tree or the stash: the comparison tools run next to
// other people's unfinished work and must leave it exactly as they found it.

import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
export const REPO = resolve(HERE, "..", "..", "..");
export const CLIENT_PREFIX = "src/DM.Web.Client";
export const CLIENT = join(REPO, ...CLIENT_PREFIX.split("/"));

// What a directory side never contributes: installed packages, build output
// and reports. They are ignored files anyway; skipping them by name saves
// walking node_modules, which would take minutes and decide nothing.
const NOT_SOURCE = new Set([
  "node_modules",
  "dist",
  "coverage",
  "playwright-report",
  "test-results",
  ".git",
  "bin",
  "obj",
]);

export class UsageError extends Error {}

export function git(args, { input, encoding = "utf8" } = {}) {
  const result = spawnSync("git", args, {
    cwd: REPO,
    input,
    encoding: encoding === "buffer" ? undefined : encoding,
    maxBuffer: 1 << 30,
    windowsHide: true,
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    const stderr = Buffer.isBuffer(result.stderr)
      ? result.stderr.toString("utf8")
      : result.stderr;
    throw new UsageError(`git ${args.join(" ")} failed: ${stderr.trim()}`);
  }
  return result.stdout;
}

/** The commit a revision names, so both reads of one side see the same tree. */
export function resolveRev(rev) {
  return git(["rev-parse", "--verify", "--quiet", `${rev}^{commit}`]).trim();
}

/**
 * Blobs by object name or "<rev>:<path>", in one git process. A missing object
 * comes back as null rather than as an error: a path that does not exist at a
 * revision is an answer, not a failure.
 */
export function readBlobs(specs) {
  const found = new Map();
  if (specs.length === 0) return found;
  const out = git(["cat-file", "--batch"], {
    input: specs.join("\n") + "\n",
    encoding: "buffer",
  });
  let at = 0;
  for (const spec of specs) {
    const newline = out.indexOf(0x0a, at);
    const header = out.subarray(at, newline).toString("utf8");
    at = newline + 1;
    if (header.endsWith(" missing") || header.endsWith(" ambiguous")) {
      found.set(spec, null);
      continue;
    }
    const size = Number(header.split(" ")[2]);
    found.set(spec, out.subarray(at, at + size));
    at += size + 1;
  }
  return found;
}

/** Every file a revision holds under a prefix, with its object name. */
export function listTree(sha, prefix) {
  const out = git(["ls-tree", "-r", "-z", "--full-tree", sha, "--", prefix]);
  return out
    .split("\0")
    .filter(Boolean)
    .map((entry) => {
      const [meta, path] = entry.split("\t");
      const [mode, type, object] = meta.split(" ");
      return { path, mode, type, object };
    })
    .filter((entry) => entry.type === "blob");
}

/**
 * Every file the working tree holds under the pathspecs: tracked ones that
 * still exist and untracked ones git does not ignore.
 */
export function listWorktree(pathspecs) {
  return git([
    "ls-files",
    "-z",
    "-co",
    "--exclude-standard",
    "--",
    ...pathspecs,
  ])
    .split("\0")
    .filter(Boolean)
    .filter((path) => existsSync(join(REPO, path)));
}

/**
 * Which of the given repository paths git ignores. The paths need not exist,
 * and a tracked path is never ignored, as git itself has it.
 */
function ignoredPaths(paths) {
  if (paths.length === 0) return new Set();
  const result = spawnSync("git", ["check-ignore", "-z", "--stdin"], {
    cwd: REPO,
    input: paths.join("\0"),
    encoding: "utf8",
    maxBuffer: 1 << 30,
    windowsHide: true,
  });
  if (result.error) throw result.error;
  // 0: some paths are ignored, 1: none is.
  if (result.status !== 0 && result.status !== 1) {
    throw new UsageError(`git check-ignore failed: ${result.stderr.trim()}`);
  }
  return new Set(result.stdout.split("\0").filter(Boolean));
}

/**
 * Every file under a directory that stands for the repository path
 * `repoPrefix`, as paths relative to it with forward slashes. What git would
 * ignore there is left out, the way the working tree side leaves it out, so a
 * copy made with cp brings no local .env and no stale output into a
 * comparison.
 */
export function listDirectory(root, repoPrefix = "") {
  const found = [];
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      if (NOT_SOURCE.has(entry.name)) continue;
      const full = join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.isFile())
        found.push(relative(root, full).split(sep).join("/"));
    }
  };
  walk(root);
  const repoPath = (path) => (repoPrefix ? `${repoPrefix}/${path}` : path);
  const ignored = ignoredPaths(found.map(repoPath));
  return found.filter((path) => !ignored.has(repoPath(path))).sort();
}

/**
 * A side of a comparison: a revision, the working tree, or a directory laid
 * out like the repository (for a copy that was changed on purpose).
 */
export function parseSide(value, kind) {
  if (kind === "dir") {
    const dir = resolve(value);
    if (!existsSync(dir) || !statSync(dir).isDirectory()) {
      throw new UsageError(`${value} is not a directory`);
    }
    return { kind: "dir", dir, label: `directory ${dir}` };
  }
  if (value === undefined) return { kind: "worktree", label: "working tree" };
  let sha;
  try {
    sha = resolveRev(value);
  } catch {
    throw new UsageError(`${value} does not name a commit`);
  }
  return {
    kind: "rev",
    rev: value,
    sha,
    label: `${value} (${sha.slice(0, 10)})`,
  };
}

/**
 * Files of a side by repository path, null for each the side does not have.
 * A revision is read in one git process: one per file costs minutes on a
 * tree of a few thousand.
 */
export function readSideFiles(side, paths) {
  const found = new Map();
  if (side.kind === "rev") {
    const blobs = readBlobs(paths.map((path) => `${side.sha}:${path}`));
    for (const path of paths) found.set(path, blobs.get(`${side.sha}:${path}`));
    return found;
  }
  for (const path of paths) {
    const full = join(
      side.kind === "dir" ? side.dir : REPO,
      ...path.split("/"),
    );
    found.set(
      path,
      existsSync(full) && statSync(full).isFile() ? readFileSync(full) : null,
    );
  }
  return found;
}

/**
 * Writes the client as a side holds it into an empty directory: the tracked
 * files of a revision, the tracked and untracked files of the working tree, or
 * the files of a directory side. Files git ignores never come along, on any
 * side, so a local .env.local or a stale dist cannot make the two builds
 * differ.
 */
export function materializeClient(side, target) {
  const files = new Map();
  if (side.kind === "rev") {
    const entries = listTree(side.sha, CLIENT_PREFIX);
    const links = entries.filter((entry) => entry.mode === "120000");
    if (links.length > 0) {
      throw new UsageError(
        `symbolic links are not supported: ${links[0].path}`,
      );
    }
    const blobs = readBlobs(entries.map((entry) => entry.object));
    for (const entry of entries) {
      files.set(
        entry.path.slice(CLIENT_PREFIX.length + 1),
        blobs.get(entry.object),
      );
    }
  } else if (side.kind === "worktree") {
    for (const path of listWorktree([CLIENT_PREFIX])) {
      files.set(
        path.slice(CLIENT_PREFIX.length + 1),
        readFileSync(join(REPO, path)),
      );
    }
  } else {
    // A directory side is laid out like the repository, so its client sits at
    // the same prefix; one that holds only the client is taken as it is.
    const nested = join(side.dir, ...CLIENT_PREFIX.split("/"));
    const root = existsSync(join(nested, "package.json")) ? nested : side.dir;
    if (!existsSync(join(root, "package.json"))) {
      throw new UsageError(
        `${side.dir} holds no client: no package.json at its root or at ${CLIENT_PREFIX}`,
      );
    }
    for (const path of listDirectory(root, CLIENT_PREFIX)) {
      files.set(path, readFileSync(join(root, ...path.split("/"))));
    }
  }
  for (const [path, content] of files) {
    const full = join(target, ...path.split("/"));
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, content);
  }
  return files.size;
}
