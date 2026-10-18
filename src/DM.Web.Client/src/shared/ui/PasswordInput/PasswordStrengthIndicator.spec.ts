import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import PasswordStrengthIndicator from "./PasswordStrengthIndicator.vue";
import SOURCE from "./PasswordStrengthIndicator.vue?raw";

type Props = {
  password: string;
  isSameAsOld?: boolean;
  error?: string | null;
  always?: boolean;
};

/**
 * The single line under the bar, and what wins it.
 *
 * The answer the server gave about this password comes first: it is the only
 * one of the three that knows something the browser cannot find out. A lookup
 * against a public breach index used to run here and could not reach a verdict
 * in any build but a developer's own — the document allows connections to this
 * origin only — so the question moved to the server, and its answer arrives on
 * submit. A rule the field itself broke comes next, because it names what to
 * correct; the strength comes last, because it is advice rather than a refusal.
 */
const lineUnderTheBar = (props: Props) =>
  mount(PasswordStrengthIndicator, { props }).text();

/** The fill of the bar: how wide it is drawn, and whether it is drawn red. */
const bar = (props: Props) => {
  const fill = mount(PasswordStrengthIndicator, { props }).get(
    ".strength-fill",
  );
  return { style: fill.attributes("style"), classes: fill.classes() };
};

describe("PasswordStrengthIndicator", () => {
  it("names the rule the field broke before anything else", () => {
    expect(lineUnderTheBar({ password: "short" })).toBe("Минимум 8 символов");
  });

  it("names the strength of a password that breaks no rule", () => {
    expect(lineUnderTheBar({ password: "correct horse" })).toBe("Надежный");
  });

  it("keeps the old password above the strength", () => {
    expect(
      lineUnderTheBar({ password: "correct horse", isSameAsOld: true }),
    ).toBe("Новый пароль совпадает с текущим");
  });

  it("says nothing at all about an empty field", () => {
    expect(lineUnderTheBar({ password: "" })).toBe("");
  });

  it("states the minimum from the start where the screen asked for it", () => {
    // The guest forms have no hint of their own under the field: this line is
    // where the rule is written, and a rule shown only after it is broken was
    // never given to the reader.
    expect(lineUnderTheBar({ password: "", always: true })).toBe(
      "Минимум 8 символов",
    );
    expect(bar({ password: "", always: true }).style).toContain("width: 0%");
  });

  it("puts the answer of the server above every rule the field owns", () => {
    // Long enough, and still refused: the breach is a fact only the server has.
    const breached = {
      password: "correct horse battery",
      error: "Этот пароль скомпрометирован утечкой, выберите другой",
    };

    expect(lineUnderTheBar(breached)).toBe(
      "Этот пароль скомпрометирован утечкой, выберите другой",
    );
    expect(bar(breached).style).toContain("width: 100%");
    expect(bar(breached).classes).toContain("error");
  });

  it("writes the verdict in the colour of a hint, not in the colour of the bar", () => {
    // The bar already carries the reading, in colour and in length. The word
    // beside it repeating that same colour makes two signals out of one fact,
    // on cards where coloured text means a refusal — and "Надежный" is not one.
    // The bar keeps its colour; only the word gives it up.
    const verdict = mount(PasswordStrengthIndicator, {
      props: { password: "correct horse" },
    });

    // The gradient is driven by this custom property, and the line no longer
    // takes it. The bar still does.
    expect(verdict.get(".strength-text").attributes("style")).toBeUndefined();
    expect(verdict.get(".strength-fill").attributes("style")).toContain(
      "--progress",
    );

    // The colour itself is in the stylesheet, which no test runner compiles:
    // the rule is read where it is written.
    const rule = SOURCE.split("\n.strength-text\n")[1] ?? "";
    expect(rule).toContain("color: $text-muted");
    expect(rule).not.toContain("color-mix");
  });

  it("announces the answer of the server, and it alone", () => {
    // The refusal arrives on submit, after the reader has left the field and
    // stopped looking at it, so it is spoken. The rule and the strength change
    // under his own typing, and a live region there would read every keystroke
    // back to him.
    const refused = mount(PasswordStrengthIndicator, {
      props: {
        password: "correct horse battery",
        error: "Этот пароль скомпрометирован утечкой, выберите другой",
      },
    });

    expect(refused.get("[role='alert']").text()).toBe(
      "Этот пароль скомпрометирован утечкой, выберите другой",
    );

    const rule = mount(PasswordStrengthIndicator, {
      props: { password: "short" },
    });

    expect(rule.find("[role='alert']").exists()).toBe(false);
  });
});
