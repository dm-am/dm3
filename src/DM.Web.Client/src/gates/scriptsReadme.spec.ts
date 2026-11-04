/**
 * @vitest-environment node
 */

/**
 * The register of the scripts, scripts/README.md, has a row for every script of
 * the repository and for nothing else: a list of eighty files that nobody
 * checks is right only on the day it is written, and a script that is in no
 * list is one nobody finds before it breaks.
 *
 * A script is a file under scripts/ or docker/scripts/, or a shell file in
 * docker/ itself, the README of a folder excepted. A row names its file by a
 * link whose text is the link, written from the folder of the register
 * (visual/api.mjs, ../docker/scripts/_env.sh), and holds three more cells: what
 * the file does, who calls it, and where it is described. The third column is
 * not checked against the callers: scriptCallers.spec.ts holds that somebody
 * calls each script, and does not count this register as one.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, posix, resolve } from "path";
import { fileURLToPath } from "url";

import { committedFiles } from "@/shared/lib/testing/duplicates";

// src/gates -> src -> DM.Web.Client -> src -> repository root
const REPO_ROOT = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../..",
);

/** The register, by path from the root. Its links are written from its folder. */
const REGISTER = "scripts/README.md";

/** The tooling, what the server runs, and the shell files of docker/ itself. */
const SCRIPT = /^(?:scripts\/|docker\/scripts\/|docker\/[^/]+\.sh$)/;

/** A row of a table of the register: its file, linked to itself, then cells. */
const ROW = /^\| \[([^\]]+)\]\(\1\) \|(.+)\|$/;

/** The cells a row holds besides its file: what, who calls it, where to read. */
const CELLS = 3;

/** The scripts of the repository, by path from the root. */
const scripts = committedFiles(REPO_ROOT).filter(
  (path) => SCRIPT.test(path) && posix.basename(path) !== "README.md",
);

const lines = readFileSync(join(REPO_ROOT, REGISTER), "utf8").split(/\r?\n/);

/** Lines that open like a row of the register, whatever they hold after. */
const candidates = lines.filter((line) => line.startsWith("| ["));

/** The path from the root that a row names, with the cells after its file. */
const rows = candidates.flatMap((line) => {
  const found = ROW.exec(line.trimEnd());
  if (!found) return [];
  return [
    {
      line,
      path: posix.normalize(posix.join(posix.dirname(REGISTER), found[1])),
      cells: found[2].split("|").map((cell) => cell.trim()),
    },
  ];
});

const listed = rows.map((row) => row.path);

describe("register of the scripts", () => {
  it("reads the rows of the register", () => {
    // A pattern that matched nothing would pass the checks below by comparing
    // an empty list with the scripts and finding no row without a file.
    expect(scripts.length).toBeGreaterThan(20);
    expect(listed).toContain("scripts/gates.sh");
    expect(listed).toContain("docker/scripts/init-env.sh");
    expect(listed).toContain("docker/setup-server.sh");
  });

  it("writes every row as a file and three filled cells", () => {
    const malformed = candidates.filter((line) => {
      const row = rows.find((candidate) => candidate.line === line);
      return (
        !row || row.cells.length !== CELLS || row.cells.some((cell) => !cell)
      );
    });
    expect(malformed).toEqual([]);
  });

  it("has one row for every script", () => {
    expect(listed.filter((path, i) => listed.indexOf(path) !== i)).toEqual([]);
    expect(scripts.filter((path) => !listed.includes(path))).toEqual([]);
  });

  it("has no row for a file that is not a script of the repository", () => {
    expect(listed.filter((path) => !scripts.includes(path))).toEqual([]);
  });
});
