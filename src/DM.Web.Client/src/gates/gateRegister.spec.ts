/**
 * @vitest-environment node
 */

/**
 * The register of this folder, README.md, has a row for every file the folder
 * holds and for nothing else: a list nobody checks is right only on the day it
 * is written.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
const REGISTER = "README.md";

/** A row of a table of the register: the file it is about, linked to itself. */
const ROW = /^\| \[([^\]]+)\]\(\1\) \| .+ \|$/;

describe("register of the gates", () => {
  const rows = readFileSync(join(HERE, REGISTER), "utf8")
    .split("\n")
    .map((line) => ROW.exec(line.trimEnd())?.[1])
    .filter((name): name is string => name !== undefined);
  const files = readdirSync(HERE).filter((name) => name !== REGISTER);

  it("reads the rows of the register", () => {
    // A pattern that matched nothing would pass the checks below by comparing
    // an empty list with the files and finding no row without a file.
    expect(rows).toContain("gateRegister.spec.ts");
    expect(rows).toContain("sizeRatchet.js");
  });

  it("has one row for every file of the folder", () => {
    expect(rows.filter((name, i) => rows.indexOf(name) !== i)).toEqual([]);
    expect(files.filter((name) => !rows.includes(name))).toEqual([]);
  });

  it("has no row for a file the folder does not hold", () => {
    expect(rows.filter((name) => !files.includes(name))).toEqual([]);
  });
});
