/**
 * A block of a sheet located by its selector path, and what it declares on
 * its own level; a .vue file is read by its <style> blocks, indents kept.
 */
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { CLIENT_SRC } from "./sourceTree";

export interface Line {
  indent: number;
  text: string;
}

const STYLE_BLOCK = /<style[^>]*>([\s\S]*?)<\/style>/g;

export function stylesheet(file: string): Line[] {
  const source = readFileSync(join(CLIENT_SRC, file), "utf8");
  const body = file.endsWith(".vue")
    ? Array.from(source.matchAll(STYLE_BLOCK), (found) => found[1]).join("\n")
    : source;
  return body
    .split("\n")
    .map((raw) => raw.replace(/\r$/, ""))
    .filter((raw) => raw.trim() !== "" && !raw.trim().startsWith("//"))
    .map((raw) => ({
      indent: raw.length - raw.trimStart().length,
      text: raw.trim(),
    }));
}

/** The body of the block at the given selector path, its own lines included. */
export function blockAt(file: string, path: string[]): Line[] {
  const lines = stylesheet(file);
  let from = 0;
  let to = lines.length;
  for (const selector of path) {
    const childIndent = lines[from].indent;
    let at = -1;
    for (let i = from; i < to; i += 1) {
      if (lines[i].indent === childIndent && lines[i].text === selector) {
        at = i;
        break;
      }
    }
    if (at < 0) {
      throw new Error(`${file} has no "${selector}" in ${path.join(" > ")}`);
    }
    from = at + 1;
    to = at + 1;
    while (to < lines.length && lines[to].indent > childIndent) to += 1;
  }
  return lines.slice(from, to);
}

/** The last value the block declares for the property, or null. */
export function declared(body: Line[], property: RegExp): string | null {
  if (body.length === 0) return null;
  const own = Math.min(...body.map((line) => line.indent));
  let value: string | null = null;
  for (const line of body) {
    if (line.indent !== own) continue;
    const found = /^([a-z-]+):\s+(.+)$/.exec(line.text);
    if (found && property.test(found[1])) value = found[2].trim();
  }
  return value;
}

export const BACKGROUND = /^background(-color)?$/;
export const IMAGE = /^background-image$/;
export const INK = /^color$/;
export const OUTLINE = /^border-color$/;
