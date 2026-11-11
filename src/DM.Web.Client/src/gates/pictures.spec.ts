/**
 * @vitest-environment node
 */

/**
 * A picture is never rounded, nor the frame or the shimmer that stands for it,
 * and the one picture the site rounds is the logos of the partners. The slot of
 * the profile avatar holds the square only while nobody measured the picture.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  CLIENT_SRC,
  asPath,
  styleFiles,
} from "@/shared/lib/testing/sourceTree";
import {
  GLOBAL_SHEETS,
  compileSass,
  sassBlocks,
  sassPrelude,
  styleRules,
} from "@/shared/lib/testing/styles";

describe("the profile avatar slot", () => {
  const profilePage = () =>
    readFileSync(join(CLIENT_SRC, "pages/profile/ProfilePage.vue"), "utf8");

  it("reserves no height of its own for a picture whose size is known", () => {
    // The reservation belongs on the img, where width/height give the browser
    // the real ratio before the decode; a floor on the wrapper reserves a
    // square under a landscape picture.
    const block = /\n\.avatar-wrapper\n([\s\S]*?)\n\n/.exec(profilePage());
    expect(
      block,
      "ProfilePage.vue has no .avatar-wrapper block",
    ).not.toBeNull();
    expect(block?.[1]).not.toMatch(/min-height/);
  });

  it("still holds the square when nobody measured the picture", () => {
    // An upload made before the pipeline recorded its dimensions leaves
    // AvatarImg declaring a square, and the square then has to be held by the
    // slot: without it the box shrinks on decode and pulls the role line, the
    // statistics and the tabs up with it.
    const source = profilePage();
    const block = /\n\.avatar-wrapper-unsized\n([\s\S]*?)\n\n/.exec(source);
    expect(
      block,
      "ProfilePage.vue has no .avatar-wrapper-unsized block",
    ).not.toBeNull();
    // The floor is the side of the square, written once as a layout variable.
    expect(block?.[1]).toMatch(/min-height:\s*\$profile-avatar-size\b/);
    expect(
      readFileSync(join(CLIENT_SRC, "assets/styles/Variables.sass"), "utf8"),
    ).toMatch(/\$profile-avatar-size:\s*220px/);
    expect(
      source,
      "the modifier has to be bound, or the floor is unreachable",
    ).toContain("'avatar-wrapper-unsized': !avatarSizeIsKnown");
  });
});

/**
 * Words that name a picture in a class, read word by word: "logo" is not the
 * start of "logout", nor "cover" the end of "recover". "preview" is left out:
 * on this tree it names a chat in the list of chats, the text of a message and
 * a strip of a hint, and the thumbnail of an upload is a "thumb". A QR code is
 * a picture the page draws, and its white field is the frame that holds it.
 */
const PICTURE_WORDS = new Set([
  "avatar",
  "image",
  "img",
  "picture",
  "photo",
  "thumb",
  "thumbnail",
  "cover",
  "logo",
  "portrait",
  "qr",
]);

/** The elements that are a picture whatever their class says. */
const PICTURE_ELEMENTS = new Set(["img", "picture"]);

/**
 * Words that make a class with a picture word a control that acts on one: the
 * cross of the viewer, a button that loads an avatar. A control keeps the
 * site's radius.
 */
const CONTROL_WORDS = new Set(["btn", "button", "close", "toggle", "input"]);

/** The stylesheets the build compiles, compiled the same way, one by one. */
function compiledSheets(): { path: string; css: string }[] {
  const prelude = new Set(
    [...sassPrelude().matchAll(/@use "@\/([\w/.-]+)"/g)].map(
      (found) => `${found[1]}.sass`,
    ),
  );
  const sheets: { path: string; css: string }[] = [];
  for (const file of styleFiles()) {
    const path = asPath(file);
    const source = readFileSync(file, "utf8");
    if (path.endsWith(".vue")) {
      if (!/<style[^>]*lang="sass"/.test(source)) continue;
      sheets.push({ path, css: compileSass(sassBlocks(file), file) });
    } else if (path.endsWith(".css")) {
      sheets.push({ path, css: source });
    } else if (GLOBAL_SHEETS.has(path) && !prelude.has(path)) {
      // A partial only declares mixins, and they are compiled where they are
      // included; a sheet main.ts imports is where its rules are emitted.
      sheets.push({ path, css: compileSass(source, file) });
    }
  }
  return sheets;
}

/**
 * The element a selector styles: its last compound, with what only wraps or
 * filters it taken away (:deep() unwrapped; :not(), :has(), arguments and
 * attributes dropped, so ":not(img)" is not read as a picture).
 */
function subjectOf(selector: string): string {
  let plain = selector.replace(/:(?:deep|global|slotted)\(([^()]*)\)/g, "$1");
  for (let before = ""; before !== plain;) {
    before = plain;
    plain = plain.replace(/\([^()]*\)/g, "");
  }
  plain = plain.replace(/\[[^\]]*\]/g, "");
  const compounds = plain.trim().split(/\s*[\s>+~]\s*/);
  return compounds[compounds.length - 1] ?? "";
}

/** Whether a compound selects a picture, or the frame that only holds one. */
function isPicture(compound: string): boolean {
  const element = compound.match(/^([a-z][a-z0-9-]*)/)?.[1];
  if (element && PICTURE_ELEMENTS.has(element)) return true;
  return [...compound.matchAll(/\.([\w-]+)/g)].some(([, name]) => {
    const words = name.toLowerCase().split(/[-_]+/);
    return (
      words.some((word) => PICTURE_WORDS.has(word)) &&
      !words.some((word) => CONTROL_WORDS.has(word))
    );
  });
}

/** A value that leaves the corner square. */
const SQUARE = /^(?:(?:0(?:px|%|em|rem)?\s*)+|initial|unset)$/;

/**
 * The one picture the site rounds: the logos of the partners. Exact to the
 * file, the selector and the value, so that no other picture shelters under it.
 */
const ROUND_PICTURE =
  "widgets/sidebar/PartnersPanel.vue: .partner-logo { border-radius: 50% }";

describe("a picture", () => {
  it("is never rounded, nor the frame or the shimmer that stands for it", () => {
    // Read off the compiled sheets, so a radius that arrives through a mixin
    // (+skeleton-shimmer) is caught where it lands.
    const offenders: string[] = [];
    let pictures = 0;
    let roundPicture = false;
    for (const { path, css } of compiledSheets()) {
      for (const { selector, declarations } of styleRules(css)) {
        const rounding = Object.entries(declarations).filter(
          ([property, value]) =>
            (/^border(?:-[a-z]+)*-radius$/.test(property) &&
              !SQUARE.test(value)) ||
            (property === "clip-path" &&
              /\b(?:circle|ellipse|round)\b/.test(value)),
        );
        for (const one of selector.split(",")) {
          if (!isPicture(subjectOf(one))) continue;
          pictures += 1;
          for (const [property, value] of rounding) {
            const found = `${path}: ${one.trim()} { ${property}: ${value} }`;
            if (found === ROUND_PICTURE) roundPicture = true;
            else offenders.push(found);
          }
        }
      }
    }
    expect(offenders).toEqual([]);
    // The exception is in use, so it cannot outlive the round logos.
    expect(roundPicture, ROUND_PICTURE).toBe(true);
    // The scan sees the avatars, thumbnails and pictures of the tree.
    expect(pictures).toBeGreaterThan(30);
  }, 120_000);

  it("is told from a control that acts on one", () => {
    for (const picture of [
      "img",
      ".upload-thumb",
      ".partner-logo",
      ".skeleton-avatar",
      ".bb-image-frame",
      ".avatar-wrapper-unsized",
      ".content :deep(img)",
      ".chain-popover .character-picture:hover",
      ".default-avatar-bg",
    ])
      expect(isPicture(subjectOf(picture)), picture).toBe(true);
    for (const other of [
      ".image-viewer-close",
      ".image-viewer-close::before",
      ".avatar-upload-btn",
      ".logout-dropdown",
      ".recover-link",
      ".messages-container::-webkit-scrollbar-thumb",
      ".chat-preview",
      ".avatar img + .badge",
      "button:not(img)",
      ".gallery > .caption",
    ])
      expect(isPicture(subjectOf(other)), other).toBe(false);
  });
});
