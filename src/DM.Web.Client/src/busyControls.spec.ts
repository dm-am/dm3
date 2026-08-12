/**
 * @vitest-environment node
 */

/**
 * A control that is waiting says so on screen, and it says it in one place.
 *
 * The `loading` prop of shared/ui/Button set `disabled` and `aria-busy` and
 * drew nothing else, and a dimmed button is what a merely disabled one looks
 * like too. So fifteen call sites wrote the state into the caption instead,
 * each picking its own word for it: "Сохранение..." in eleven of them,
 * "Сохраняем..." in one, then "Создание...", "Отмечаю...", "Отправка...",
 * "Выдаем...", "Загрузка...". A caption says what a control does, not what it
 * is doing, and a caption swapped mid-request re-measures the control: the
 * "Отмена" beside it moves while the reader is aiming at it.
 *
 * Two facts hold that shut.
 *
 * The state is drawn by one mixin, +button-busy, and both controls that take a
 * loading prop include it: shared/ui/Button and the confirm button of
 * shared/ui/ConfirmDialog, which nineteen call sites drive. The mixin is keyed
 * off aria-busy rather than off a class of its own, so what the screen shows
 * and what a screen reader announces cannot drift apart, and its travel frames
 * sit in the one stylesheet main.ts emits, for the reason styleSystem.spec.ts
 * keeps every set of frames out of the partials.
 *
 * And no caption carries the state. The shape is read off the source: a
 * conditional whose two branches are both string literals, one of them
 * spelling a progress state, which is letters and then the ellipsis. The bare
 * "..." is not that shape. UI_STANDARDS lists it as the inline indicator, the
 * per-row actions of the account sections use it, and it puts no second
 * wording of one action on the screen.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

/** This spec sits at the root of the client sources. */
const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/** The controls whose loading prop has to reach the screen. */
const BUSY_CONTROLS = [
  "shared/ui/Button/Button.vue",
  "shared/ui/ConfirmDialog/ConfirmDialog.vue",
];

/** A ternary whose two branches are string literals, in either quote. */
const TWO_STRING_TERNARIES = [
  /\?\s*"([^"]*)"\s*:\s*"([^"]*)"/g,
  /\?\s*'([^']*)'\s*:\s*'([^']*)'/g,
];

/** A branch that spells a progress state: letters, and then the ellipsis. */
const WORDED_PROGRESS = /\p{L}.*\.\.\.$/u;

function collectFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collectFiles(full, out);
    } else if (
      (full.endsWith(".ts") || full.endsWith(".vue")) &&
      !full.endsWith(".spec.ts") &&
      !full.endsWith(".d.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

/** Path as a failure message spells it: relative, forward slashes. */
const where = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

const read = (path: string): string =>
  readFileSync(join(CLIENT_SRC, path), "utf8");

describe("the busy state of a control", () => {
  it("is declared in one place", () => {
    const inputs = read("assets/styles/Inputs.sass");
    expect(inputs).toContain("@mixin button-busy()");
    expect(inputs).toContain('&[aria-busy="true"]::after');

    // One tempo for every wait on the site, from one token.
    expect(inputs).toContain("$loading-tempo");
    expect(read("assets/styles/Variables.sass")).toMatch(
      /\$loading-tempo:\s*1\.5s/,
    );
    expect(read("assets/styles/_Skeleton.sass")).toContain("$loading-tempo");

    // The frames, in the sheet main.ts emits once and never in a partial.
    expect(read("assets/styles/Reset.sass")).toContain(
      "@keyframes button-busy",
    );
  });

  it("is taken from that place by every control that takes a loading prop", () => {
    const offenders = BUSY_CONTROLS.filter(
      (path) => !read(path).includes("+button-busy"),
    ).map(
      (path) =>
        `${path}: a loading prop that only dims the control is the disabled state under another name`,
    );
    expect(offenders).toEqual([]);
  });

  it("is announced by the attribute it is drawn off", () => {
    const offenders = BUSY_CONTROLS.filter(
      (path) => !read(path).includes(':aria-busy="loading || undefined"'),
    ).map(
      (path) =>
        `${path}: the bar is keyed off aria-busy, and the markup sets nothing`,
    );
    expect(offenders).toEqual([]);
  });
});

describe("a caption", () => {
  it("never swaps itself for the state of the request", () => {
    const offenders: string[] = [];
    let scanned = 0;
    for (const file of collectFiles(CLIENT_SRC)) {
      scanned++;
      const raw = readFileSync(file, "utf8");
      if (!raw.includes("...")) continue;
      for (const shape of TWO_STRING_TERNARIES) {
        for (const [, whenTrue, whenFalse] of raw.matchAll(shape)) {
          for (const branch of [whenTrue, whenFalse]) {
            if (!WORDED_PROGRESS.test(branch)) continue;
            offenders.push(
              `${where(file)}: "${branch}" is the state of a request written into a caption; pass :loading and leave the wording alone`,
            );
          }
        }
      }
    }
    expect(offenders).toEqual([]);
    // A rule that reads nothing passes.
    expect(scanned).toBeGreaterThan(300);
  });
});
