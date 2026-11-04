// The client's source tree as the gates walk it, without build, coverage and packages.
import { readdirSync, statSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/** The directory the "@/" alias stands for. */
export const CLIENT_SRC = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../..",
);
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

export function collect(
  dir: string,
  wanted: (name: string) => boolean,
  out: string[] = [],
): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, wanted, out);
    } else if (wanted(name)) {
      out.push(full);
    }
  }
  return out;
}

export const isStyle = (name: string): boolean =>
  STYLE_FILES.some((ext) => name.endsWith(ext));

export const styleFiles = (): string[] => collect(CLIENT_SRC, isStyle);

export const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");
