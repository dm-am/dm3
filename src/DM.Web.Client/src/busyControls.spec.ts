/**
 * @vitest-environment node
 */

/**
 * A control that is waiting says so on screen, and it says it in one place.
 *
 * The `loading` prop of shared/ui/Button set `disabled` and `aria-busy` and
 * drew nothing else. So fifteen call sites wrote the state into the caption
 * instead, each picking its own word for it: "Сохранение..." in eleven of them,
 * "Сохраняем..." in one, then "Создание...", "Отмечаю...", "Отправка...",
 * "Выдаем...", "Загрузка...". A caption says what a control does, not what it
 * is doing, and a caption swapped mid-request re-measures the control: the
 * "Отмена" beside it moves while the reader is aiming at it.
 *
 * Two facts hold that shut.
 *
 * A control with a request in flight wears the site's disabled state, which is
 * now a shape and not a shade: +disabled-hatch draws the diagonal hatch over
 * the fill the control already had. There is no second drawing for waiting —
 * the travelling bar that used to be one is gone, together with its frames —
 * because "cannot be used right now" is the whole of what the screen has to
 * say, and saying it twice in two languages is what left the two states
 * looking unrelated. What waiting adds on top is announced and not drawn:
 * aria-busy, which says the control is coming back.
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

/**
 * The controls whose loading prop has to reach the screen.
 *
 * Read off the source, so the list holds only what DRAWS a control. The
 * confirmation window used to draw its own pair of buttons and stood here for
 * that; it now hands both halves of the row to the entry above, and a source
 * scan of it would find the forwarding and not the state. What it forwards is
 * asserted on the rendered window instead, in ConfirmDialog.spec.ts.
 */
const BUSY_CONTROLS = ["shared/ui/Button/Button.vue"];

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
  it("is the disabled state, declared in one place", () => {
    const inputs = read("assets/styles/Inputs.sass");
    // The state is :disabled unless a wrapper that draws the box of a control
    // says it with a class instead (Select); a control that waits is disabled,
    // so the default is the one it wears.
    expect(inputs).toContain('@mixin disabled-hatch($state: ":disabled")');
    expect(inputs).toContain("&#{$state}");

    // One tempo for every wait on the site, from one token.
    expect(read("assets/styles/Variables.sass")).toMatch(
      /\$loading-tempo:\s*1\.5s/,
    );
    expect(read("assets/styles/_Skeleton.sass")).toContain("$loading-tempo");
  });

  /**
   * The hatch has a weight of its own, and every place that draws it uses that
   * one weight.
   *
   * It used to be $disabled-opacity twice over: the layer took the same 0.5 as
   * the dimming of the control around it, and the read-only plate of the guest
   * surface, which has no dimming of its own, wrote the square of that number
   * out by hand. Measured headless against the fill each stroke lies on, in
   * both themes: 1.57 to 2.01 by WCAG 1.4.3 on the plate, on the checkbox and
   * on the filled button alike. No place was darker than the rest and all of
   * them were heavier than a hatch has to be, which is the answer to "does this
   * one field match the others, or are they all too dark".
   *
   * The weight is now its own token at 0.35, and the same three measure 1.36 to
   * 1.62. What this holds is that there is ONE weight: a second number typed
   * into either drawing is how the plate and the button drift apart again.
   */
  it("draws its hatch at one weight, in both places that draw it", () => {
    const inputs = read("assets/styles/Inputs.sass");
    const surface = read("assets/styles/AuthSurface.sass");

    expect(inputs).toMatch(/\$disabled-hatch-opacity:\s*0\.35/);

    // The mixin: the control is dimmed by one token and the hatch over it by
    // the other. Two different tokens, or the layer is dimming twice by
    // accident again.
    const mixin = inputs.slice(inputs.indexOf("@mixin disabled-hatch("));
    expect(mixin).toContain("opacity: $disabled-opacity");
    expect(mixin).toContain("opacity: $disabled-hatch-opacity");

    // The plate is not a control and carries no dimming, so it writes the
    // product the mixin reaches by nesting. Written as the two tokens and not
    // as a number, or it stops following them.
    expect(
      surface,
      "плита читаемого значения рисует штриховку своей плотностью",
    ).toContain("opacity: $disabled-opacity * $disabled-hatch-opacity");
  });

  it("draws no second state of its own", () => {
    // The travelling bar and its frames. A control that waits and a control
    // that cannot be used are one picture, so a mixin keyed off aria-busy is
    // a second language for a state that already has one — and the frames it
    // animated are dead weight in the one sheet main.ts emits.
    const offenders: string[] = [];
    for (const file of collectFiles(CLIENT_SRC)) {
      const source = readFileSync(file, "utf8");
      if (!/\+button-busy|@keyframes button-busy/.test(source)) continue;
      offenders.push(`${where(file)}: the busy bar is back`);
    }
    for (const sheet of [
      "assets/styles/Inputs.sass",
      "assets/styles/Reset.sass",
    ]) {
      if (!/button-busy/.test(read(sheet))) continue;
      offenders.push(`${sheet}: the busy bar is back`);
    }
    expect(offenders).toEqual([]);
  });

  it("stops the control, on the screen and in the tree alike", () => {
    const offenders: string[] = [];
    for (const path of BUSY_CONTROLS) {
      const source = read(path);
      // The hatch is drawn off :disabled, so the attribute IS the state.
      if (!/:disabled="loading/.test(source)) {
        offenders.push(
          `${path}: a loading prop that leaves the control clickable is not a state at all`,
        );
      }
      if (!source.includes(':aria-busy="loading || undefined"')) {
        offenders.push(
          `${path}: nothing tells a screen reader the control is coming back`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });

  it("is taken from that place by every control that takes a loading prop", () => {
    // Through +button, which is where +disabled-hatch is included.
    const offenders = BUSY_CONTROLS.filter(
      (path) => !read(path).includes("+button"),
    ).map(
      (path) =>
        `${path}: a control drawing its own idea of "unavailable" is how the state stopped being one`,
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
