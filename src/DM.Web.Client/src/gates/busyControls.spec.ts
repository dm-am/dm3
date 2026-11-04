/**
 * @vitest-environment node
 */

/**
 * A control that is waiting says so in one place: it wears the site's disabled
 * state (+disabled-hatch over its own fill) and announces aria-busy, and no
 * caption carries the state. A caption says what a control does, not what it
 * is doing, and a caption swapped mid-request re-measures the control while
 * the reader is aiming at it. The bare "..." is not a worded state: UI_STANDARDS
 * lists it as the inline indicator.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { SURFACE } from "@/shared/lib/testing/guestSheet";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** The control whose loading prop has to reach the screen, read off the source. */
const BUTTON = "shared/ui/Button/Button.vue";

/** A ternary whose two branches are string literals, in either quote. */
const TWO_STRING_TERNARIES = [
  /\?\s*"([^"]*)"\s*:\s*"([^"]*)"/g,
  /\?\s*'([^']*)'\s*:\s*'([^']*)'/g,
];

/** A branch that spells a progress state: letters, and then the ellipsis. */
const WORDED_PROGRESS = /\p{L}.*\.\.\.$/u;

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) &&
  !name.endsWith(".spec.ts") &&
  !name.endsWith(".d.ts");

const read = (path: string): string =>
  readFileSync(join(CLIENT_SRC, path), "utf8");

describe("the busy state of a control", () => {
  it("is the disabled state, declared in one place", () => {
    const buttons = read("assets/styles/_Buttons.sass");
    // The state is :disabled unless a wrapper that draws the box of a control
    // says it with a class instead (Select); a control that waits is disabled,
    // so the default is the one it wears.
    expect(buttons).toContain('@mixin disabled-hatch($state: ":disabled")');
    expect(buttons).toContain("&#{$state}");

    // One tempo for every wait on the site, from one token.
    expect(read("assets/styles/Variables.sass")).toMatch(
      /\$loading-tempo:\s*1\.5s/,
    );
    expect(read("assets/styles/_Skeleton.sass")).toContain("$loading-tempo");
  });

  /**
   * The hatch has a weight of its own, and every place that draws it uses that
   * one weight: a second number typed into either drawing is how the plate and
   * the button drift apart.
   */
  it("draws its hatch at one weight, in both places that draw it", () => {
    const buttons = read("assets/styles/_Buttons.sass");
    const surface = SURFACE;

    expect(buttons).toMatch(/\$disabled-hatch-opacity:\s*0\.35/);

    // The mixin: the control is dimmed by one token and the hatch over it by
    // the other.
    const mixin = buttons.slice(buttons.indexOf("@mixin disabled-hatch("));
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

  it("stops the control, on the screen and in the tree alike", () => {
    const source = read(BUTTON);
    // The hatch is drawn off :disabled, so the attribute IS the state.
    expect(
      source,
      `${BUTTON}: a loading prop that leaves the control clickable is not a state at all`,
    ).toMatch(/:disabled="loading/);
    expect(
      source,
      `${BUTTON}: nothing tells a screen reader the control is coming back`,
    ).toContain(':aria-busy="loading || undefined"');
  });

  it("is taken from that place by every control that takes a loading prop", () => {
    // Through +button, which is where +disabled-hatch is included.
    expect(
      read(BUTTON),
      `${BUTTON}: a control drawing its own idea of "unavailable" is how the state stopped being one`,
    ).toContain("+button");
  });
});

describe("a caption", () => {
  it("never swaps itself for the state of the request", () => {
    const offenders: string[] = [];
    let scanned = 0;
    for (const file of collect(CLIENT_SRC, isSource)) {
      scanned++;
      const raw = readFileSync(file, "utf8");
      if (!raw.includes("...")) continue;
      for (const shape of TWO_STRING_TERNARIES) {
        for (const [, whenTrue, whenFalse] of raw.matchAll(shape)) {
          for (const branch of [whenTrue, whenFalse]) {
            if (!WORDED_PROGRESS.test(branch)) continue;
            offenders.push(
              `${asPath(file)}: "${branch}" is the state of a request written into a caption; pass :loading and leave the wording alone`,
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
