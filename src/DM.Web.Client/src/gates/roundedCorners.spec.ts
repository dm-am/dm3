/**
 * @vitest-environment node
 */

/**
 * A card of content is square and a control is rounded by the one radius of
 * the site; a literal radius is a named exception or a defect.
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
  compileSass,
  componentCss,
  ruleMap,
  sassBlocks,
} from "@/shared/lib/testing/styles";

/** What a rounded corner may be written as without a word of explanation. */
const RADIUS_TOKENS = new Set([
  "$border-radius",
  "$bubble-radius",
  "inherit",
  "0",
]);

/**
 * Every other radius on the tree, file by file and value by value, with the
 * reason it is not the site's one. A control is rounded by $border-radius and
 * a card of content not at all; what stands here is neither, or it is a defect
 * that lives in a zone of its own and is named as one.
 */
const RADIUS_REGISTER: Record<
  string,
  { values: string[]; why: string; open?: true }
> = {
  "assets/styles/Inputs.sass": {
    values: ["2px", "50%"],
    why: "the checkbox, 2 on a side of 16 (the site's 8 would draw it round, as the radio), and the radio with its dot",
  },
  "assets/styles/_AuthNotice.sass": {
    values: ["2px"],
    why: "the bars of the marks the guest statuses draw, of a screen and of a field",
  },
  "assets/styles/_AuthTokens.sass": {
    values: ["1px"],
    why: "the two bars of the guest cross, drawn on the card's close and on the icon of a refused name",
  },
  "assets/styles/_DialogSurface.sass": {
    values: ["1px"],
    why: "the two bars of a window's cross",
  },
  "assets/styles/_Skeleton.sass": {
    values: ["$minor"],
    why: "a shimmering line of text waiting to load, not a control",
  },
  "assets/styles/Reset.sass": {
    values: ["3px"],
    why: "the marker pen over the letters of a search match",
  },
  "entities/testimonial/ui/TestimonialSkeleton.vue": {
    values: ["3px"],
    why: "a shimmering line of text waiting to load",
  },
  "widgets/chat-feed/ChatFeed.vue": {
    values: ["7px"],
    why: "the thumb of the feed's scrollbar, in both chats",
  },
  "pages/profile/ProfileAchievementsSection.vue": {
    values: ["$minor", "3px"],
    why: "the tier badge on an icon and the 6px bar of a chain's progress",
  },
  "pages/profile/ProfileAchievementPopover.vue": {
    values: ["$minor"],
    why: "the tier badge on a row of the popover",
  },
  "pages/profile/ProfileAwardsSection.vue": {
    values: ["$minor"],
    why: "the tier badge on an icon",
  },
  "pages/rules/RulesIntro.vue": {
    values: ["$tiny"],
    why: "a tag of the markup quoted in the text, set as code",
  },
  "shared/ui/PasswordInput/PasswordStrengthIndicator.vue": {
    values: ["2px"],
    why: "the bars of the strength scale",
  },
  "shared/ui/SiteSettings/SiteSettingsPanel.vue": {
    values: ["18px", "50%"],
    why: "the track of a switch and its knob",
  },
  "widgets/footer/Footer.vue": {
    values: ["50%"],
    why: "the ring of the age mark, text and not a control",
  },
  "widgets/sidebar/PartnersPanel.vue": {
    values: ["50%"],
    why: "the round logos of the partners, the one picture of the site that is rounded",
  },
  "shared/ui/BBCodeEditor/BBCodeEditorToolbar.vue": {
    values: ["2px"],
    why: "the tag buttons of the toolbar, drawn as DM2's: the site's 8 rounded them into controls of another kind",
  },
};

/** The radii a style file declares, comments left out. */
function radii(file: string): string[] {
  const path = asPath(file);
  const source = readFileSync(file, "utf8");
  const styles = path.endsWith(".vue")
    ? [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)]
        .map((found) => found[1])
        .join("\n")
    : source;
  const clean = styles
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .map((one) => one.replace(/\/\/.*$/, ""))
    .join("\n");
  return [...clean.matchAll(/^\s*border-radius:\s*([^\n;]+)/gm)].map((found) =>
    found[1].trim(),
  );
}

describe("a rounded corner", () => {
  it("is the site's one radius, or an exception with a reason", () => {
    // A control wears the token of a control, not a radius of its own.
    const offenders: string[] = [];
    let seen = 0;
    for (const file of styleFiles()) {
      const path = asPath(file);
      for (const value of radii(file)) {
        seen += 1;
        if (RADIUS_TOKENS.has(value)) continue;
        if (RADIUS_REGISTER[path]?.values.includes(value)) continue;
        offenders.push(
          `${path}: border-radius: ${value} — a control takes $border-radius, a card of content none; anything else goes into RADIUS_REGISTER with its reason`,
        );
      }
    }
    expect(offenders).toEqual([]);
    expect(seen).toBeGreaterThan(50);
  });

  it("keeps no reason for a radius that is gone", () => {
    // An open defect is left out: it goes from the register by hand when its
    // zone fixes it, and a fix elsewhere must not turn this file red.
    const stale: string[] = [];
    for (const [path, { values, open }] of Object.entries(RADIUS_REGISTER)) {
      if (open) continue;
      const found = radii(join(CLIENT_SRC, path));
      for (const value of values)
        if (!found.includes(value)) stale.push(`${path}: ${value}`);
    }
    expect(stale).toEqual([]);
  });

  it("is the site's one radius on the controls that wore another", () => {
    // The chevron of a filter bubble (2), the round cross of the notepad
    // (50%), the sign buttons of a post's rating (none) and the badge of a
    // tooltip (999px). Named, so a regression reads as the control it hit.
    const fixed: [string, string][] = [
      ["assets/styles/_FilterBubbles.sass", ".bubble-expand-btn"],
      ["widgets/notepad/NotepadEntryEditor.vue", ".close-btn"],
    ];
    for (const [file, selector] of fixed) {
      const css = file.endsWith(".vue")
        ? // Compiled as the file, so its own relative @use lines resolve.
          compileSass(
            sassBlocks(join(CLIENT_SRC, file)),
            join(CLIENT_SRC, file),
          )
        : compileSass(
            `@use "@/assets/styles/FilterBubbles" as *\n.probe\n  +filter-bubbles`,
          );
      const rule = [...ruleMap(css).entries()].find(
        ([one]) => one === selector || one === `.probe ${selector}`,
      );
      expect(rule, `${file}: no ${selector}`).toBeDefined();
      expect(rule?.[1]["border-radius"], `${file} ${selector}`).toBe("8px");
    }
  });

  it("is DM2's 2 on the tag buttons of the editor, not the site's 8", () => {
    // Named here and not only in the register: the token passes the first
    // test of this block.
    const file = join(
      CLIENT_SRC,
      "shared/ui/BBCodeEditor/BBCodeEditorToolbar.vue",
    );
    const rule = ruleMap(compileSass(sassBlocks(file), file)).get(".tag-btn");
    expect(rule, "BBCodeEditorToolbar.vue: no .tag-btn").toBeDefined();
    expect(rule?.["border-radius"]).toBe("2px");
  });
});

describe("a card of content", () => {
  it("is square, the radius being the mark of a control", () => {
    const card = ruleMap(compileSass(".probe\n  +card")).get(".probe");
    expect(card?.border).toBe("1px solid var(--border)");
    expect(card?.["border-radius"]).toBeUndefined();
  });

  it("is not rounded again by the block that takes the recipe", () => {
    const offenders: string[] = [];
    for (const file of styleFiles()) {
      const lines = readFileSync(file, "utf8").split("\n");
      lines.forEach((line, at) => {
        if (!/^\s*\+card\b/.test(line)) return;
        const indent = line.length - line.trimStart().length;
        // The declarations of the same block, on both sides of the include.
        for (const step of [-1, 1]) {
          for (let i = at + step; i >= 0 && i < lines.length; i += step) {
            const other = lines[i];
            if (!other.trim()) continue;
            const own = other.length - other.trimStart().length;
            if (own < indent) break;
            if (own === indent && /^\s*border-radius:/.test(other))
              offenders.push(`${asPath(file)}:${i + 1}: ${other.trim()}`);
          }
        }
      });
    }
    expect(offenders).toEqual([]);
  });

  it("is square in the shared panels that are not controls", () => {
    for (const [file, selector] of [
      ["shared/ui/SettingsSection/SettingsSection.vue", ".settings-section"],
      ["shared/ui/ErrorState/ErrorState.vue", ".error-state"],
      ["shared/ui/Skeleton/CharacterCardSkeleton.vue", ".skeleton-card"],
      ["shared/ui/Skeleton/ChatPreviewSkeleton.vue", ".skeleton-chat"],
    ]) {
      const rule = ruleMap(componentCss(join(CLIENT_SRC, file))).get(selector);
      expect(rule, `${file} has no ${selector}`).toBeDefined();
      expect(rule?.["border-radius"], file).toBeUndefined();
    }
  });
});
