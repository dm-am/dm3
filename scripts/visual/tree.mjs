// Where a run works: the repository, its client, the directory the run keeps
// its builds and bases in, and the tree it builds from.

import { spawnSync } from "node:child_process";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { fail } from "./refusal.mjs";

export const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, "..", "..");
export const CLIENT = join(REPO, "src", "DM.Web.Client");
export const WORK = process.env.DM_VISUAL_WORK || join(tmpdir(), "dm3-visual");

/**
 * The tree a run builds from: the working tree, or a commit exported beside it.
 *
 * A commit is the natural base of a step (what was there before the step) and
 * the one tree nobody else edits while the run is going: the working tree is
 * shared, and another process that edits a source and puts it back mid-run
 * leaves images built from a tree that no longer exists. The export is
 * `git archive` of the commit, plus what a clone does not carry: the client's
 * packages, linked from the working tree, and for a stack built from it
 * (--rebuild-stand) the stack's secrets (docker/.env).
 */
export function sourceTree(rev, buildsStack) {
  if (!rev) return { root: REPO, commit: commitOf(REPO) };
  const commit = spawnSync(
    "git",
    ["-C", REPO, "rev-parse", "--verify", `${rev}^{commit}`],
    { encoding: "utf8" },
  ).stdout.trim();
  if (!commit) fail(`no commit ${rev}`);
  const root = join(WORK, "tree", commit.slice(0, 12));
  const done = join(root, ".exported");
  if (!existsSync(done)) {
    rmSync(root, { recursive: true, force: true });
    mkdirSync(root, { recursive: true });
    const archive = spawnSync(
      "git",
      ["-C", REPO, "archive", "--format=tar", commit],
      {
        maxBuffer: 1 << 30,
      },
    );
    if (archive.status !== 0) fail(`git archive ${commit} failed`);
    // Unpacked in place rather than with -C: GNU tar reads "C:" in a Windows
    // path as the name of a remote host.
    const untar = spawnSync("tar", ["-x"], {
      cwd: root,
      input: archive.stdout,
    });
    if (untar.status !== 0) fail(`unpacking ${commit} failed: ${untar.stderr}`);
    writeFileSync(done, commit);
  }
  if (buildsStack)
    copyFileSync(join(REPO, "docker", ".env"), join(root, "docker", ".env"));
  const modules = join(root, "src", "DM.Web.Client", "node_modules");
  if (!existsSync(modules))
    symlinkSync(join(CLIENT, "node_modules"), modules, "junction");
  return { root, commit };
}

function commitOf(tree) {
  const head = spawnSync("git", ["-C", tree, "rev-parse", "HEAD"], {
    encoding: "utf8",
  });
  const dirty = spawnSync("git", ["-C", tree, "status", "--porcelain"], {
    encoding: "utf8",
  });
  return `${head.stdout.trim()}${dirty.stdout.trim() ? "+dirty" : ""}`;
}
