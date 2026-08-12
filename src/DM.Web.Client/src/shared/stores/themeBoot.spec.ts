/**
 * @vitest-environment node
 */

/**
 * Two readers answer the question "which theme is this device in", and only
 * one of them is in the bundle. The anti-FOUC script of index.html runs before
 * the bundle exists — it is what keeps a reader who chose the dark theme from
 * being shown a white page for the length of a download — and it answers on
 * its own: the same key in localStorage, the same two names, the same fallback
 * to the system preference.
 *
 * Nothing holds the two together. The store keeps the key private and a script
 * in the document cannot import it, so a rename on either side is silent: the
 * page paints light, the bundle arrives and repaints it dark, which is the
 * flash the script exists to prevent. This file is what turns that red.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { Theme } from "@/shared/api/models/personal";

const HERE = dirname(fileURLToPath(import.meta.url));
// stores -> shared -> src -> DM.Web.Client
const CLIENT_ROOT = resolve(HERE, "..", "..", "..");

const storeSource = readFileSync(join(HERE, "ui.ts"), "utf8");
const indexHtml = readFileSync(join(CLIENT_ROOT, "index.html"), "utf8");

/** The string literal a top-level constant of the store is declared with. */
const storeConstant = (name: string): string => {
  const declaration = new RegExp(`${name}\\s*=\\s*"([^"]+)"`).exec(storeSource);
  if (!declaration) throw new Error(`ui.ts declares no ${name}`);
  return declaration[1];
};

/** The media query the store asks the system. */
const storeMediaQuery = (): string => {
  const call = /matchMedia\(\s*"([^"]+)"/.exec(storeSource);
  if (!call) throw new Error("ui.ts asks the system nothing");
  return call[1];
};

/** The anti-FOUC script: the only script of the document with no src. */
const antiFouc = (): string => {
  const script = /<script>([\s\S]*?)<\/script>/.exec(indexHtml);
  if (!script) throw new Error("index.html carries no inline script");
  return script[1];
};

describe("theme before the bundle", () => {
  it("reads the key the store writes", () => {
    const key = storeConstant("THEME_STORAGE_KEY");

    // Pinned rather than only compared: the key is a name already written on
    // every reader's device, so renaming it on both sides at once still drops
    // every choice ever made. It is the literal the store specs assert against.
    expect(key).toBe("dm_theme");
    expect(antiFouc()).toContain(`localStorage.getItem("${key}")`);
  });

  it("knows every theme by the name it is stored under", () => {
    const script = antiFouc();

    const unknown = Object.values(Theme).filter(
      (theme) => !script.includes(`"${theme}"`),
    );

    expect(unknown).toEqual([]);
  });

  it("asks the system the question the store asks", () => {
    expect(antiFouc()).toContain(`matchMedia("${storeMediaQuery()}")`);
  });

  it("asks it only after the key, as the store does", () => {
    const script = antiFouc();

    // The stored choice wins and the system preference is the fallback. The
    // other way round, the script would paint the system theme over a choice
    // the reader made, and the bundle would correct it after the first paint.
    expect(script.indexOf("localStorage.getItem")).toBeLessThan(
      script.indexOf("matchMedia"),
    );
  });
});
