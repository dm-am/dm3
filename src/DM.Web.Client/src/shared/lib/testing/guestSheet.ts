/**
 * The guest surface as text in the order the browser gets its rules: the
 * _Auth*.sass parts as AuthSurface.sass orders them, then the sheet itself.
 * Spec-only: ruleBody() asserts with vitest.
 */
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect } from "vitest";
import { CLIENT_SRC } from "./sourceTree";

const STYLES = join(CLIENT_SRC, "assets", "styles");

const sheet = readFileSync(join(STYLES, "AuthSurface.sass"), "utf8");
const parts = [...sheet.matchAll(/^@use "(Auth\w+)"$/gm)].map(([, part]) =>
  readFileSync(join(STYLES, `_${part}.sass`), "utf8"),
);

// A sheet read as nothing would pass every absence a spec asserts about it.
if (parts.length === 0) {
  throw new Error("AuthSurface.sass uses no _Auth*.sass part to read");
}

/** The guest sheet: its parts in the order it uses them, then itself. */
export const SURFACE = [...parts, sheet].join("\n");

/** The body of one rule: everything indented under its line, to the next first-column line. */
export const ruleBody = (source: string, selector: string): string => {
  const lines = source.split("\n");
  const opens = lines.findIndex((line) => line === selector);
  expect(opens, `"${selector}" is not declared`).toBeGreaterThan(-1);

  const body: string[] = [];
  for (let i = opens + 1; i < lines.length; i += 1) {
    const line = lines[i];
    if (line.trim() !== "" && !/^\s/.test(line)) break;
    body.push(line);
  }
  return body.join("\n");
};
