/**
 * @vitest-environment node
 */

/**
 * The field of a guest card against the prototype it was drawn from: the strip
 * under a field, the code field of the second factor, the eye of a password
 * field and the check line under the password. Read off the sheet as text for
 * the reason guestSurface.spec.ts gives: jsdom applies no stylesheet.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";
import { SURFACE, ruleBody } from "@/shared/lib/testing/guestSheet";

const read = (...parts: string[]): string =>
  readFileSync(join(CLIENT_SRC, ...parts), "utf8");

const PASSWORD_INPUT = read(
  "shared",
  "ui",
  "PasswordInput",
  "PasswordInput.vue",
);

describe("the strip under a field", () => {
  /**
   * Nothing in the strip is indented: every other form of the site prints the
   * reason under a field with no inset either.
   */
  it("indents nothing in it", () => {
    // The message keeps its own block for the top margin; what must not come
    // back is a left inset in it.
    expect(
      ruleBody(SURFACE, ".auth-surface .auth-field + .auth-message"),
      "the line under a field is indented again",
    ).not.toContain("padding-left");

    expect(
      SURFACE,
      "the inset is used by the controls themselves, not by text",
    ).toContain("$auth-field-inset: 14px");
    expect(SURFACE, "an indented strip is back").not.toContain(
      "padding-left: calc(#{$auth-field-inset} + 1px)",
    );
    expect(SURFACE, "the caption of the scale is indented again").not.toContain(
      ".auth-surface .auth-field + .auth-meter > :not(.strength-bar)",
    );
    expect(SURFACE, "the verdict on a name is indented again").not.toContain(
      ".auth-surface .auth-field .username-status",
    );
  });

  it("holds its place under the code field and says nothing", () => {
    // The shape of the code is shown by the field itself. The strip is held
    // open for the refusal, and it is hidden rather than emptied so its height
    // does not depend on what it happens to hold.
    expect(
      ruleBody(
        SURFACE,
        ".auth-surface .auth-message--reserved:not(.auth-message--error)",
      ),
    ).toContain("visibility: hidden");
  });
});

describe("the second factor", () => {
  it("sets the code in the surface own face and pays the spacing back", () => {
    const field = ruleBody(
      SURFACE,
      ".auth-surface .auth-field input.auth-input--code",
    );

    expect(field).toContain("text-align: center");
    // The spacing is added after the last symbol too, so the same amount comes
    // back as an indent: without it the line sits off centre by half a gap.
    expect(field).toContain("letter-spacing: .3em");
    expect(field).toContain("text-indent: .3em");
    // No family of its own and no height of its own: one field set apart from
    // the rest of the form read as a piece of someone else s.
    expect(field, "the code took a face of its own again").not.toContain(
      "font-family",
    );
    expect(field, "the code took a height of its own again").not.toContain(
      "height:",
    );
  });

  it("sets the longer recovery code tighter", () => {
    const recovery = ruleBody(
      SURFACE,
      ".auth-surface .auth-field input.auth-input--code.auth-input--code-recovery",
    );

    expect(recovery).toContain("letter-spacing: .1em");
    expect(recovery).toContain("text-indent: .1em");
  });

  it("takes the switch between the two kinds from the site's own control", () => {
    const pill = ruleBody(SURFACE, ".auth-surface .auth-card .auth-segmented");

    // Only what this card changes: the pill runs the full width and the two
    // halves split it. A fill, an edge or a weight written here would be the
    // component redrawn by hand, and a copy drifts from what it copied.
    expect(pill).toContain("width: 50%");
    expect(pill).not.toContain("background");
    expect(pill).not.toContain("border-radius");
    expect(pill).not.toContain("font-weight");
  });
});

describe("the eye of a password field", () => {
  it("is the size of the prototype's and in its ink", () => {
    const toggle = PASSWORD_INPUT.slice(
      PASSWORD_INPUT.indexOf(".password-toggle"),
    );

    expect(toggle).toContain("width: 19px");
    expect(toggle).toContain("height: 19px");
    // Full-strength ink from the start: dimmed until the pointer finds it, the
    // one control of a password field reads as unavailable.
    expect(toggle).toContain("color: $text");
    expect(toggle).not.toContain("color: $text-muted");
  });
});

describe("the check line between a field and the button", () => {
  /**
   * "Запомнить меня" stands under the strip of the password field and over the
   * button, and the strip is empty until the server refuses. So the row is
   * placed twice, and both placements leave the card the same height: the two
   * margins are opposites, and their sum is the 16px the base hangs below
   * itself either way. Read off the sheet: jsdom applies no stylesheet.
   */
  it("keeps one gap above the row and the same gap below it", () => {
    const empty = ruleBody(
      SURFACE,
      ".auth-surface .auth-message + .auth-checkline",
    );
    const refused = ruleBody(
      SURFACE,
      ".auth-surface .auth-message--error + .auth-checkline",
    );

    // Both placements leave the same 17.3px above the checkbox and below the
    // row, the gap the consent row of the registration keeps above its own
    // button, measured by ink and not by margin.
    expect(empty).toContain("margin-top: -15.7px");
    expect(empty).toContain("margin-bottom: 1rem");
    expect(refused).toContain("margin-top: 5.3px");
    expect(refused).toContain("margin-bottom: 1rem");
  });

  it("leaves the reserved strip alone", () => {
    // The strip's height is what keeps every card of the surface still when the
    // server answers, and only the cards with a check line under it are being
    // placed. A shift written into the strip would move the rest of them.
    expect(
      ruleBody(SURFACE, ".auth-surface .auth-field + .auth-message"),
      "сдвиг чек-строки уехал в полосу сообщения и двигает все карточки",
    ).toContain("min-height: calc(#{$auth-strip} - #{$auth-strip-gap})");
  });
});
