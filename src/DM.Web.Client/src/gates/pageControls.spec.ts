/**
 * @vitest-environment node
 */

/**
 * Tap targets: WCAG 2.5.8 puts the floor for a pointer target at 24x24 CSS px.
 * The glyph may stay small, the mixin grows the target around it, so what is
 * checked is that the icon-only controls take the mixin and that the mixin
 * declares a floor.
 *
 * The controls of a page: every button and every field of one line is the
 * search, "Фильтры" and the sort of the games list's filter bar, 38 high, in
 * 14, the field 8 in from its frame on the ground of $bg-element, the button 16
 * to each side, one radius. A window keeps its 52 and a text area grows with
 * its rows. The caption over a field is bold wherever a page draws one, and a
 * link answers the pointer one way, an <a> and a <button> alike. The corners of
 * these controls are roundedCorners.spec.ts's.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";
import {
  compileSass,
  componentCss,
  ruleMap,
} from "@/shared/lib/testing/styles";

/** Icon-only controls held above the floor. */
const ICON_ONLY = [
  "shared/ui/Filters/primitives/FilterDropdownHeader.vue",
  "shared/ui/ScrollNav/ScrollNav.vue",
  // The cross of a filter bubble, 14 by 14 (=remove-icon-btn).
  "assets/styles/_Filters.sass",
];

describe("the pointer target of an icon-only control", () => {
  it("has a floor declared in one place", () => {
    const variables = readFileSync(
      join(CLIENT_SRC, "assets/styles/Variables.sass"),
      "utf8",
    );
    expect(variables).toMatch(/\$tap-target-min:\s*24px/);

    const buttons = readFileSync(
      join(CLIENT_SRC, "assets/styles/_Buttons.sass"),
      "utf8",
    );
    expect(buttons).toContain("@mixin icon-button(");
    expect(buttons).toContain("$tap-target-min");
  });

  it("is taken from that place by the controls that were under it", () => {
    const offenders = ICON_ONLY.filter(
      (path) =>
        !readFileSync(join(CLIENT_SRC, path), "utf8").includes("+icon-button"),
    ).map(
      (path) =>
        `${path}: an icon-only control sizing its own hit box again — the three that did measured 20x20, 20x20 and 36x18`,
    );
    expect(offenders).toEqual([]);
  });
});

const probe = (block: string) =>
  ruleMap(
    compileSass(
      `@use "@/assets/styles/Inputs" as *\n@use "@/assets/styles/Buttons" as *\n${block}`,
    ),
  );

describe("the site checkbox and radio", () => {
  it.each(["checkbox", "radio"])(
    "%s zeroes the field padding it also inherits, so it is the square it asks for",
    (mixin) => {
      // The same <input> goes through +input() as well and arrives with a text
      // field's padding, so the box zeroes it. The side lives once, as
      // $checkbox-size.
      expect(probe(`.probe\n  +${mixin}`).get(".probe")).toMatchObject({
        padding: "0",
        width: "16px",
        height: "16px",
      });
    },
  );

  it("is measured by the guest check line through its name, not by eye", () => {
    // The check line hangs its indent on the box, by the same token.
    const line = ruleMap(compileSass('@use "@/assets/styles/AuthSurface"')).get(
      ".auth-surface .auth-checkline",
    );
    expect(line?.["padding-left"]).toMatch(/^calc\(16px \+ /);
  });
});

describe("a control on a page", () => {
  const variables = () =>
    readFileSync(join(CLIENT_SRC, "assets/styles/Variables.sass"), "utf8");

  it("is 38 high in 14, the one measure of the page", () => {
    // The filter bar of the games list.
    expect(variables()).toMatch(/^\$control-height:\s*38px$/m);
    expect(variables()).toMatch(
      /^\$control-font-size:\s*\$secondary-font-size$/m,
    );
    expect(variables()).toMatch(/^\$field-inset:\s*\$small$/m);
    expect(variables()).toMatch(/^\$button-padding:\s*0 \$medium$/m);
  });

  it("is a field of one line at that height by declaration, 8 in from its frame, in 14", () => {
    // Declared, and not left to the padding and the font's line box, which
    // put the field near the token by accident and moved it with the font.
    const rules = probe(".probe\n  +input");
    const line = [...rules.entries()].find(([selector]) =>
      selector.startsWith(".probe:where(:not(textarea"),
    );
    expect(line, "+input has no rule for a field of one line").toBeDefined();
    expect(line?.[1]).toMatchObject({
      "box-sizing": "border-box",
      height: "38px",
      padding: "0 8px",
      "font-size": "14px",
    });
    // A text area and the boxes a reader ticks keep their own geometry, and
    // the text area its 16: a post, a note, the text of a tooltip.
    for (const kept of ["textarea", "[type=checkbox]", "[type=radio]"])
      expect(line?.[0], kept).toContain(kept);
    expect(rules.get(".probe")?.["font-size"]).toBe("16px");
  });

  it("is a search of a filter bar as it stood before, the measure of every field", () => {
    const rules = ruleMap(
      compileSass(
        '@use "@/assets/styles/Filters" as *\n.probe\n  +filter-search-container',
      ),
    );
    expect(rules.get(".probe .search-container")).toMatchObject({
      height: "38px",
      "font-size": "14px",
      padding: "0 8px",
      border: "1px solid var(--border)",
      "border-radius": "8px",
      // Over the page this is $bg-element, the ground the search stood on
      // (fillContrast.spec.ts measures that); one ground for every field.
      "background-color": "var(--input-bg-overlay)",
    });
    expect(rules.get(".probe .the-input")?.["font-size"]).toBe("14px");
    const field = probe(".probe\n  +input-base").get(".probe");
    expect(field).toMatchObject({
      "background-color": "var(--input-bg-overlay)",
      border: "1px solid var(--border)",
      "border-radius": "8px",
    });
  });

  it("is a date field of the page at the page's height and type", () => {
    const sheet = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/DatePicker/DateInput.vue")),
    );
    expect(sheet.get(".di-field")?.height).toBe("38px");
    expect(sheet.get(".di-input")?.["font-size"]).toBe("14px");
    expect(sheet.get(".di-input")?.padding).toBe("0 2px 0 8px");
  });

  it("is the page's field in the panel of a filter too, level with its buttons", () => {
    // The panel takes the page's 38, as "Неделя", "Месяц", "Год" and
    // "Применить" do.
    const panel = ruleMap(
      compileSass(
        '@use "@/assets/styles/Filters" as *\n.probe\n  +dropdown-search-input',
      ),
    ).get(".probe .dropdown-search-input");
    expect(panel).toMatchObject({
      height: "38px",
      padding: "0 8px",
      "font-size": "14px",
      "background-color": "var(--input-bg-overlay)",
    });
    // The dates of the range are the page's date field, not its compact
    // variant.
    const range = readFileSync(
      join(CLIENT_SRC, "shared/ui/Filters/inputs/DateRangePicker.vue"),
      "utf8",
    );
    expect(range).toMatch(/<DateInput\b/);
    expect(range).not.toMatch(/<DateInput\b[^>]*\scompact\b/);
    // The numbers of a range, the same field with a step on either side.
    const stepper = ruleMap(
      componentCss(
        join(CLIENT_SRC, "shared/ui/Filters/inputs/NumericRangePicker.vue"),
      ),
    );
    expect(stepper.get(".stepper")).toMatchObject({
      height: "38px",
      "box-sizing": "border-box",
      "background-color": "var(--input-bg-overlay)",
    });
    expect(stepper.get(".stepper-input")?.["font-size"]).toBe("14px");
  });

  it("is a bubble of an applied filter 28 high, with a cross a finger can take", () => {
    // Every bubble of a row stands at 28, the bubble of names and "и еще N"
    // too: the line of 14 in the middle of the 26 between the frames, and the
    // pill of a name as high as that inside.
    const bubbles = ruleMap(
      compileSass(
        '@use "@/assets/styles/FilterBubbles" as *\n.probe\n  +filter-bubbles',
      ),
    );
    const pill = ruleMap(
      componentCss(join(CLIENT_SRC, "entities/game/ui/TagSelector.vue")),
    ).get(".tag-button");
    const bubble = bubbles.get(".probe .bubble");
    for (const size of ["font-size", "border"])
      expect(bubble?.[size], size).toBe(pill?.[size]);
    expect(bubble).toMatchObject({
      "box-sizing": "border-box",
      "min-height": "28px",
      padding: "0 8px",
      "line-height": "26px",
    });
    expect(bubbles.get(".probe .bubble-owner-item")).toMatchObject({
      padding: "2px 4px",
      margin: "-2px 0",
      "line-height": "22px",
    });
    // The copy space would take the line of 26 and grow the bubble.
    expect(bubbles.get(".probe .bubble .copy-space")?.["line-height"]).toBe(
      "0",
    );
    // The cross is drawn 14 and taken at the floor of a target, 24.
    expect(bubbles.get(".probe .bubble-remove-btn")).toMatchObject({
      width: "14px",
      height: "14px",
      position: "relative",
    });
    expect(bubbles.get(".probe .bubble-remove-btn::after")).toMatchObject({
      width: "max(100%, 24px)",
      height: "max(100%, 24px)",
    });
  });

  it("is a button of the filter bar's geometry, beside a pill and a field of the same", () => {
    const segmented = ruleMap(
      componentCss(
        join(CLIENT_SRC, "shared/ui/SegmentedControl/SegmentedControl.vue"),
      ),
    );
    expect(segmented.get(".segmented-control")?.height).toBe("38px");
    const button = probe(".probe\n  +button");
    expect(button.get(".probe")).toMatchObject({
      height: "38px",
      padding: "0 16px",
      "font-size": "14px",
      "border-radius": "8px",
    });
    // The main action is the same box in another colour: the modifier names
    // colours and the weight, never a size of its own.
    const primary = probe(".probe\n  +button-primary").get(".probe") ?? {};
    for (const size of ["height", "padding", "font-size", "border-radius"])
      expect(primary[size], size).toBeUndefined();
  });
});

/** Captions of a field that a page draws by hand, and not through FormField. */
const HAND_CAPTIONS: { file: string; selector: string }[] = [
  { file: "pages/game/room/PostComposer.vue", selector: ".composer-label" },
  { file: "pages/game/room/PostComposer.vue", selector: ".dice-title" },
  {
    file: "features/upload/ui/AttachmentsPicker.vue",
    selector: ".attachments-title",
  },
  { file: "pages/game/room/PostPendencyField.vue", selector: ".turns-title" },
  {
    file: "entities/game/ui/DiceComposer.vue",
    selector: ".dice-comment-caption",
  },
  {
    file: "pages/game/settings/RoomsSection.vue",
    selector: ".grants-caption",
  },
  { file: "widgets/game-post/GamePostEdit.vue", selector: ".edit-label" },
  { file: "features/topic/ui/TopicView.vue", selector: ".edit-label" },
  { file: "pages/game/CharacterEdit.vue", selector: ".switch-label" },
  {
    file: "pages/moderation/FundraisingPage.vue",
    selector: ".form-field label",
  },
  {
    file: "pages/moderation/ModerationAwardsSeries.vue",
    selector: ".form-row label",
  },
];

describe("the caption of a field on a page", () => {
  it("is 14 and bold in the ink of the text, declared once", () => {
    const caption = ruleMap(
      compileSass(
        '@use "@/assets/styles/Inputs" as *\n.probe\n  +field-caption',
      ),
    ).get(".probe");
    expect(caption).toEqual({
      color: "var(--text)",
      "font-size": "14px",
      "font-weight": "700",
    });
  });

  it("is what FormField draws over a field of a page", () => {
    const sheet = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/Form/FormField.vue")),
    );
    const found = sheet.get(".form-field-label");
    expect(found, "FormField.vue has no .form-field-label").toBeDefined();
    expect(found).toMatchObject({
      color: "var(--text)",
      "font-size": "14px",
      "font-weight": "700",
    });
  });

  it("is the same where a page captions a field by hand", () => {
    // A caption a page draws by hand is bold, as FormField draws it.
    const offenders: string[] = [];
    for (const { file, selector } of HAND_CAPTIONS) {
      const sheet = ruleMap(componentCss(join(CLIENT_SRC, file)));
      const found = sheet.get(selector);
      if (!found) {
        offenders.push(`${file}: no ${selector}`);
        continue;
      }
      const { color, "font-size": size, "font-weight": weight } = found;
      if (color !== "var(--text)" || size !== "14px" || weight !== "700")
        offenders.push(
          `${file} ${selector}: ${color} ${size} ${weight}, a caption of a page is var(--text) 14px 700`,
        );
    }
    expect(offenders).toEqual([]);
  });
});

describe("a link", () => {
  const compiled = ruleMap(
    compileSass(
      [
        '@use "@/assets/styles/Links" as *',
        ".blue\n  +inline-link-button",
        ".grey\n  +muted-link",
        ".grey-button\n  +muted-link-button",
      ].join("\n"),
    ),
  );

  it("eases its colour at the buttons' tempo, an <a> as a <button>", () => {
    const reset = ruleMap(
      compileSass(
        readFileSync(join(CLIENT_SRC, "assets/styles/Reset.sass"), "utf8"),
        join(CLIENT_SRC, "assets/styles/Reset.sass"),
      ),
    );
    expect(reset.get("a")?.transition).toBe("color 0.15s ease");
    for (const one of [".blue", ".grey", ".grey-button"])
      expect(compiled.get(one)?.transition, one).toBe("color 0.15s ease");
  });

  it("turns $link-hover with the underline, whatever colour it rests in", () => {
    // As every link of DM2 does, a grey one included.
    expect(compiled.get(".blue")?.color).toBe("var(--link)");
    expect(compiled.get(".grey")?.color).toBe("var(--text-muted)");
    expect(compiled.get(".grey-button")?.color).toBe("var(--text-muted)");
    for (const one of [".blue", ".grey", ".grey-button"]) {
      expect(compiled.get(`${one}:hover:not(:disabled)`), one).toEqual({
        color: "var(--link-hover)",
        "text-decoration": "underline",
      });
      expect(compiled.get(one)?.["text-decoration"], one).toBe("none");
    }
  });
});
